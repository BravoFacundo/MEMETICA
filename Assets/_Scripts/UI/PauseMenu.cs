using UnityEngine;

// Pause: Esc or P. Shows the pause window, frees the cursor and freezes the game.
// Also the pause "framing" effect (before: PauseViewport), both parts driven by the same animation:
//   - the camera view shrinks a few pixels on every side (Camera.rect: costs nothing)
//   - the monitor frame comes in from outside the screen (scaled up → normal size) and settles around it
// On resume both go back. Owns Canvas_Frame: nothing else should toggle it (Framed asks for it without pausing).
public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;
    // The frame on without pausing (OrbitView: the view behind the end screens, the menu)
    public static bool Framed = false;

    [Header("Frame Effect")]
    [Tooltip("Margin on each side while paused, in frame pixels (the frame border is 10)")]
    public float marginPixels = 10f;
    [Tooltip("Frame scale when hidden: big enough to push its border off screen")]
    public float hiddenScale = 1.06f;
    [Tooltip("Seconds to go in or out")]
    public float duration = 0.15f;

    [Header("References")]
    [Tooltip("The pause window (SettingsWindow): shown while paused")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private UIManager uiManager;  // loads the Menu and quits with the busy cursor
    [Tooltip("Player camera (Main Camera)")]
    public Camera targetCamera;
    [Tooltip("Canvas_Frame: turned on only while it is visible; its scale keeps the margin right at any resolution")]
    public Canvas frameCanvas;
    [Tooltip("The frame image inside Canvas_Frame (the one that gets scaled)")]
    public RectTransform frame;

    private float frameAmount = -1f;
    private PlayerLook look;
    // The player's (not Camera.main: the end screens' OrbitView turns that camera off)
    private PlayerLook Look => look != null ? look : (look = targetCamera != null ? targetCamera.GetComponent<PlayerLook>() : FindObjectOfType<PlayerLook>());   // 0 = playing (full view, frame off screen), 1 = paused (framed). -1 forces the first apply

    void Start()
    {
        // A fresh Game scene always starts unpaused (the static flag survives scene loads)
        GameIsPaused = false;
        Framed = false;
        Time.timeScale = 1f;
        ApplyFrame(0f);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (GameIsPaused) Resume();
            else Pause();
        }
        AnimateFrame();
    }

    void OnDisable()
    {
        if (targetCamera != null) targetCamera.rect = new Rect(0f, 0f, 1f, 1f);   // never leave it shrunk
    }

    public void Resume()
    {
        GameIsPaused = false;

        pauseMenuUI.SetActive(false);
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetFollowMouse(false);   // back to the fixed aim (RetroCursor sets the icon)

        Time.timeScale = 1f;
        if (Look != null) Look.playerCanLook = true;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Pause()
    {
        GameIsPaused = true;

        pauseMenuUI.SetActive(true);
        if (RetroCursor.Instance != null)
        {
            RetroCursor.Instance.SetFollowMouse(true);              // free cursor, like in the menu
            RetroCursor.Instance.SetState(CursorState.Default);     // the arrow
        }

        Time.timeScale = 0f;
        if (Look != null) Look.playerCanLook = false;
        Cursor.lockState = CursorLockMode.None;
    }

    // Pause → Menu: the game stays frozen behind the busy cursor; UIManager unpauses right before switching
    public void ReturnToMenu()
    {
        if (uiManager != null) uiManager.GoToMenu();
        else Debug.LogWarning("[PauseMenu] Assign the UIManager to return to the Menu.", this);
    }

    public void QuitGame()
    {
        if (uiManager != null) uiManager.QuitGame();
        else Application.Quit();
    }

    //---- Frame effect (before: PauseViewport)

    private void AnimateFrame()
    {
        float target = GameIsPaused || Framed ? 1f : 0f;
        if (Mathf.Approximately(frameAmount, target)) return;

        // Unscaled time: the game is frozen while paused
        ApplyFrame(Mathf.MoveTowards(Mathf.Max(frameAmount, 0f), target, Time.unscaledDeltaTime / Mathf.Max(duration, 0.01f)));
    }

    private void ApplyFrame(float value)
    {
        frameAmount = value;
        float eased = frameAmount * frameAmount * (3f - 2f * frameAmount);   // smoothstep

        // Frame: visible only while animating or paused
        if (frameCanvas != null && frameCanvas.gameObject.activeSelf != (frameAmount > 0f)) frameCanvas.gameObject.SetActive(frameAmount > 0f);
        if (frame != null) frame.localScale = Vector3.one * Mathf.Lerp(hiddenScale, 1f, eased);

        // Camera view
        if (targetCamera == null) return;
        float scale = frameCanvas != null ? frameCanvas.scaleFactor : 1f;
        float marginX = marginPixels * scale / Screen.width * eased;
        float marginY = marginPixels * scale / Screen.height * eased;
        targetCamera.rect = new Rect(marginX, marginY, 1f - 2f * marginX, 1f - 2f * marginY);
    }
}
