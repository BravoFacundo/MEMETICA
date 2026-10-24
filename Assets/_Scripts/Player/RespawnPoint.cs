using UnityEngine;

// A place where the player reappears after falling into the void. Put it on the ground (Memetica > Level > Respawn >
// New Respawn Point puts one where you look in the Scene view): you reappear Height meters above it, fall onto it, and
// face its arrow (its blue axis), ready to try again.
// Several on one island: you reappear at the one nearest to where you last stood on it (a parkour course: one before
// each hard part, facing it). The hub's one is LevelManager > Start Respawn Point.
// It must be inside its island (any depth). Children just named RespawnPoint, without this, still work (Game_Old):
// they reappear you from the default height, facing where you were.
public class RespawnPoint : MonoBehaviour
{
    [Tooltip("Turns you to face its arrow (blue) when you reappear")]
    public bool face = true;
    [Tooltip("Meters above it you reappear. 0 = the default (Game: the player's RespawnSlam Height; Game_Old: LevelManager's Drop Distance)")]
    public float height = 0f;

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
        Color color = new Color(0.3f, 1f, 0.5f, selected ? 1f : 0.7f);
        Gizmos.color = color;
        Vector3 ground = transform.position + Vector3.up * 0.02f;

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
