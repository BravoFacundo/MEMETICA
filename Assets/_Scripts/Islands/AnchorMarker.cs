using UnityEngine;

// A bridge anchor you can see and click in the Scene view: a small orange sphere (Memetica > Level > Stone Path >
// Mark Bridge Anchors adds it to every bridge's anchors). Only a marker: it does nothing while playing.
public class AnchorMarker : MonoBehaviour
{
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        bool selected = UnityEditor.Selection.Contains(gameObject);
        Gizmos.color = new Color(1f, 0.55f, 0.1f, selected ? 1f : 0.8f);
        Gizmos.DrawSphere(transform.position, selected ? 0.3f : 0.2f);
        if (selected) UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, name);
    }
#endif
}
