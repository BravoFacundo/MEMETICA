// What each desktop thing does, and moving between scenes. Goes in Managers > UIManager (every scene).
// The icons and buttons (WindowButton) only detect the click; in their "On Click" / "On Open" a method of this is picked.
// Example: Button_Play → On Open → UIManager.PlayGame()
// Menu: the Play icons (with the tutorial popup first). Game: the Screens (pause and end screen, one at a time, the
// HUD hidden behind them), Play Again and back to the Menu. Both: loading with the busy cursor, quitting, windows.
// Screens: the HUD, the pause, the end screen (later the MemeDex…), each with its Role. Appear In Order: each time it
// opens its parts appear one after another (ScreenAppear: its children that are on, in hierarchy order or reversed,
// each with its UIWindow's sound).
// On or off, however they were left in the scene (turn things on or off freely while you work on them):
//   At the start (Arrange): on (with their parents) what must work from the first frame: the HUD, its meme windows,
//   the virus timer, the cursor. Off: the screens that wait to be opened (Pause, End, Other), the tutorial popup and
//   every window that Opens With Button (UIWindow: Configuration, Recycle Bin, Exit?…).
//   Opening a screen (Pause, End, Other: however it is opened): its parts (its children) are turned on, except the
//   ones that Open With Button (a window inside it that a button opens) - then they appear in order, if it does.
// The scoreboard and the MemeDex are screens of their own (Other): their buttons call OpenWindow / CloseWindow.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]   // (before the rest: what it turns on wakes up before anyone looks for it)
public class UIManager : MonoBehaviour
{
    [Section("Scenes")]
    [Tooltip("The new Memetica (the Play icon)")]
    public SceneField gameScene = new SceneField("Game");
    [Tooltip("The original Memetica, opened from the Recycle Bin")]
    public SceneField originalGameScene = new SceneField("OriginalGame");
    [Tooltip("Where the pause and the end screen go back to")]
    public SceneField menuScene = new SceneField("Menu");

    [Section("Tutorial")]
    [Tooltip("Shown when a Play icon opens a game: does it skip the tutorial? Its buttons: Yes → UIManager.StartWithoutTutorial, " +
             "No → UIManager.StartWithTutorial. Empty: the games start with the tutorial")]
    public GameObject tutorialPopup;
    [Tooltip("Play Again loads the same game skipping its tutorial (the same player: they already did it)")]
    public bool playAgainSkipsTutorial = true;

    public enum ScreenRole { Other, Hud, Pause, End }

    [System.Serializable]
    public class ScreenEntry
    {
        public GameObject target;
        [Tooltip("Hud: fades out while another screen is open. Pause: Esc / P / Start opens it (the game freezes, the " +
                 "cursor is free). End: the end screen (GameManager shows it; no pause over it). Other: any (the MemeDex…)")]
        public ScreenRole role = ScreenRole.Other;
        [Tooltip("Each time it opens, its parts appear one after another (its children that are on), each with its sound")]
        public bool appearInOrder;
        [Tooltip("Appear In Order: from the last child up")]
        public bool reverse;

        public Transform Parts => target.transform;   // (its parts: its children)

        [System.NonSerialized] public bool wasOn;
        [System.NonSerialized] public ScreenAppear appear;
        [System.NonSerialized] public Coroutine running;
        [System.NonSerialized] public bool closing;
    }

    [Section("Screens (Game)")]
    [Tooltip("The HUD, the pause, the end screen…: one each, with its role. Empty: none in this scene (the Menu)")]
    public List<ScreenEntry> screens = new List<ScreenEntry>();
    [Tooltip("Seconds the HUD takes to fade out or in")]
    public float hudFadeTime = 0.15f;

    [Section("Appear In Order", Closed = true)]
    [Tooltip("Seconds before the first part")]
    public float appearDelay = 0.05f;
    [Tooltip("Seconds between one part and the next, however many there are")]
    public float appearInterval = 0.15f;
    [Tooltip("Seconds each part takes to pop in or out; a single window too (opening and closing: the Menu's)")]
    public float appearPopTime = 0.25f;
    [Tooltip("A click or a key meanwhile: the rest appear at once")]
    public bool appearSkipOnInput = true;

    // Before the list (moved into it once: OnValidate / Awake)
    [SerializeField, HideInInspector, FormerlySerializedAs("hud")] private CanvasGroup oldHud;
    [SerializeField, HideInInspector, FormerlySerializedAs("pauseScreen")] private GameObject oldPause;
    [SerializeField, HideInInspector, FormerlySerializedAs("endScreen")] private GameObject oldEnd;

    // The screen with this role (null: none)
    GameObject ScreenOf(ScreenRole role)
    {
        foreach (ScreenEntry s in screens) if (s != null && s.role == role && s.target != null) return s.target;
        return null;
    }
    public GameObject pauseScreen => ScreenOf(ScreenRole.Pause);
    public GameObject endScreen => ScreenOf(ScreenRole.End);
    private CanvasGroup hudGroup;
    public CanvasGroup hud
    {
        get
        {
            if (hudGroup != null) return hudGroup;
            GameObject g = ScreenOf(ScreenRole.Hud);
            if (g == null) return null;
            hudGroup = g.GetComponent<CanvasGroup>();
            if (hudGroup == null) hudGroup = g.AddComponent<CanvasGroup>();
            return hudGroup;
        }
    }

    // The three old fields into the list (once)
    void MoveOldScreens()
    {
        if (oldHud == null && oldPause == null && oldEnd == null) return;
        if (screens.Count == 0)
        {
            if (oldHud != null) screens.Add(new ScreenEntry { target = oldHud.gameObject, role = ScreenRole.Hud });
            if (oldPause != null) screens.Add(new ScreenEntry { target = oldPause, role = ScreenRole.Pause });
            if (oldEnd != null) screens.Add(new ScreenEntry { target = oldEnd, role = ScreenRole.End });
        }
        oldHud = null; oldPause = null; oldEnd = null;
    }
#if UNITY_EDITOR
    void OnValidate() => MoveOldScreens();
#endif

    [Section("Loading", Closed = true)]
    [Tooltip("Minimum seconds a load takes (busy cursor, music fading out), even if the scene is ready before")]
    public float minLoadingTime = 1.5f;
    [Tooltip("Seconds it \"thinks\" before closing the game (the music fades out meanwhile; then AudioManager's Shutdown Sound)")]
    public float quitDelay = 2f;

    private bool loading;
    private string pendingScene;   // the game the tutorial popup is asking about

    // The game is paused (the pause Screen is open): time frozen, the player still
    public static bool GameIsPaused;

    private float hudShown = 1f;
    private PlayerLook look;
    private PlayerLook Look => look != null ? look : (look = FindObjectOfType<PlayerLook>());
    bool EndOpen => endScreen != null && endScreen.activeInHierarchy;

    void Awake()
    {
        MoveOldScreens();
        Arrange();
    }

    // Everything as it should be at the start, whatever was left on or off in the scene
    void Arrange()
    {
        // On (and their parents): what has to work from the first frame; they hide themselves if they should
        if (hud != null) Reveal(hud.gameObject);
        foreach (HudWindow window in FindObjectsOfType<HudWindow>(true)) Reveal(window.gameObject);       // (the meme window on the HUD)
        foreach (VirusClock clock in FindObjectsOfType<VirusClock>(true)) Reveal(clock.gameObject);       // (hidden until the countdown)
        foreach (EndScreen end in FindObjectsOfType<EndScreen>(true)) Reveal(end.gameObject);             // (it keeps its screen hidden)
        foreach (RetroCursor cursor in FindObjectsOfType<RetroCursor>(true)) Reveal(cursor.gameObject);

        // Off (their parents on): what waits to be opened
        foreach (ScreenEntry sc in screens) if (sc != null && sc.target != null && sc.role != ScreenRole.Hud) Close(sc.target);
        Close(tutorialPopup);
        foreach (UIWindow window in FindObjectsOfType<UIWindow>(true)) if (window.opensWithButton) window.gameObject.SetActive(false);
    }

    // A screen opening: its parts on, but the ones a button opens (closed)
    static void Prepare(ScreenEntry sc)
    {
        foreach (Transform part in sc.Parts)
        {
            bool byButton = part.TryGetComponent(out UIWindow w) && w.opensWithButton;
            if (part.gameObject.activeSelf == byButton) part.gameObject.SetActive(!byButton);
        }
    }

    // It and every parent on
    static void Reveal(GameObject g)
    {
        if (g == null) return;
        for (Transform t = g.transform; t != null; t = t.parent) if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
    }

    // Off, its parents on (so opening it later shows it)
    static void Close(GameObject g)
    {
        if (g == null) return;
        if (g.transform.parent != null) Reveal(g.transform.parent.gameObject);
        g.SetActive(false);
    }

    void Start()
    {
        // A fresh scene always starts unpaused (the static flag survives scene loads)
        GameIsPaused = false;
        Time.timeScale = 1f;
        if (pauseScreen != null) pauseScreen.SetActive(false);
        if (hud != null) { hud.alpha = 1f; hud.blocksRaycasts = hud.interactable = true; }
        foreach (ScreenEntry sc in screens) if (sc != null) sc.wasOn = sc.target != null && sc.target.activeInHierarchy && !(sc.role == ScreenRole.Hud && sc.appearInOrder);   // (the HUD may appear at the start)
    }

    // A screen that just opened (whoever opened it): its parts on (Prepare), then, Appear In Order, hidden and shown one
    // by one. Here, after every Update: nothing shows a frame before. Closed halfway: its parts are left as they are
    void LateUpdate()
    {
        foreach (ScreenEntry sc in screens)
        {
            if (sc == null || sc.target == null) continue;
            bool on = sc.target.activeInHierarchy;
            if (on == sc.wasOn) continue;
            sc.wasOn = on;
            if (on && sc.role != ScreenRole.Hud) Prepare(sc);   // (the HUD's parts show and hide themselves)
            if (!sc.appearInOrder) continue;
            if (sc.appear == null) sc.appear = new ScreenAppear();
            if (sc.running != null) { StopCoroutine(sc.running); sc.running = null; }
            if (on) sc.running = StartCoroutine(sc.appear.Play(sc.Parts, sc.reverse, appearDelay, appearInterval, appearPopTime, appearSkipOnInput));
            else sc.appear.ShowAll();
        }
    }

    // A screen whose parts appear in order (they sound, not the screen)
    bool AppearsInOrder(GameObject window)
    {
        foreach (ScreenEntry sc in screens) if (sc != null && sc.target == window && sc.appearInOrder) return true;
        return false;
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
        if (pauseScreen != null) CloseWindow(pauseScreen);
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

    // The Play button inside the Recycle Bin window
    public void PlayOriginalGame() => AskTutorial(originalGameScene);

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
        if (tutorialPopup != null) CloseWindow(tutorialPopup);   // (it shrinks away while the game loads)
        GameManager.SkipFromMenu = skipTutorial;   // the game scene's GameManager reads it when it starts
        StartCoroutine(LoadScene(pendingScene));
    }

    // ---------- Game ----------

    // The end screen's Play Again: this game from the start
    public void PlayAgain()
    {
        if (loading) return;
        GameManager.SkipFromMenu = playAgainSkipsTutorial;
        CloseScreens();   // (its windows go away while it reloads)
        StartCoroutine(LoadScene(SceneManager.GetActiveScene().name));
    }

    // The pause window and the end screen (Game → Menu): their windows go away (still paused, the busy cursor) while
    // the Menu loads
    public void GoToMenu()
    {
        if (loading) return;
        CloseScreens();
        StartCoroutine(LoadScene(menuScene));
    }

    // Every open screen but the HUD closes (its windows disappear in order); the pause stays (time frozen)
    void CloseScreens()
    {
        foreach (ScreenEntry sc in screens)
            if (sc != null && sc.target != null && sc.role != ScreenRole.Hud && sc.target.activeSelf) CloseWindow(sc.target);
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
        // Still disappearing (opened again while closing): it appears again from the start
        ScreenEntry closingOne = EntryOf(window);
        if (closingOne != null && closingOne.closing)
        {
            StopCoroutine(closingOne.running);
            closingOne.closing = false;
            closingOne.running = null;
            closingOne.appear.ShowAll();
            closingOne.wasOn = false;   // (LateUpdate: prepared and shown in order again)
        }
        // Its sound (UIWindow: Window or Popup; without one, Window). A screen whose parts appear in order: they sound
        bool wasOff = !window.activeSelf || Popping(window, false);
        if (wasOff && !AppearsInOrder(window))
            AudioManager.PlayUI(UIWindow.SoundOf(window) ?? UISound.Open);
        window.SetActive(true);
        window.transform.SetAsLastSibling();   // the one opened comes to the front
        if (wasOff && closingOne == null) PopWindow(window, true);   // (a window, not a screen: it grows in place)
    }

    // A window growing in as it opens, shrinking out as it closes (WindowPop); asked again halfway, the new one wins
    private readonly WindowPop windowPop = new WindowPop();
    private readonly Dictionary<GameObject, Coroutine> windowPops = new Dictionary<GameObject, Coroutine>();
    private readonly HashSet<GameObject> windowsClosing = new HashSet<GameObject>();

    void PopWindow(GameObject window, bool open)
    {
        if (windowPops.TryGetValue(window, out Coroutine running) && running != null) { StopCoroutine(running); windowPop.Restore(window.transform); }
        if (open) windowsClosing.Remove(window); else windowsClosing.Add(window);
        windowPops[window] = StartCoroutine(PopRoutine(window, open));
    }

    IEnumerator PopRoutine(GameObject window, bool open)
    {
        yield return windowPop.Run(window.transform, open, appearPopTime);
        windowPops.Remove(window);
        windowsClosing.Remove(window);
    }

    // Shrinking out now (closing: true) or growing in (false)
    bool Popping(GameObject window, bool growing) => window.activeSelf && windowsClosing.Contains(window) != growing;

    public void CloseWindow(GameObject window)
    {
        if (!window.activeSelf) return;
        AudioManager.PlayUI(UISound.Close);
        // A screen whose parts appear in order: they disappear the other way round first, then it closes
        ScreenEntry sc = EntryOf(window);
        if (sc != null && sc.appearInOrder && sc.appear != null && !sc.closing)
        {
            if (sc.running != null) StopCoroutine(sc.running);
            sc.closing = true;
            sc.running = StartCoroutine(CloseInOrder(sc));
            return;
        }
        if (sc != null && sc.closing) return;   // (already disappearing)
        if (sc == null) { if (!windowsClosing.Contains(window)) PopWindow(window, false); return; }   // (a window: it shrinks, then turns off)
        window.SetActive(false);
    }

    IEnumerator CloseInOrder(ScreenEntry sc)
    {
        yield return sc.appear.Disappear(appearInterval, appearPopTime);
        sc.closing = false;
        sc.running = null;
        sc.target.SetActive(false);   // (LateUpdate leaves its parts as they are for next time)
    }

    ScreenEntry EntryOf(GameObject window)
    {
        foreach (ScreenEntry sc in screens) if (sc != null && sc.target == window) return sc;
        return null;
    }

    // ---------- Inside ----------

    IEnumerator Quit()
    {
        loading = true;

        // As when loading: busy cursor, nothing clickable and the music fades out; then the shutdown sound, to its end
        if (EventSystem.current != null) EventSystem.current.enabled = false;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetBusy(true);
        if (AudioManager.Instance != null) AudioManager.Instance.FadeOutMusic(quitDelay);

        yield return new WaitForSecondsRealtime(quitDelay);
        float shutdown = AudioManager.Instance != null ? AudioManager.Instance.PlayShutdown() : 0f;
        if (shutdown > 0f) yield return new WaitForSecondsRealtime(shutdown);

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
