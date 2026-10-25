using UnityEngine;

// The game's HUD (the meme window, the virus timer, the tutorial popups…) hides while paused and on the end screens
// (GameManager: win and lose), fading quickly. Goes on the HUD group only: not on what must show then (the pause
// window, the blue screen).
[RequireComponent(typeof(CanvasGroup))]
public class HudVisibility : MonoBehaviour
{
    [Tooltip("Seconds it takes to fade out or in")]
    public float fadeTime = 0.15f;

    private CanvasGroup group;
    private float shown = 1f;

    void Awake() => group = GetComponent<CanvasGroup>();

    void Update()
    {
        bool ended = GameManager.Instance != null && GameManager.Instance.Ended;
        bool visible = !PauseMenu.GameIsPaused && !ended;
        shown = Mathf.MoveTowards(shown, visible ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));   // (the pause stops time)
        group.alpha = shown;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }
}
