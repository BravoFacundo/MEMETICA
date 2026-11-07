using UnityEngine;
using UnityEngine.Events;

// The run, in every game scene: how it starts (GameManager.Intro: the fade in, skipping the tutorial), the virus
// countdown, winning and losing, and how it ends (GameManager.Ending: the End Screen, or rising into the sky).
// Game: the countdown. Your first meme also downloads a virus (a meme itself): from
// then on (Start On: the hub's pedestal going down after your first raffle) you have Minutes until it throws the blue screen. Collect every meme in the MemeDex and back it up
// before that to win.
//   Lose  time is up: the blue screen (Blue Screen) for a moment, then the End Screen (On Lose)
//   Win   the backup finished (Win(): the MemeDex calls it once it exists; DebugTools F9 meanwhile): the End Screen
// End Screen is one window for both: its Win Only / Lose Only parts (titles, texts, pictures) show for that result.
// Without an End Screen: the old separate Blue Screen / Win Screen.
// When it ends the player stops (only the cursor moves, for the screens). The HUD shows the time with a VirusClock.
// OriginalGame: Minutes 0 (no countdown) and Ending Ascend (its last pedestals win: Completes Game).
// Goes in Managers > GameManager (Game and OriginalGame). A scene without one gets one made by the LevelManager.
public partial class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum State { NotStarted, Running, Won, Lost }
    public enum StartOn { Manual = 1, PedestalDown = 2, VirusAlert = 3 }   // (numbers kept: scenes store them)

    [Section("Time")]
    [Tooltip("Minutes until the blue screen (the itch.io build: 30; the exhibition: 5). 0: no countdown (OriginalGame)")]
    public float minutes = 5f;
    public enum ScoreMode { Expo, Itch }
    [Tooltip("Which build this is, saved with each score: runs of different builds aren't ranked together")]
    public ScoreMode scoreMode = ScoreMode.Expo;

    [Section("Start")]
    [Tooltip("Virus Alert: once the first raffle's way rose, the Virus Alert window shows (you stay still) and the " +
             "countdown starts when it closes. Pedestal Down: when Start Pedestal goes into the ground (the hub's " +
             "randomizer, after your first raffle). Manual: when something calls StartTimer")]
    public StartOn startOn = StartOn.PedestalDown;
    [Tooltip("The pedestal (its PedestalRetire) whose first trip into the ground starts the countdown")]
    [ShowIf(nameof(startOn), StartOn.PedestalDown)] public PedestalRetire startPedestal;
    public UnityEvent onStart;

    [Section("End Screen (win and lose)")]
    [Tooltip("One window for both results (on a canvas over everything). Empty: the old Blue Screen / Win Screen")]
    public GameObject endScreen;
    [Tooltip("Parts of it shown only when you win (e.g. its 'Backup complete' title)")]
    public GameObject[] winOnly;
    [Tooltip("Parts of it shown only when you lose (e.g. its 'Your memes are gone' title)")]
    public GameObject[] loseOnly;

    [Section("Virus Alert (Start On: Virus Alert)")]
    [Tooltip("The window 'Virus detected' (on a canvas, outside the HUD group): hidden until then. Set, it is what " +
             "starts the countdown, whatever Start On says")]
    public GameObject virusAlert;
    [Tooltip("Optional: a text of it with {min} where the minutes go ('...se borrará en {min} minutos'): it writes Minutes")]
    public TMPro.TMP_Text virusMinutesText;
    [Tooltip("Seconds after the way started rising before it shows")]
    public float alertDelay = 1f;
    [Tooltip("Seconds it stays at least: the game stops until it is clicked after that (PopupManager modal: 'Click to start')")]
    public float alertMinTime = 1.5f;
    public AudioClip alertClip;

    [Section("Lose (blue screen)")]
    [Tooltip("Shown when time is up (your blue screen, on a canvas over everything). With an End Screen: only for " +
             "Blue Screen Seconds, then the End Screen")]
    public GameObject blueScreen;
    [Tooltip("With an End Screen: seconds the blue screen stays before it")]
    public float blueScreenSeconds = 3f;
    public AudioClip loseClip;
    public UnityEvent onLose;

    [Section("Win (backup)")]
    [Tooltip("Shown when the backup finished")]
    public GameObject winScreen;
    public AudioClip winClip;
    public UnityEvent onWin;

    public State Current { get; private set; } = State.NotStarted;
    public float TimeLeft { get; private set; }
    public float Elapsed { get; private set; }   // seconds since the countdown started (for the score)
    public bool Running => Current == State.Running;
    public bool Ended => Current == State.Won || Current == State.Lost;

    // Something changed state (the clock, the screens)
    public static event System.Action<State> StateChanged;

    private PlayerMove player;
    private AudioSource source;

    void Awake()
    {
        Instance = this;
        DecideIntro();   // before the elevators' Start (the intro one doesn't ride when skipping)
        TimeLeft = minutes * 60f;
        if (blueScreen != null) blueScreen.SetActive(false);
        if (winScreen != null) winScreen.SetActive(false);
        if (endScreen != null) endScreen.SetActive(false);
        if (virusAlert != null) virusAlert.SetActive(false);
    }

    void OnEnable() => RandomizerPedestal.WayOpened += OnWayOpened;
    void OnDisable() => RandomizerPedestal.WayOpened -= OnWayOpened;

    //---- Virus Alert: the first way rose → the window, you stay still → closed: the countdown

    private bool alerting;
    void OnWayOpened()
    {
        if ((startOn != StartOn.VirusAlert && virusAlert == null) || Current != State.NotStarted || alerting) return;
        StartCoroutine(VirusAlert());
    }

    System.Collections.IEnumerator VirusAlert()
    {
        alerting = true;
        yield return new WaitForSeconds(alertDelay);
        if (virusMinutesText != null) virusMinutesText.text = virusMinutesText.text.Replace("{min}", Mathf.RoundToInt(minutes).ToString());
        PlayClip(alertClip);
        bool closed = false;
        PopupManager.ShowModal(virusAlert, () => closed = true, alertMinTime);   // the game stops until clicked
        StartTimer();   // the clock shows with it, stopped (time is frozen): it runs once clicked
        while (!closed) yield return null;
    }

    void Start()
    {
        player = FindObjectOfType<PlayerMove>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        BeginIntro();
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; EndIntro(); }
    }

    void Update()
    {
        if (Current == State.NotStarted && ShouldStart()) StartTimer();
        if (Current != State.Running || UIManager.GameIsPaused) return;
        TimeLeft -= Time.deltaTime;
        Elapsed += Time.deltaTime;
        if (TimeLeft <= 0f) { TimeLeft = 0f; Lose(); }
    }

    bool ShouldStart()
    {
        if (minutes <= 0f) return false;   // no countdown (OriginalGame)
        if (virusAlert != null) return false;   // a Virus Alert set: it starts the countdown (whatever Start On says)
        if (startOn == StartOn.PedestalDown) return startPedestal != null && !startPedestal.IsUp;
        return false;
    }

    //---- The run

    public void StartTimer()
    {
        if (Current != State.NotStarted) return;
        TimeLeft = minutes * 60f;
        Elapsed = 0f;
        SetState(State.Running);
        onStart.Invoke();
    }

    public void Lose()
    {
        if (Current == State.Won || Current == State.Lost) return;
        TimeLeft = 0f;
        End(State.Lost, blueScreen, loseClip);
        onLose.Invoke();
    }

    public void Win()
    {
        if (Current == State.Won || Current == State.Lost) return;
        if (ending == Ending.Ascend) { SetState(State.Won); PlayClip(winClip); StartCoroutine(Ascend()); }
        else End(State.Won, winScreen, winClip);
        onWin.Invoke();
    }

    // Debug (DebugTools F11): only a few seconds left
    public void SkipTo(float secondsLeft)
    {
        if (Current == State.NotStarted) StartTimer();
        if (Current == State.Running) TimeLeft = Mathf.Min(TimeLeft, secondsLeft);
    }

    void End(State state, GameObject screen, AudioClip clip)
    {
        SetState(state);
        if (player != null) player.InputLocked = true;
        PlayerLook look = PlayerLook.Instance;
        if (look != null) look.LookLocked = true;   // only the cursor moves (RetroCursor: free once it ended), for the screens
        PlayClip(clip);
        if (endScreen == null) { if (screen != null) screen.SetActive(true); return; }   // (the old separate screens)

        bool won = state == State.Won;
        foreach (GameObject part in winOnly) if (part != null) part.SetActive(won);
        foreach (GameObject part in loseOnly) if (part != null) part.SetActive(!won);
        if (!won && blueScreen != null) StartCoroutine(BlueScreenThenEnd());
        else endScreen.SetActive(true);
    }

    // Lose: the blue screen a moment, then the End Screen over it
    System.Collections.IEnumerator BlueScreenThenEnd()
    {
        blueScreen.SetActive(true);
        yield return new WaitForSecondsRealtime(blueScreenSeconds);
        endScreen.SetActive(true);
        endScreen.transform.SetAsLastSibling();
    }

    void PlayClip(AudioClip clip)
    {
        if (clip != null && source != null) source.PlayOneShot(clip);
    }

    void SetState(State state)
    {
        Current = state;
        StateChanged?.Invoke(state);
    }

    // Time as the clock shows it: 04:59
    public static string Format(float seconds)
    {
        int s = Mathf.CeilToInt(Mathf.Max(seconds, 0f));
        return $"{s / 60:00}:{s % 60:00}";
    }
}
