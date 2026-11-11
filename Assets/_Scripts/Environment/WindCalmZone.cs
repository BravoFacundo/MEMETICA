using System.Collections.Generic;
using UnityEngine;

// A calm spot: an island's middle (or wherever you put it: by hand, it needn't be the island's own center). Inside
// Calm Radius you hear only the light wind; from there to Wind Radius the strong one comes back little by little;
// outside, only the strong one (WindAmbience). It reaches Height meters above and below its middle: falling off the
// island, the strong wind is back. It moves with what it is in (a floating island).
// Selected, the Scene view shows it: the inner circle (calm), the outer one (strong again), and its height.
public class WindCalmZone : MonoBehaviour
{
    [Tooltip("Meters (flat, from its middle) where it is fully calm: only the light wind")]
    public float calmRadius = 8f;
    [Tooltip("Meters (flat) where the strong wind is fully back. Between both: it blends")]
    public float windRadius = 15f;
    [Tooltip("Meters above and below its middle it reaches, fully")]
    public float height = 6f;
    [Tooltip("Meters past Height where it fades out (falling below the island, flying above it)")]
    public float heightFade = 6f;

    public static readonly List<WindCalmZone> All = new List<WindCalmZone>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // 1 fully calm here, 0 strong wind
    public float CalmAt(Vector3 position)
    {
        Vector3 d = position - transform.position;
        float flat = new Vector2(d.x, d.z).magnitude;
        float around = 1f - Mathf.InverseLerp(calmRadius, Mathf.Max(windRadius, calmRadius + 0.01f), flat);
        float vertical = 1f - Mathf.InverseLerp(height, height + Mathf.Max(heightFade, 0.01f), Mathf.Abs(d.y));
        return around * vertical;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 c = transform.position;
        UnityEditor.Handles.color = new Color(0.4f, 0.9f, 1f, 0.9f);   // calm
        UnityEditor.Handles.DrawWireDisc(c, Vector3.up, calmRadius, 2f);
        UnityEditor.Handles.color = new Color(1f, 0.6f, 0.3f, 0.9f);   // strong again
        UnityEditor.Handles.DrawWireDisc(c, Vector3.up, windRadius, 2f);
        UnityEditor.Handles.color = new Color(0.4f, 0.9f, 1f, 0.4f);
        UnityEditor.Handles.DrawWireDisc(c + Vector3.up * height, Vector3.up, calmRadius);
        UnityEditor.Handles.DrawWireDisc(c - Vector3.up * height, Vector3.up, calmRadius);
    }
#endif
}
