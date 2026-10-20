// Select a bridge: lines between each stone (falling platforms) and from/to its anchors, colored by whether
// the player can make it, with the Player's real values (jump, speed, Step Offset):
//   green   walk over it (small gap, step lower than Step Offset)
//   yellow  needs a jump, but it can be done walking (running gives margin)
//   orange  only running
//   red     can't be done
// The gap and the height difference are written on each line. Works in edit mode (where the stones are placed)
// and in play mode (where the bridge actually lays them out).

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ParkourValidator
{
    [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy)]
    static void DrawBridge(Bridge bridge, GizmoType type)
    {
        PlayerMove player = Object.FindObjectOfType<PlayerMove>(true);
        if (player == null) return;
        CharacterController body = player.GetComponent<CharacterController>();
        float stepOffset = body != null ? body.stepOffset : 0.3f;

        // Points along the bridge: anchor A, each stone (its top middle), anchor B
        var stones = bridge.GetComponentsInChildren<FallingPlatform>(true).Select(p => TopOf(p.transform)).ToList();
        if (stones.Count == 0) return;   // solid bridges are walked, nothing to check
        Vector3 start = bridge.anchorA != null ? bridge.anchorA.position : stones[0].center;
        Vector3 direction = (bridge.anchorB != null ? bridge.anchorB.position : stones[stones.Count - 1].center) - start;
        direction.y = 0f;
        stones = stones.OrderBy(s => Vector3.Dot(s.center - start, direction)).ToList();

        var points = new List<Bounds>();
        if (bridge.anchorA != null) points.Add(new Bounds(bridge.anchorA.position, Vector3.zero));
        points.AddRange(stones);
        if (bridge.anchorB != null) points.Add(new Bounds(bridge.anchorB.position, Vector3.zero));

        for (int i = 1; i < points.Count; i++)
        {
            Bounds from = points[i - 1], to = points[i];
            Vector3 a = new Vector3(from.center.x, from.max.y, from.center.z);
            Vector3 b = new Vector3(to.center.x, to.max.y, to.center.z);
            float gap = FlatGap(from, to);
            float rise = b.y - a.y;

            Color color = Rate(gap, rise, stepOffset, player);
            Handles.color = color;
            Handles.DrawAAPolyLine(4f, a, b);
            Handles.Label((a + b) * 0.5f + Vector3.up * 0.4f, $"{gap:0.0} m  {(rise >= 0 ? "+" : "")}{rise:0.0}", Style(color));
        }
    }

    static Color Rate(float gap, float rise, float stepOffset, PlayerMove player)
    {
        if (gap < 0.3f && rise <= stepOffset) return Color.green;
        float walk = Reach(player.walkSpeed, rise, player);
        float run = Reach(player.runSpeed, rise, player);
        if (gap <= walk * 0.8f) return Color.yellow;   // 20 % margin: it is not a pixel-perfect game
        if (gap <= run * 0.8f) return new Color(1f, 0.5f, 0f);
        return Color.red;
    }

    // Horizontal meters a jump covers at that speed, landing 'rise' meters higher (or lower if negative)
    static float Reach(float speed, float rise, PlayerMove player)
    {
        float g = -player.gravity;
        float up = Mathf.Sqrt(2f * g * player.jumpHeight);
        float discriminant = up * up - 2f * g * rise;
        if (discriminant < 0f) return 0f;   // too high, never gets there
        float time = (up + Mathf.Sqrt(discriminant)) / g;
        return speed * time;
    }

    // Distance between the edges of two pieces seen from above (0 if they touch)
    static float FlatGap(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Abs(a.center.x - b.center.x) - a.extents.x - b.extents.x);
        float dz = Mathf.Max(0f, Mathf.Abs(a.center.z - b.center.z) - a.extents.z - b.extents.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // What the player stands on: its meshes seen as one box (meshes keep their size even while hidden)
    static Bounds TopOf(Transform piece)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(piece.position, Vector3.one * 0.5f);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static GUIStyle Style(Color color)
    {
        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = color;
        return style;
    }
}
