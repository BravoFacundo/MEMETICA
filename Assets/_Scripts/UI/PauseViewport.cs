using UnityEngine;

// Pause "framing" effect, both parts driven by the same animation:
//   - the camera view shrinks a few pixels on every side (Camera.rect: costs nothing)
//   - the monitor frame comes in from outside the screen (scaled up → normal size) and settles around it
// On resume both go back: the view grows to full screen and the frame leaves the screen, then turns off.
// Goes in Managers (or anywhere in the Game scene). Owns Canvas_Frame: nothing else should toggle it.
public class PauseViewport : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player camera (Main Camera)")]
    public Camera targetCamera;
    [Tooltip("Canvas_Frame: turned on only while it is visible; its scale keeps the margin right at any resolution")]
    public Canvas frameCanvas;
    [Tooltip("The frame image inside Canvas_Frame (the one that gets scaled)")]
    public RectTransform frame;

    [Header("Effect")]
    [Tooltip("Margin on each side while paused, in frame pixels (the frame border is 10)")]
    public float marginPixels = 10f;
    [Tooltip("Frame scale when hidden: big enough to push its border off screen")]
    public float hiddenScale = 1.06f;
    [Tooltip("Seconds to go in or out")]
    public float duration = 0.15f;

    float t = -1f;   // 0 = playing (full view, frame off screen), 1 = paused (framed). -1 forces the first apply

    void Start() => Apply(0f);

    void Update()
    {
        float target = PauseMenu.GameIsPaused ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;

        // Unscaled time: the game is frozen while paused
        Apply(Mathf.MoveTowards(Mathf.Max(t, 0f), target, Time.unscaledDeltaTime / Mathf.Max(duration, 0.01f)));
    }

    void Apply(float value)
    {
        t = value;
        float eased = t * t * (3f - 2f * t);   // smoothstep

        // Frame: visible only while animating or paused
        if (frameCanvas != null && frameCanvas.gameObject.activeSelf != (t > 0f)) frameCanvas.gameObject.SetActive(t > 0f);
        if (frame != null) frame.localScale = Vector3.one * Mathf.Lerp(hiddenScale, 1f, eased);

        // Camera view
        if (targetCamera == null) return;
        float scale = frameCanvas != null ? frameCanvas.scaleFactor : 1f;
        float marginX = marginPixels * scale / Screen.width * eased;
        float marginY = marginPixels * scale / Screen.height * eased;
        targetCamera.rect = new Rect(marginX, marginY, 1f - 2f * marginX, 1f - 2f * marginY);
    }

    void OnDisable()
    {
        if (targetCamera != null) targetCamera.rect = new Rect(0f, 0f, 1f, 1f);   // never leave it shrunk
    }
}
