using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Debug keys and debug starts, to test faster. Goes in Managers > DebugTools (Game and OriginalGame).
// Skip Tutorial: pressing Play in the scene starts at the hub (the GameManager does it; from the menu its popup decides).
//   F1  the console (DebugConsole): these keys, their state and the selected start (they work with it hidden too)
//   F2  noclip: fly through everything, no gravity. WASD where you look, Space up, Ctrl down, Shift fast
//   F3  hide all the UI and the crosshair (screenshots)
//   F4 / F6  previous / next start   |   F5  restart the scene at the selected start
//   F7  fast elevators: every ride goes Fast Elevator Speed times faster
//   F8  the meme in your hand changes as if you had carried it onto another island (the next Move)
//   F9 / F10  win / lose now (GameManager)   |   F11  the countdown starts (if it hadn't) with 10 seconds left
//   F12 the MemeDex so far (in unlock order, then what is missing) to the Console
//   8   (the run ended) saves it as a score named TEST001, TEST002… (until the name screen exists)
//   (0 locks and 9 switches English / Spanish: the console's own, so they also work in the Menu)
// All Islands Up: every island is already up when the game starts (to walk the whole level)
// Log Meme Sounds: every sound a meme plays goes to the Console ([Meme sound] who: clip (why) frame), to find repeated ones
// Starts: the player appears at Spawn Point with islands and bridges already up, elevators already docked
// and memes already on their pedestals, as if that part had been played. Start At picks one when pressing Play.
// Editor and Development Builds: every key. Normal build (exhibition): only the keys in Build Keys,
// and the rest (Start At, All Islands Up, Fast Elevators, Log Meme Sounds) stays off. Build Keys empty: DebugTools off.
// Simulate Build: in the Editor, act like the normal build (to try Build Keys before building).
// Every key, in the Editor too, only works once unlocked (F1 asks for DebugLock's password) and not while the console
// asks for a value.
[DefaultExecutionOrder(-50)]   // before the elevators, so a start can dock them before the intro ride begins
public class DebugTools : MonoBehaviour
{
    [System.Serializable]
    public class MemePlacement
    {
        public Meme meme;
        public Pedestal pedestal;
    }

    [System.Serializable]
    public class DebugStart
    {
        public string name = "Island 1";
        [Tooltip("Where the player appears (e.g. the RespawnPoint of an island)")]
        public Transform spawnPoint;
        [Tooltip("Islands and bridges already up, no animation (drag them in)")]
        public Lift[] up = new Lift[0];
        [Tooltip("Elevators already docked at their island (the intro one too, so it doesn't ride)")]
        public Elevator[] dockedElevators;
        [Tooltip("Memes already placed on a pedestal (the pedestal evaluates them as usual)")]
        public MemePlacement[] memes;
    }

    // Every debug key, to choose which ones work in the exhibition build
    [System.Flags]
    public enum DebugKey
    {
        None = 0,
        Help = 1 << 0,            // (unused: F1 is the console's, always there)
        Noclip = 1 << 1,          // F2
        HideUI = 1 << 2,          // F3
        Starts = 1 << 3,          // F4 / F6 / F5
        FastElevators = 1 << 4,   // F7
        MemeChange = 1 << 5,      // F8
        WinLose = 1 << 6,         // F9 / F10
        TenSecondsLeft = 1 << 7,  // F11
        MemeDexLog = 1 << 8,      // F12
        TestScore = 1 << 9,       // 8
    }

    [Header("Configuration")]
    [Tooltip("Keys that still work in a normal (non-development) build. In the Editor and Development Builds every key works. None: DebugTools is off in the build")]
    public DebugKey buildKeys = DebugKey.None;
    [Tooltip("In the Editor, act like the normal build: only Build Keys work and the rest of DebugTools is off")]
    public bool simulateBuild = false;
    [Tooltip("Start used when pressing Play. -1 = normal game")]
    public int startAt = -1;
    [Tooltip("Pressing Play in this scene starts at the hub, without the tutorial (GameManager: Skip Tutorial). Opened " +
             "from the menu, its popup decides. A start (Start At) wins over it")]
    public bool skipTutorial = false;

    [Header("Noclip")]
    public float flySpeed = 10f;
    public float flyFastSpeed = 30f;

    [Header("Elevators")]
    [Tooltip("Rides go faster (F7 toggles it in play)")]
    public bool fastElevators = false;
    [Range(1f, 10f)] public float fastElevatorSpeed = 5f;

    [Header("Islands")]
    [Tooltip("Every island already up when the game starts")]
    public bool allIslandsUp = false;

    [Header("Memes")]
    [Tooltip("Every sound a meme plays goes to the Console, with why: [Meme sound] (can be switched while playing)")]
    public bool logMemeSounds = false;

    [Header("Starts")]
    public DebugStart[] starts;

    private static int pendingStart = -1;   // survives the F5 reload

    // How much faster elevators ride (1 = normal; DebugTools off in builds: always 1)
    public static float ElevatorSpeed { get; private set; } = 1f;
    // Islands read it in their Start (after this Awake). DebugTools off in builds: always false
    public static bool AllIslandsUp { get; private set; }
    // The GameManager reads it in its Awake (after this one). DebugTools off in builds: always false
    public static bool SkipTutorial { get; private set; }
    // MemeChanger reads it (DebugTools off in builds: always false)
    public static bool LogMemeSounds { get; private set; }

    private PlayerMove playerMove;
    private CharacterController characterController;
    private Transform cameraTransform;
    private Canvas[] hiddenCanvases;
    private int selectedStart;
    private bool noclip;
    private bool fullAccess;   // Editor / Development Build: every key and option

    void Awake()
    {
        fullAccess = (Application.isEditor && !simulateBuild) || (!Application.isEditor && Debug.isDebugBuild);
        if (!fullAccess)
        {
            // Normal build: nothing changes the game by itself, only the chosen keys
            startAt = -1;
            skipTutorial = false;
            allIslandsUp = false;
            fastElevators = false;
            logMemeSounds = false;
            if (buildKeys == DebugKey.None) { enabled = false; return; }
        }
        AllIslandsUp = allIslandsUp;
        SkipTutorial = skipTutorial;
        LogMemeSounds = logMemeSounds;
        playerMove = FindObjectOfType<PlayerMove>();
        if (playerMove != null) characterController = playerMove.GetComponent<CharacterController>();
        if (Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void OnEnable() => DebugConsole.AddSection(this, "DEBUG  (" + (fullAccess ? "every key" : "build: only the * keys") + ")", HelpLines, 10);

    void OnDisable()
    {
        DebugConsole.RemoveSections(this);
        ElevatorSpeed = 1f;
        AllIslandsUp = false;
        SkipTutorial = false;
    }

    void Start()
    {
        int index = pendingStart >= 0 ? pendingStart : GameManager.FromMenu ? -1 : startAt;   // from the menu: its tutorial popup decides
        pendingStart = -1;
        selectedStart = Mathf.Max(index, 0);
        if (index >= 0 && index < starts.Length) StartCoroutine(ApplyStart(starts[index]));
        if (AllIslandsUp) StartCoroutine(AllBridgesUp());
    }

    // Is this key on now? (every key in the Editor / Development Build; in a normal build, only Build Keys, and only unlocked)
    bool Allowed(DebugKey key) => DebugLock.Unlocked && (fullAccess || (buildKeys & key) != 0);

    bool Pressed(KeyCode keyCode, DebugKey key) => Allowed(key) && DebugConsole.Pressed(keyCode);   // (its console line lights up)

    // All Islands Up: the bridges too (and their stones), once everything started
    System.Collections.IEnumerator AllBridgesUp()
    {
        yield return null;
        foreach (Bridge bridge in FindObjectsOfType<Bridge>()) bridge.SnapUp();
    }

    void Update()
    {
        LogMemeSounds = logMemeSounds;   // switchable while playing
        if (!DebugLock.Unlocked && noclip) SetNoclip(false);   // locked again: no flying
        if (Pressed(KeyCode.F2, DebugKey.Noclip)) SetNoclip(!noclip);
        if (Pressed(KeyCode.F3, DebugKey.HideUI)) ToggleUI();
        if (Pressed(KeyCode.F4, DebugKey.Starts)) SelectStart(-1);
        if (Pressed(KeyCode.F6, DebugKey.Starts)) SelectStart(1);
        if (Pressed(KeyCode.F5, DebugKey.Starts)) RestartAtSelectedStart();
        if (Pressed(KeyCode.F7, DebugKey.FastElevators)) fastElevators = !fastElevators;
        if (Pressed(KeyCode.F8, DebugKey.MemeChange)) ForceMemeChange();
        if (GameManager.Instance != null)
        {
            if (Pressed(KeyCode.F9, DebugKey.WinLose)) GameManager.Instance.Win();
            if (Pressed(KeyCode.F10, DebugKey.WinLose)) GameManager.Instance.Lose();
            if (Pressed(KeyCode.F11, DebugKey.TenSecondsLeft)) GameManager.Instance.SkipTo(10f);
        }
        if (Pressed(KeyCode.F12, DebugKey.MemeDexLog) && MemeDex.Instance != null) MemeDex.Instance.LogProgress();
        if (Allowed(DebugKey.TestScore) && DebugConsole.Pressed(8)) SaveTestRun();
        ElevatorSpeed = fastElevators ? fastElevatorSpeed : 1f;

        if (noclip && !UIManager.GameIsPaused && !DebugConsole.Typing) Fly();
    }

    //---- Noclip

    void SetNoclip(bool on)
    {
        if (playerMove == null) return;
        noclip = on;
        playerMove.enabled = !on;              // no walking, gravity or fall respawn while flying
        characterController.enabled = !on;     // no collisions
        if (!on) playerMove.ResetMotion();     // lands from where it is, without old speed
        if (RetroCursor.Instance != null) RetroCursor.Instance.cursorImage.enabled = !on;   // no crosshair while flying
    }

    void Fly()
    {
        Vector3 direction = cameraTransform.forward * Input.GetAxisRaw("Vertical") + cameraTransform.right * Input.GetAxisRaw("Horizontal");
        if (Input.GetKey(KeyCode.Space)) direction += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl)) direction += Vector3.down;
        float speed = Input.GetKey(KeyCode.LeftShift) ? flyFastSpeed : flySpeed;
        playerMove.transform.position += Vector3.ClampMagnitude(direction, 1f) * speed * Time.unscaledDeltaTime;
    }

    //---- Hide UI

    void ToggleUI()
    {
        if (hiddenCanvases == null)
        {
            hiddenCanvases = System.Array.FindAll(FindObjectsOfType<Canvas>(), c => c.isRootCanvas && c.enabled && !DebugConsole.IsConsoleCanvas(c));
            foreach (Canvas c in hiddenCanvases) c.enabled = false;
        }
        else
        {
            foreach (Canvas c in hiddenCanvases) if (c != null) c.enabled = true;
            hiddenCanvases = null;
        }
    }

    //---- Starts

    void SelectStart(int step)
    {
        if (starts.Length == 0) return;
        selectedStart = (selectedStart + step + starts.Length) % starts.Length;
    }

    void RestartAtSelectedStart()
    {
        if (starts.Length == 0) return;
        pendingStart = selectedStart;
        Time.timeScale = 1f;
        UIManager.GameIsPaused = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    System.Collections.IEnumerator ApplyStart(DebugStart start)
    {
        // Same frame, before the elevators start: dock them. The intro elevator always docks
        // (a start skips the intro; otherwise it boards the player and carries it away)
        foreach (Elevator elevator in FindObjectsOfType<Elevator>()) if (elevator.rideOnStart) elevator.DockInstantly();
        foreach (Elevator elevator in start.dockedElevators) if (elevator != null) elevator.DockInstantly();

        // Next frame, after the player, islands and bridges set themselves up: move the player, put things in place, place the memes
        yield return null;
        if (start.spawnPoint != null && playerMove != null) playerMove.TeleportTo(start.spawnPoint);
        foreach (Lift lift in start.up) if (lift != null) lift.SnapUp();
        foreach (MemePlacement placement in start.memes)
        {
            if (placement.meme == null || placement.pedestal == null) continue;
            placement.meme.transform.position = placement.pedestal.socket.position;   // the pedestal's trigger takes it from here
        }

        Debug.Log("[DebugTools] Started at '" + start.name + "'.");
    }

#if UNITY_EDITOR
    // Right click on the component > Add Skip Tutorial Start: builds the start from the scene.
    // Every elevator docked, the player at the island where the last elevator arrives,
    // and the memes that ride on an elevator placed on its pedestal (as if you had brought them).
    [ContextMenu("Add Skip Tutorial Start")]
    void AddSkipTutorialStart()
    {
        Elevator[] elevators = FindObjectsOfType<Elevator>(true);
        var memes = new System.Collections.Generic.List<MemePlacement>();
        Transform spawn = null;

        foreach (Elevator elevator in elevators)
        {
            if (!elevator.rideOnStart && elevator.dockPoint != null)
            {
                Island island = elevator.dockPoint.GetComponentInParent<Island>();
                if (island != null)
                    foreach (Transform t in island.GetComponentsInChildren<Transform>(true))
                        if (t.name == "RespawnPoint") { spawn = t; break; }
            }
            if (elevator.pedestal == null) continue;
            foreach (Transform passenger in elevator.passengers)
                if (passenger != null && passenger.TryGetComponent(out Meme meme))
                    memes.Add(new MemePlacement { meme = meme, pedestal = elevator.pedestal });
        }

        var start = new DebugStart
        {
            name = "Skip tutorial",
            spawnPoint = spawn,
            up = new Lift[0],
            dockedElevators = elevators,
            memes = memes.ToArray(),
        };

        UnityEditor.Undo.RecordObject(this, "Add Skip Tutorial Start");
        var list = new System.Collections.Generic.List<DebugStart>(starts ?? new DebugStart[0]) { start };
        starts = list.ToArray();
        startAt = starts.Length - 1;
        Debug.Log($"[DebugTools] Added '{start.name}': spawn {(spawn != null ? spawn.parent.name + "/" + spawn.name : "NONE (assign it)")}, {elevators.Length} elevators, {memes.Count} memes. Start At = {startAt}.", this);
    }
#endif

    void ForceMemeChange()
    {
        Meme held = Meme.Held;
        if (held != null) held.DebugIslandChange();
        else DebugConsole.Log("F8: pick up a meme first");
    }

    // The run ended: saved as a score named TEST001, TEST002…
    void SaveTestRun()
    {
        if (GameManager.Instance == null || !GameManager.Instance.Ended || MemeDex.Instance == null) { DebugConsole.Log("8: only once the run ended"); return; }
        ScoreBoard.Run run = MemeDex.Instance.CurrentRun(ScoreBoard.NextFree("TEST"));
        int place = ScoreBoard.Save(run);
        DebugConsole.Log($"Saved '{run.name}' ({run.mode}): {run.Count}/{run.total} memes, {run.seconds:0} s, falls {run.falls}, place {place}");
    }

    //---- In the console

    List<DebugConsole.Line> HelpLines()
    {
        string startName = starts.Length > 0 ? (selectedStart + 1) + "/" + starts.Length + "  " + starts[selectedStart].name : "none (add them in DebugTools)";
        string gameState = GameManager.Instance != null ? GameManager.Instance.Current + ", " + GameManager.Format(GameManager.Instance.TimeLeft) : "no GameManager";
        return new List<DebugConsole.Line>
        {
            Line("F2", "noclip " + (noclip ? "ON" : "off") + "  (WASD, Space up, Ctrl down, Shift fast)", DebugKey.Noclip),
            Line("F3", "hide UI " + (hiddenCanvases != null ? "ON" : "off"), DebugKey.HideUI),
            Line("F4", "previous start", DebugKey.Starts),
            Line("F6", "next start   (" + startName + ")", DebugKey.Starts),
            Line("F5", "restart at that start", DebugKey.Starts),
            Line("F7", "fast elevators " + (fastElevators ? "ON (x" + fastElevatorSpeed + ")" : "off"), DebugKey.FastElevators),
            Line("F8", "the meme in your hand changes (next Move)", DebugKey.MemeChange),
            Line("F9", "win now   (" + gameState + ")", DebugKey.WinLose),
            Line("F10", "lose now", DebugKey.WinLose),
            Line("F11", "10 s left", DebugKey.TenSecondsLeft),
            Line("F12", "MemeDex to the Console", DebugKey.MemeDexLog),
            Line("8", "save this run as TEST###  (once it ended)", DebugKey.TestScore),
        };
    }

    // A console line; * works in the normal build, off: not in this one
    DebugConsole.Line Line(string key, string text, DebugKey flag) =>
        new DebugConsole.Line(key, text + ((buildKeys & flag) != 0 ? "  *" : ""), !Allowed(flag) && DebugLock.Unlocked);
}
