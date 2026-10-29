using System.Collections.Generic;
using UnityEngine;

// A place where the player reappears after falling into the void. Put it on the ground (Memetica > Level > Respawn >
// New Respawn Point puts one where you look in the Scene view): you reappear Height meters above it, fall onto it, and
// face its arrow (its blue axis), ready to try again.
// Several on one island: you reappear at the one nearest to where you last stood on it (a parkour course: one before
// each hard part, facing it). The hub's one is LevelManager > Start Respawn Point.
// Inside its island (any depth): it is that island's. Children just named RespawnPoint, without this, still work
// (Game_Old): they reappear you from the default height, facing where you were.
// Inside a level instead (not in any island), standing on an island (the hub): it is that island's, but only once its
// level is up (any of the level's islands: its target island rises first). E.g. on the hub, facing the level's first
// bridge: it turns with the level (its root at 0,0), so it keeps facing it. Until then the island's other ones are used.
[SelectionBase]   // clicking any of its parts in the Scene view selects this
public class RespawnPoint : MonoBehaviour
{
    [Tooltip("Turns you to face its arrow (blue) when you reappear")]
    public bool face = true;
    [Tooltip("Meters above it you reappear. 0 = the default (Game: the player's RespawnSlam Height; Game_Old: LevelManager's Drop Distance)")]
    public float height = 0f;
    public static readonly List<RespawnPoint> All = new List<RespawnPoint>();

    // The island it is for: the one it is inside, else the one under it (kept in its level)
    public Transform OwnIsland { get; private set; }
    // Kept in a level (not inside an island): that level's islands. It counts once one of them is up
    private Island[] levelIslands;
    // Kept in a level: that level's root (the PortalWindow of that level brings you back here)
    public Transform LevelRoot { get; private set; }

    void Awake()   // (before the islands go down and hide their ground)
    {
        Island inside = GetComponentInParent<Island>();
        if (inside != null) { OwnIsland = inside.transform; return; }
        OwnIsland = IslandUnder();
        if (OwnIsland == null) Debug.LogWarning($"[RespawnPoint] '{name}' isn't inside an island nor on one: it won't be used.", this);
        levelIslands = LevelIslands(transform, out Transform root);
        LevelRoot = root;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // It can be used now: inside its island, always; kept in a level, once the level is up
    public bool Active
    {
        get
        {
            if (levelIslands == null) return true;
            foreach (Island island in levelIslands)
                if (island != null && island.IsUp && !island.IsHidden) return true;
            return false;
        }
    }

    // The island (its root) right under it
    Transform IslandUnder()
    {
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 1f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            Island island = hit.transform.GetComponentInParent<Island>();
            if (island != null) return island.transform;
        }
        return null;
    }

    // The nearest parent that holds islands (its level), or null
    static Island[] LevelIslands(Transform point, out Transform root)
    {
        root = null;
        for (Transform t = point.parent; t != null; t = t.parent)
        {
            Island[] islands = t.GetComponentsInChildren<Island>(true);
            if (islands.Length > 0) { root = t; return islands; }
        }
        return null;
    }

    // Where you look when you reappear: its forward, level
    public Vector3 Facing
    {
        get
        {
            Vector3 flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward;
        }
    }

#if UNITY_EDITOR
    // On the ground: a ring and its arrow. Above: where you reappear, and the fall down to it
    void OnDrawGizmos()
    {
        bool selected = UnityEditor.Selection.Contains(gameObject);
        bool waits = GetComponentInParent<Island>() == null;   // (yellow: kept in a level, it waits for it to be up)
        Color color = waits ? new Color(1f, 0.85f, 0.3f, selected ? 1f : 0.7f) : new Color(0.3f, 1f, 0.5f, selected ? 1f : 0.7f);
        Gizmos.color = color;
        Vector3 ground = transform.position + Vector3.up * 0.02f;
        Gizmos.DrawSphere(ground + Vector3.up * 0.2f, selected ? 0.25f : 0.18f);   // (solid: click it to select it)

        const int segments = 24;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
            Gizmos.DrawLine(ground + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f, ground + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * 0.5f);
        }

        Vector3 forward = Facing, side = Vector3.Cross(Vector3.up, forward);
        Vector3 tip = ground + forward * 1.3f;
        Gizmos.color = face ? new Color(0.3f, 0.6f, 1f) : color;
        Gizmos.DrawLine(ground, tip);
        Gizmos.DrawLine(tip, tip - forward * 0.35f + side * 0.25f);
        Gizmos.DrawLine(tip, tip - forward * 0.35f - side * 0.25f);

        // Where you reappear: dashed down to the ground
        Gizmos.color = color;
        Vector3 top = transform.position + Vector3.up * EditorHeight();
        for (float y = 0f; y < top.y - ground.y; y += 0.6f)
            Gizmos.DrawLine(ground + Vector3.up * y, ground + Vector3.up * Mathf.Min(y + 0.3f, top.y - ground.y));
        Gizmos.DrawWireSphere(top, 0.4f);
        Gizmos.DrawLine(top, top + forward * 0.8f);
        if (selected) UnityEditor.Handles.Label(top + Vector3.up * 0.6f, name);
    }

    // The height it will use (its own, or the default from the scene)
    float EditorHeight()
    {
        if (height > 0f) return height;
        RespawnSlam slam = FindObjectOfType<RespawnSlam>();
        if (slam != null && slam.enabled) return slam.height;
        LevelManager level = FindObjectOfType<LevelManager>();
        return level != null ? level.dropDistance : 8f;
    }
#endif
}
