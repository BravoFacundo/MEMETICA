using UnityEngine;
using UnityEngine.SceneManagement;

// Debug keys to test faster. Goes in Managers > DebugTools (Game and Game_Old).
//   F1  help on screen (keys and the selected start)
//   F2  noclip: fly through everything, no gravity. WASD where you look, Space up, Ctrl down, Shift fast
//   F3  hide all the UI and the crosshair (screenshots)
//   F4 / F6  previous / next start   |   F5  restart the scene at the selected start
//   F7  fast elevators: every ride goes Fast Elevator Speed times faster
//   F8  the meme in your hand changes as if you had carried it onto another island (the next Move)
// All Islands Up: every island is already up when the game starts (to walk the whole level)
// Starts: the player appears at Spawn Point with islands and bridges already up, elevators already docked
// and memes already on their pedestals, as if that part had been played. Start At picks one when pressing Play.
// Off in builds unless Enabled In Build is on (the exhibition build stays clean).
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
        [Tooltip("Island IDs already up (e.g. 1, 2)")]
        public string[] islands;
        [Tooltip("Bridge IDs already up (e.g. A, 02)")]
        public string[] bridges;
        [Tooltip("Elevators already docked at their island (the intro one too, so it doesn't ride)")]
        public Elevator[] dockedElevators;
        [Tooltip("Memes already placed on a pedestal (the pedestal evaluates them as usual)")]
        public MemePlacement[] memes;
    }

    [Header("Configuration")]
    public bool enabledInBuild = false;
    [Tooltip("Start used when pressing Play. -1 = normal game")]
    public int startAt = -1;

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

    [Header("Starts")]
    public DebugStart[] starts;

    private static int pendingStart = -1;   // survives the F5 reload

    // How much faster elevators ride (1 = normal; DebugTools off in builds: always 1)
    public static float ElevatorSpeed { get; private set; } = 1f;
    // Islands read it in their Start (after this Awake). DebugTools off in builds: always false
    public static bool AllIslandsUp { get; private set; }

    private PlayerMove playerMove;
    private CharacterController characterController;
    private Transform cameraTransform;
    private Canvas[] hiddenCanvases;
    private int selectedStart;
    private bool showHelp;
    private bool noclip;

    void Awake()
    {
        if (!Application.isEditor && !Debug.isDebugBuild && !enabledInBuild) { enabled = false; return; }
        AllIslandsUp = allIslandsUp;
        playerMove = FindObjectOfType<PlayerMove>();
        if (playerMove != null) characterController = playerMove.GetComponent<CharacterController>();
        if (Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void OnDisable()
    {
        ElevatorSpeed = 1f;
        AllIslandsUp = false;
    }

    void Start()
    {
        int index = pendingStart >= 0 ? pendingStart : startAt;
        pendingStart = -1;
        selectedStart = Mathf.Max(index, 0);
        if (index >= 0 && index < starts.Length) StartCoroutine(ApplyStart(starts[index]));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) showHelp = !showHelp;
        if (Input.GetKeyDown(KeyCode.F2)) SetNoclip(!noclip);
        if (Input.GetKeyDown(KeyCode.F3)) ToggleUI();
        if (Input.GetKeyDown(KeyCode.F4)) SelectStart(-1);
        if (Input.GetKeyDown(KeyCode.F6)) SelectStart(1);
        if (Input.GetKeyDown(KeyCode.F5)) RestartAtSelectedStart();
        if (Input.GetKeyDown(KeyCode.F7)) { fastElevators = !fastElevators; showHelp = true; }
        if (Input.GetKeyDown(KeyCode.F8)) ForceMemeChange();
        ElevatorSpeed = fastElevators ? fastElevatorSpeed : 1f;

        if (noclip && !PauseMenu.GameIsPaused) Fly();
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
            hiddenCanvases = System.Array.FindAll(FindObjectsOfType<Canvas>(), c => c.isRootCanvas && c.enabled);
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
        showHelp = true;
    }

    void RestartAtSelectedStart()
    {
        if (starts.Length == 0) return;
        pendingStart = selectedStart;
        Time.timeScale = 1f;
        PauseMenu.GameIsPaused = false;
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
        foreach (string id in start.islands) LevelManager.Instance.SnapIslandUp(id);
        foreach (string id in start.bridges) LevelManager.Instance.SnapBridgeUp(id);
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
            islands = new string[0],
            bridges = new string[0],
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
        else Debug.Log("[DebugTools] F8: pick up a meme first.");
    }

    //---- Help on screen

    void OnGUI()
    {
        if (!showHelp) return;
        string startName = starts.Length > 0 ? (selectedStart + 1) + "/" + starts.Length + "  " + starts[selectedStart].name : "none (add them in DebugTools)";
        string text =
            "DEBUG\n" +
            "F1  this help\n" +
            "F2  noclip " + (noclip ? "ON" : "off") + "  (WASD, Space up, Ctrl down, Shift fast)\n" +
            "F3  hide UI " + (hiddenCanvases != null ? "ON" : "off") + "\n" +
            "F4 / F6  start: " + startName + "\n" +
            "F5  restart at that start\n" +
            "F7  fast elevators " + (fastElevators ? "ON (x" + fastElevatorSpeed + ")" : "off") + "\n" +
            "F8  the meme in your hand changes (next Move)";
        GUI.Box(new Rect(10, 10, 420, 146), "");
        GUI.Label(new Rect(20, 15, 400, 136), text);
    }
}
