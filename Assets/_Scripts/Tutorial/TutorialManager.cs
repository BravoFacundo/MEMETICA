using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Shows and hides the tutorial popups. Goes in Managers > TutorialManager.
// Popups are the children of Canvas_Tutorial, placed freely (an aesthetic decision per popup).
// TutorialTriggers say which popup to show; this manager does the fading and the pause hiding.
//   - All popups start hidden.
//   - Several popups can be visible at once (each one in its own place).
//   - If two triggers ask for the same popup, it stays visible until both release it.
//   - While the game is paused the whole canvas is hidden (the pause window owns the screen).
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Canvas_Tutorial: its children are the popups")]
    public Transform popupCanvas;

    [Header("Pause")]
    [Tooltip("Hide all popups while the game is paused")]
    public bool hideWhilePaused = true;

    [Header("Fade")]
    [Tooltip("Seconds to fade a popup in or out")]
    public float fadeTime = 0.25f;

    readonly Dictionary<GameObject, int> requests = new Dictionary<GameObject, int>();
    readonly Dictionary<GameObject, CanvasGroup> popups = new Dictionary<GameObject, CanvasGroup>();
    CanvasGroup canvasGroup;

    void Awake()
    {
        Instance = this;
        if (popupCanvas == null) { Debug.LogError("[TutorialManager] Assign Popup Canvas (Canvas_Tutorial).", this); enabled = false; return; }

        // One CanvasGroup on the whole canvas: hides everything during pause and never takes clicks
        canvasGroup = popupCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = popupCanvas.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        foreach (Transform child in popupCanvas)
        {
            CanvasGroup g = GroupOf(child.gameObject);
            g.alpha = 0f;
            child.gameObject.SetActive(false);
        }
    }

    public void Show(GameObject popup)
    {
        if (popup == null) return;
        requests[popup] = Count(popup) + 1;
        GroupOf(popup);
        popup.SetActive(true);
    }

    public void Hide(GameObject popup)
    {
        if (popup == null) return;
        requests[popup] = Mathf.Max(0, Count(popup) - 1);
    }

    void Update()
    {
        canvasGroup.alpha = (hideWhilePaused && PauseMenu.GameIsPaused) ? 0f : 1f;

        float step = Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f);
        foreach (var pair in popups.ToList())
        {
            GameObject popup = pair.Key;
            CanvasGroup g = pair.Value;
            if (popup == null || !popup.activeSelf) continue;

            float target = Count(popup) > 0 ? 1f : 0f;
            g.alpha = Mathf.MoveTowards(g.alpha, target, step);
            if (target == 0f && g.alpha == 0f) popup.SetActive(false);   // fully faded out
        }
    }

    int Count(GameObject popup) => requests.TryGetValue(popup, out int n) ? n : 0;

    CanvasGroup GroupOf(GameObject popup)
    {
        if (popups.TryGetValue(popup, out CanvasGroup g)) return g;
        g = popup.GetComponent<CanvasGroup>();
        if (g == null) g = popup.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        popups[popup] = g;
        return g;
    }
}
