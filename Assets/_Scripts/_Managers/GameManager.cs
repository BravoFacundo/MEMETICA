using UnityEngine;
using UnityEngine.Events;

// The run (Game): the virus countdown and how it ends. Your first meme also downloads a virus (a meme itself): from
// then on (Start On: the hub's pedestal going down after your first raffle) you have Minutes until it throws the blue screen. Collect every meme in the MemeDex and back it up
// before that to win.
//   Lose  time is up: the blue screen (Blue Screen) for a moment, then the End Screen (On Lose)
//   Win   the backup finished (Win(): the MemeDex calls it once it exists; DebugTools F9 meanwhile): the End Screen
// End Screen is one window for both: its Win Only / Lose Only parts (titles, texts, pictures) show for that result.
// Without an End Screen: the old separate Blue Screen / Win Screen.
// When it ends the player stops (only the cursor moves, for the screens). The HUD shows the time with a VirusClock.
// Goes in Managers > GameManager. Without it (Game_Old) there is no countdown.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum State { NotStarted, Running, Won, Lost }
    public enum StartOn { IslandReached, Manual, PedestalDown }

    [Section("Time")]
    [Tooltip("Minutes until the blue screen (the itch.io build: 30; the exhibition: 5)")]
    public float minutes = 5f;
    public enum ScoreMode { Expo, Itch }
    [Tooltip("Which build this is, saved with each score: runs of different builds aren't ranked together")]
    public ScoreMode scoreMode = ScoreMode.Expo;

    [Section("Start")]
    [Tooltip("Pedestal Down: when Start Pedestal goes into the ground (the hub's randomizer, after your first raffle). " +
             "Island Reached: when you first step on Start Island. Manual: when something calls StartTimer")]
    public StartOn startOn = StartOn.PedestalDown;
    [Tooltip("Its ID (1 → Island_1)")]
    [ShowIf(nameof(startOn), StartOn.IslandReached)] public string startIsland = "1";
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
        TimeLeft = minutes * 60f;
        if (blueScreen != null) blueScreen.SetActive(false);
        if (winScreen != null) winScreen.SetActive(false);
        if (endScreen != null) endScreen.SetActive(false);
    }

    void Start()
    {
        player = FindObjectOfType<PlayerMove>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }

    void Update()
    {
        if (Current == State.NotStarted && ShouldStart()) StartTimer();
        if (Current != State.Running || PauseMenu.GameIsPaused) return;
        TimeLeft -= Time.deltaTime;
        Elapsed += Time.deltaTime;
        if (TimeLeft <= 0f) { TimeLeft = 0f; Lose(); }
    }

    bool ShouldStart()
    {
        if (startOn == StartOn.IslandReached) return OnStartIsland();
        if (startOn == StartOn.PedestalDown) return startPedestal != null && !startPedestal.IsUp;
        return false;
    }

    bool OnStartIsland()
    {
        if (player == null || player.CurrentIsland == null || LevelManager.Instance == null) return false;
        Transform start = LevelManager.Instance.FindIsland(startIsland);
        Island here = player.CurrentIsland.GetComponentInParent<Island>();
        return start != null && here != null && here.transform == start;
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
        End(State.Won, winScreen, winClip);
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
        PlayerLook look = FindObjectOfType<PlayerLook>();
        if (look != null) look.LookLocked = true;   // only the cursor moves (RetroCursor: free once it ended), for the screens
        if (clip != null) source.PlayOneShot(clip);
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
