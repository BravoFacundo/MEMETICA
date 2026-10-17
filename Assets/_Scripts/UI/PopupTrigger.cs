using UnityEngine;

// Popup zone (before: TutorialTrigger): while the player is inside, its popup is shown (TutorialManager does the fading).
// Needs a collider marked as Trigger. Any mesh on it is hidden when the game starts,
// so the zone can stay visible in the editor while you place it.
[RequireComponent(typeof(Collider))]
public class PopupTrigger : MonoBehaviour
{
    [Tooltip("Popup (child of Canvas_Tutorial) to show while the player is inside")]
    public GameObject popup;
    [Tooltip("Hide the popup when the player leaves the zone")]
    public bool hideOnExit = true;

    bool inside;

    // When the component is added: make the collider a trigger
    void Reset() => GetComponent<Collider>().isTrigger = true;

    void Awake()
    {
        if (TryGetComponent(out Renderer r)) r.enabled = false;
        if (!GetComponent<Collider>().isTrigger)
            Debug.LogWarning("[PopupTrigger] '" + name + "' collider is not a Trigger: it will block the player.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (inside || !IsPlayer(other)) return;
        inside = true;
        if (TutorialManager.Instance != null) TutorialManager.Instance.Show(popup);
    }

    void OnTriggerExit(Collider other)
    {
        if (!inside || !IsPlayer(other)) return;
        inside = false;
        if (hideOnExit && TutorialManager.Instance != null) TutorialManager.Instance.Hide(popup);
    }

    static bool IsPlayer(Collider c) => c.GetComponentInParent<PlayerMove>() != null;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!TryGetComponent(out BoxCollider box)) return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
#endif
}
