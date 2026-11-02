// What each desktop thing does, and moving between scenes. Goes in Managers > UIManager (every scene).
// The icons and buttons (WindowButton) only detect the click; in their "On Click" / "On Open" a method of this is picked.
// Example: Button_Play → On Open → UIManager.PlayGame()
// Menu: the Play icons (with the tutorial popup first). Game: the Screens (pause and end screen, one at a time, the
// HUD hidden behind them), Play Again and back to the Menu. Both: loading with the busy cursor, quitting, windows.

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Section("Scenes")]
    [Tooltip("The new Memetica (the Play icon)")]
    public SceneField gameScene = new SceneField("Game");
    [Tooltip("The original Memetica, opened from the Recycle Bin")]
    public SceneField oldGameScene = new SceneField("OriginalGame");
    [Tooltip("Where the pause and the end screen go back to")]
    public SceneField menuScene = new SceneField("Menu");

    [Section("Tutorial")]
    [Tooltip("Shown when a Play icon opens a game: does it skip the tutorial? Its buttons: Yes → UIManager.StartWithoutTutorial, " +
             "No → UIManager.StartWithTutorial. Empty: the games start with the tutorial")]
    public GameObject tutorialPopup;
    [Tooltip("Play Again loads the same game skipping its tutorial (the same player: they already did it)")]
    public bool playAgainSkipsTutorial = true;

    [Section("Screens (Game)")]
    [Tooltip("The HUD group (meme window, virus timer, tutorial popups): fades out while a Screen is open. Needs a CanvasGroup (added if missing)")]
    public CanvasGroup hud;
    [Tooltip("Esc / P / Start opens it: the game freezes and the cursor is free. Empty: no pause in this scene (the Menu)")]
    public GameObject pauseScreen;
    [Tooltip("The end screen (GameManager shows it): while it is open there is no pause")]
    public GameObject endScreen;
    [Tooltip("Seconds the HUD takes to fade out or in")]
    public float hudFadeTime = 0.15f;

    [Section("Loading", Closed = true)]
    [Tooltip("Minimum seconds a load takes (busy cursor, music fading out), even if the scene is ready before")]
    public float minLoadingTime = 1.5f;
    [Tooltip("Seconds it \"thinks\" before closing the game")]
    public float quitDelay = 2f;

    private bool loading;
    private string pendingScene;   // the game the tutorial popup is asking about

    // The game is paused (the pause Screen is open): time frozen, the player still
    public static bool GameIsPaused;

    private float hudShown = 1f;
    private PlayerLook look;
    private PlayerLook Look => look != null ? look : (look = FindObjectOfType<PlayerLook>());
    bool EndOpen => endScreen != null && endScreen.activeInHierarchy;

    void Start()
    {
        // A fresh scene always starts unpaused (the static flag survives scene loads)
        GameIsPaused = false;
        Time.timeScale = 1f;
        if (pauseScreen != null) pauseScreen.SetActive(false);
        if (hud != null) { hud.alpha = 1f; hud.blocksRaycasts = hud.interactable = true; }
    }

    void Update()
    {
        // Pause: only with a pause Screen, never over the end screen or while loading
        if (pauseScreen != null && !loading && GameInput.PauseDown)
        {
            if (GameIsPaused) Resume();
            else if (!EndOpen) Pause();
        }
        UpdateHud();
    }

    // ---------- Screens ----------

    // The pause window's Continue (and Esc again)
    public void Resume()
    {
        if (!GameIsPaused) return;
        GameIsPaused = false;
        if (pauseScreen != null) pauseScreen.SetActive(false);
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetFollowMouse(false);   // back to the fixed aim (RetroCursor sets the icon)
        Time.timeScale = 1f;
        if (Look != null) Look.playerCanLook = true;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Pause()
    {
        if (GameIsPaused || pauseScreen == null || EndOpen) return;
        GameIsPaused = true;
        OpenWindow(pauseScreen);
        if (RetroCursor.Instance != null)
        {
            RetroCursor.Instance.SetFollowMouse(true);              // free cursor, like in the menu
            RetroCursor.Instance.SetState(CursorState.Default);     // the arrow
        }
        Time.timeScale = 0f;
        if (Look != null) Look.playerCanLook = false;
        Cursor.lockState = CursorLockMode.None;
    }

    // The HUD shows only while no Screen is open
    void UpdateHud()
    {
        if (hud == null) return;
        bool visible = !GameIsPaused && !EndOpen;
        hudShown = Mathf.MoveTowards(hudShown, visible ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(hudFadeTime, 0.01f));   // (the pause stops time)
        hud.alpha = hudShown;
        hud.blocksRaycasts = hud.interactable = visible;
    }

    // ---------- Icons ----------

    public void PlayGame() => AskTutorial(gameScene);

    // Button_Play inside the Recycle Bin window
    public void PlayOldGame() => AskTutorial(oldGameScene);

    // ---------- Tutorial popup ----------

    // The popup asks first (without one: straight in, with the tutorial)
    void AskTutorial(string sceneName)
    {
        if (loading) return;   // (a double click too many doesn't load twice)
        pendingScene = sceneName;
        if (tutorialPopup != null) OpenWindow(tutorialPopup);
        else StartGame(false);
    }

    // The popup's buttons
    public void StartWithTutorial() => StartGame(false);
    public void StartWithoutTutorial() => StartGame(true);

    void StartGame(bool skipTutorial)
    {
        if (loading || string.IsNullOrEmpty(pendingScene)) return;
        if (tutorialPopup != null) tutorialPopup.SetActive(false);
        LevelManager.SkipFromMenu = skipTutorial;   // the game scene's LevelManager reads it when it starts
        StartCoroutine(LoadScene(pendingScene));
    }

    // ---------- Game ----------

    // The end screen's Play Again: this game from the start
    public void PlayAgain()
    {
        if (loading) return;
        LevelManager.SkipFromMenu = playAgainSkipsTutorial;
        StartCoroutine(LoadScene(SceneManager.GetActiveScene().name));
    }

    // The pause window and the end screen (Game → Menu)
    public void GoToMenu()
    {
        if (loading) return;
        StartCoroutine(LoadScene(menuScene));
    }

    public void QuitGame()
    {
        if (loading) return;   // (a click too many doesn't repeat it)
        StartCoroutine(Quit());
    }

    // ---------- Windows ----------
    // They work even if the window is inactive: it is referenced from the Inspector.

    public void OpenWindow(GameObject window)
    {
        window.SetActive(true);
        window.transform.SetAsLastSibling();   // the one opened comes to the front
    }

    public void CloseWindow(GameObject window)
    {
        window.SetActive(false);
    }

    // ---------- Inside ----------

    IEnumerator Quit()
    {
        loading = true;

        // As when loading: busy cursor, nothing clickable and the music fades out
        if (EventSystem.current != null) EventSystem.current.enabled = false;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetBusy(true);
        if (AudioManager.Instance != null) AudioManager.Instance.FadeOutMusic(quitDelay);

        yield return new WaitForSecondsRealtime(quitDelay);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // in the Editor: stops Play
#else
        Application.Quit();
#endif
    }

    IEnumerator LoadScene(string sceneName)
    {
        loading = true;

        // While loading: busy cursor and nothing else clickable
        if (EventSystem.current != null) EventSystem.current.enabled = false;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetBusy(true);
        if (AudioManager.Instance != null) AudioManager.Instance.FadeOutMusic(minLoadingTime);   // the music fades out while loading
        yield return null;   // one frame so the busy cursor shows before it starts

        // Loads in the background, without switching to the new scene yet
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        float start = Time.unscaledTime;

        // Unity keeps the progress at 0.9 until the scene is allowed to activate
        while (op.progress < 0.9f || Time.unscaledTime - start < minLoadingTime)
            yield return null;

        // Leave the time and pause state clean for the next scene (coming from the pause window)
        Time.timeScale = 1f;
        GameIsPaused = false;

        op.allowSceneActivation = true;   // here the scene switches
    }
}
