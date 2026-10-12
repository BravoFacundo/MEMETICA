using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("References")]
    [Tooltip("The pause window (SettingsWindow): shown while paused")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private UIManager uiManager;  // loads the Menu and quits with the busy cursor

    void Start()
    {
        // A fresh Game scene always starts unpaused (the static flag survives scene loads)
        GameIsPaused = false;
        Time.timeScale = 1f;
        // The monitor frame is animated by PauseViewport
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }

        }
    }

    public void Resume()
    {
        GameIsPaused = false;

        pauseMenuUI.SetActive(false);
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetFollowMouse(false);   // back to the fixed crosshair (Crosshair sets the icon)

        Time.timeScale = 1f;
        Camera.main.GetComponent<PlayerLook>().playerCanLook = true;
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
        Camera.main.GetComponent<PlayerLook>().playerCanLook = false;
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
}
