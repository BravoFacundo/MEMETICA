using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

// Todo lo general del nivel. Va en Managers > LevelManager.
// - Drop Distance: one distance for heights and falls (before: Top Y / Bottom Y, Fall Distance; MasterPivot, LimiteCaida, Cage)
// - Subir y bajar islas, puentes y ascensores (antes: IslandsManager en "Scene Manager")
// Los demás scripts lo usan con LevelManager.Instance.
//
// Los pedestales piden cosas por ID y acá se buscan por nombre, con la convención Tipo_ID:
//   isla "3" → "Island_3"   |   puente "A" → "Bridge_A"   |   isla "0" → fin del juego
//   elevators: "Tutorial_02" → "Elevator_Tutorial_02" (an island ID that matches an elevator starts its ride)
// Leading zeros in numbers are ignored: "2", "02" and "002" all find Island_2 / Island_02 (no limit on how many islands).
// Cada isla tiene un hijo "RespawnPoint" donde reaparece el jugador.
// Para sumar una isla o un puente no hace falta tocar código: alcanza con respetar la convención.
// Los prefijos se pueden cambiar en el Inspector (sección NOMBRES).
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Section("Heights")]
    [Tooltip("One distance for the whole game, about down to the clouds: islands and bridges wait this far below their place " +
             "(invisible), falling this far below the last ground is a fall into the void, and you (and memes) reappear this far " +
             "above the ground of the RespawnPoint")]
    public float dropDistance = 30f;

    [Section("Lift (islands and bridges)")]
    [Tooltip("Seconds an island or bridge takes to rise, however far it travels")]
    public float liftRiseDuration = 3f;
    [Tooltip("Seconds a bridge takes to go back down")]
    public float liftLowerDuration = 2.5f;
    [Tooltip("Meters it passes the top before settling")]
    public float liftOvershoot = 0.4f;
    [Tooltip("Pedestals with In Order: seconds between one thing arriving and the next one starting")]
    public float sequenceGap = 0.3f;

    [Section("Platforms")]
    [Tooltip("Speed of every stone's motion and fall (Slide, Hop, Blink, Fall): under 1 slower and easier, over 1 faster " +
             "and harder. It can change while playing (an admin console: LevelManager.SetPlatformSpeed)")]
    [Range(0.25f, 2f)] public float platformSpeed = 1f;

    // The stones read it every frame (1 without a LevelManager)
    public static float PlatformSpeed => Instance != null ? Mathf.Max(Instance.platformSpeed, 0.01f) : 1f;
    public static void SetPlatformSpeed(float speed) { if (Instance != null) Instance.platformSpeed = Mathf.Clamp(speed, 0.25f, 2f); }

    [Section("Debug", Closed = true)]
    [Tooltip("Scene view: a shadow of each island and bridge where it waits hidden, and the line you fall from above each RespawnPoint")]
    public bool showDropGizmos = true;

    [Section("Game")]
    [Tooltip("Where everything reappears at the start: the RespawnPoint of the tutorial island")]
    public Transform startRespawnPoint;
    [Tooltip("Objects you can turn off in the editor to see better (e.g. Clouds_HideIslands): they are turned on when the game starts")]
    public GameObject[] activateOnPlay;
    [Tooltip("Seconds the islands fall before the game closes")]
    public float endingDuration = 10f;

    [Section("Skip Tutorial")]
    [Tooltip("Start without the tutorial: inside Skip Elevator, already docked at the hub, the meme on the floor in front of " +
             "you, facing the hub's pedestal. The menu's popup decides it; this one is used when pressing Play in this scene. " +
             "A DebugTools start (Start At) wins over it")]
    public bool skipTutorial = false;
    [Tooltip("The elevator from the tutorial to the hub (Elevator_Tutorial_02). Empty: the first one that doesn't ride on start")]
    public Elevator skipElevator;
    [Tooltip("What you face (the hub's spinning pedestal). Empty: the GameManager's Start Pedestal")]
    public Transform skipLookAt;
    [Tooltip("The meme you start with (Meme Window or orb). Empty: the one riding Skip Elevator")]
    public Meme skipMeme;
    [Tooltip("Meters in front of you where it lies")]
    public float skipMemeDistance = 1.5f;
    [Tooltip("Seconds the screen takes to fade in from black (it hides the setting up)")]
    public float skipFadeTime = 1f;

    // Set by the menu's tutorial popup before loading the scene (null: this scene's Skip Tutorial)
    public static bool? SkipFromMenu;
    // This game started without the tutorial (the intro elevator reads it: no ride)
    public static bool SkippingTutorial { get; private set; }
    // This scene was opened from the menu (its popup chose): DebugTools' Start At doesn't apply
    public static bool FromMenu { get; private set; }

    [Section("Names (Type_ID)", Closed = true)]
    public string islandPrefix = "Island_";
    public string bridgePrefix = "Bridge_";
    public string elevatorPrefix = "Elevator_";
    [Tooltip("Name of the child of each island where things reappear")]
    public string respawnPointName = "RespawnPoint";

    // Se arman solas al empezar, buscando los scripts en la escena
    private readonly Dictionary<string, Island> islands = new Dictionary<string, Island>();
    private readonly Dictionary<string, Bridge> bridges = new Dictionary<string, Bridge>();
    private readonly Dictionary<string, Elevator> elevators = new Dictionary<string, Elevator>();

    void Awake()
    {
        Instance = this;   // antes de que las islas, puentes y pedestales lo usen en su Start
        foreach (GameObject o in activateOnPlay) if (o != null) o.SetActive(true);

        foreach (Island i in FindObjectsOfType<Island>()) islands[NormalizeName(i.name)] = i;
        foreach (Bridge b in FindObjectsOfType<Bridge>()) bridges[NormalizeName(b.name)] = b;
        foreach (Elevator e in FindObjectsOfType<Elevator>()) elevators[NormalizeName(e.name)] = e;

        // Before the elevators' Start (the intro one doesn't ride). Coming from the menu, its popup decides (DebugTools'
        // Start At is ignored); pressing Play in the scene, a DebugTools start wins over Skip Tutorial
        FromMenu = SkipFromMenu.HasValue;
        SkippingTutorial = SkipFromMenu ?? skipTutorial;
        SkipFromMenu = null;
        DebugTools debug = FindObjectOfType<DebugTools>();
        if (!FromMenu && debug != null && debug.enabled && debug.startAt >= 0) SkippingTutorial = false;
    }

    void Start()
    {
        if (SkippingTutorial) StartCoroutine(SkipTutorial(Blackout()));
    }

    void OnDestroy()
    {
        if (Instance == this) SkippingTutorial = FromMenu = false;
    }

    // ---------- Skip Tutorial ----------

    IEnumerator SkipTutorial(CanvasGroup fade)
    {
        yield return null;   // the player, islands, elevators and memes set themselves up first

        Elevator elevator = skipElevator != null ? skipElevator : HubElevator();
        PlayerMove player = FindObjectOfType<PlayerMove>();
        if (elevator == null || elevator.dockPoint == null || player == null)
        {
            Debug.LogWarning("[LevelManager] Skip Tutorial: no Skip Elevator (with a Dock Point) or no player: starting with the tutorial's start.", this);
            yield return FadeIn(fade);
            yield break;
        }
        Meme meme = skipMeme != null ? skipMeme : PassengerMeme(elevator);
        Pedestal holder = meme != null ? meme.CurrentPedestal : null;

        // The elevator at the hub, as if it had arrived
        elevator.DockInstantly();
        yield return null;
        Island hub = elevator.dockPoint.GetComponentInParent<Island>();
        Transform respawn = hub != null ? RespawnPointOf(hub.gameObject, elevator.transform.position) : null;

        // Its pedestal is gone from the start (the meme off it first)
        if (meme != null && holder != null && holder == elevator.pedestal)
        {
            meme.transform.SetParent(null, true);
            meme.RemoveFromPedestal(holder);
        }
        elevator.RemovePedestal();

        // You, standing on its floor, facing the hub's pedestal, looking straight ahead
        CharacterController body = player.GetComponent<CharacterController>();
        Vector3 floor = FloorUnder(elevator.transform.position + Vector3.up * 2f, player.transform, meme, out Vector3 hit) ? hit : elevator.transform.position;
        float feet = body != null ? body.height * 0.5f - body.center.y + body.skinWidth + 0.05f : 1f;
        Transform spot = new GameObject("SkipTutorialSpot").transform;
        spot.position = floor + Vector3.up * feet;
        player.TeleportTo(spot);
        if (respawn != null) { player.SetRespawn(respawn); Destroy(spot.gameObject); }

        Transform target = skipLookAt;
        if (target == null && GameManager.Instance != null && GameManager.Instance.startPedestal != null) target = GameManager.Instance.startPedestal.transform;
        Vector3 facing = target != null ? target.position - player.transform.position : elevator.transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
        facing.Normalize();
        player.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        PlayerLook look = Camera.main != null ? Camera.main.GetComponent<PlayerLook>() : null;
        if (look != null) { look.LookLevel(); look.ResetSmoothing(); }

        // The meme on the floor in front of you
        if (meme != null)
        {
            if (holder != null && holder != elevator.pedestal) holder.Break();   // (another pedestal: it lets it go next frame)
            Vector3 place = player.transform.position + facing * skipMemeDistance;
            meme.transform.SetParent(null, true);
            meme.transform.position = place;   // off its pedestal: it lets it go next frame
            yield return null;
            if (FloorUnder(place + Vector3.up * 2f, player.transform, meme, out Vector3 ground))
            {
                Collider shape = meme.GetComponentInChildren<Collider>();
                float lift = shape != null ? meme.transform.position.y - shape.bounds.min.y : 0.5f;
                place = ground + Vector3.up * (lift + 0.02f);
            }
            meme.PlaceAt(place, Quaternion.LookRotation(facing, Vector3.up), respawn);   // its front to you
        }

        yield return new WaitForSecondsRealtime(0.2f);
        yield return FadeIn(fade);
        Debug.Log($"[LevelManager] Tutorial skipped: in '{elevator.name}'" + (meme != null ? $", '{meme.name}' in front" : ", no meme") + ".", this);
    }

    // The first elevator that doesn't ride on start (the one from the tutorial to the hub)
    Elevator HubElevator()
    {
        foreach (Elevator e in FindObjectsOfType<Elevator>()) if (!e.rideOnStart && e.dockPoint != null) return e;
        return null;
    }

    // The meme riding it (its passengers, or on its pedestal)
    static Meme PassengerMeme(Elevator elevator)
    {
        foreach (Transform p in elevator.passengers) if (p != null && p.TryGetComponent(out Meme m)) return m;
        if (elevator.pedestal != null && elevator.pedestal.MemeObject != null) return elevator.pedestal.MemeObject.GetComponent<Meme>();
        return null;
    }

    // The ground under a point (not the player nor the meme)
    static bool FloorUnder(Vector3 from, Transform player, Meme meme, out Vector3 point)
    {
        point = from;
        float nearest = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(player) || (meme != null && h.transform.IsChildOf(meme.transform))) continue;
            if (h.distance < nearest) { nearest = h.distance; point = h.point; }
        }
        return nearest < float.MaxValue;
    }

    // Black over everything (it hides the setting up), then FadeIn
    static CanvasGroup Blackout()
    {
        var go = new GameObject("SkipTutorialFade", typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        go.GetComponent<Image>().color = Color.black;
        go.GetComponent<Image>().raycastTarget = false;
        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = group.interactable = false;
        return group;
    }

    IEnumerator FadeIn(CanvasGroup fade)
    {
        if (fade == null) yield break;
        for (float t = 0f; t < skipFadeTime; t += Time.unscaledDeltaTime)
        {
            fade.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / Mathf.Max(skipFadeTime, 0.01f));
            yield return null;
        }
        Destroy(fade.gameObject);
    }

    // ---------- Lo que piden los pedestales ----------

    public void SubirIsla(string id)
    {
        switch (NormalizeName(id))
        {
            case "0": StartCoroutine(GameComplete()); break;
            default:
                // Elevators share the pedestal IDs with islands: if one matches, it starts its ride
                if (elevators.TryGetValue(NormalizeName(elevatorPrefix + id), out Elevator elevator)) elevator.StartRide();
                else if (TryGet(islands, islandPrefix + id, out Island isla)) isla.Raise();
                break;
        }
    }

    public void SubirPuente(string id)
    {
        if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) puente.Raise();
    }

    // Pedestal with In Order: each island (or elevator) rises after the previous one arrived, then each bridge
    // appears after the previous one is in place. Lists in the order they should happen
    public void RaiseInOrder(string[] islandIds, string[] bridgeIds) => StartCoroutine(InOrder(islandIds, bridgeIds));

    IEnumerator InOrder(string[] islandIds, string[] bridgeIds)
    {
        foreach (string id in islandIds) yield return RaiseAndWait(id, true);
        foreach (string id in bridgeIds) yield return RaiseAndWait(id, false);
    }

    // Raises one island (or elevator) or bridge and waits until it arrived, plus the Sequence Gap (MemeRandomizer's way too)
    public IEnumerator RaiseAndWait(string id, bool island)
    {
        if (island)
        {
            SubirIsla(id);
            yield return null;
            if (elevators.TryGetValue(NormalizeName(elevatorPrefix + id), out Elevator elevator)) while (elevator.IsRiding) yield return null;
            else if (TryGet(islands, islandPrefix + id, out Island isla)) while (isla.IsMoving) yield return null;
        }
        else
        {
            SubirPuente(id);
            yield return null;
            if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) while (puente.IsAppearing || puente.IsMoving) yield return null;
        }
        yield return new WaitForSeconds(sequenceGap);
    }

    public void BajarPuente(string id)
    {
        if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) puente.Lower();
    }

    // Debug: up right away, no animation (DebugTools starts)
    public void SnapIslandUp(string id)
    {
        if (TryGet(islands, islandPrefix + id, out Island isla)) isla.SnapUp();
    }

    public void SnapBridgeUp(string id)
    {
        if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) puente.SnapUp();
    }

    // ---------- Consultas ----------

    // The island (or elevator) with that ID, or null (the hub's platform aims at it)
    public Transform FindIsland(string id)
    {
        if (elevators.TryGetValue(NormalizeName(elevatorPrefix + id), out Elevator elevator)) return elevator.transform;
        return TryGet(islands, islandPrefix + id, out Island isla) ? isla.transform : null;
    }

    // A name in a list (MemeRandomizer's Sequence): Island_09 / Elevator_X / Bridge_T1, or just its ID (09, T1) if only
    // an island or only a bridge has it. Gives the ID and which one it is; false (and a warning) if nothing has it
    public bool Resolve(string entry, out string id, out bool island)
    {
        id = entry != null ? entry.Trim() : "";
        island = true;
        if (id.Length == 0) return false;
        if (id.StartsWith(islandPrefix)) { id = id.Substring(islandPrefix.Length); return true; }
        if (id.StartsWith(elevatorPrefix)) { id = id.Substring(elevatorPrefix.Length); return true; }
        if (id.StartsWith(bridgePrefix)) { id = id.Substring(bridgePrefix.Length); island = false; return true; }

        bool isIsland = islands.ContainsKey(NormalizeName(islandPrefix + id)) || elevators.ContainsKey(NormalizeName(elevatorPrefix + id));
        bool isBridge = bridges.ContainsKey(NormalizeName(bridgePrefix + id));
        if (isIsland && isBridge) Debug.LogWarning($"[LevelManager] '{entry}' is both {islandPrefix}{id} and {bridgePrefix}{id}: using the island (write the full name).", this);
        if (!isIsland && !isBridge) { Debug.LogWarning($"[LevelManager] No existe '{entry}' en la escena (ni {islandPrefix}{id} ni {bridgePrefix}{id}).", this); return false; }
        island = isIsland;
        return true;
    }

    public Transform FindBridge(string id) => TryGet(bridges, bridgePrefix + id, out Bridge puente) ? puente.transform : null;

    // Bridges wait for this before appearing, so they connect to islands that are already there
    public bool AnyIslandMoving
    {
        get
        {
            foreach (Island isla in islands.Values) if (isla.IsMoving) return true;
            return false;
        }
    }

    // The island's respawn point (any child named RespawnPoint, at any depth), or null
    // Where the player reappears for a RespawnPoint: Drop Distance above the ground under it, falling down onto it
    public Vector3 RespawnPosition(Transform point, out float groundY)
    {
        bool hasGround = Physics.Raycast(point.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 200f, ~0, QueryTriggerInteraction.Ignore);
        groundY = hasGround ? ground.point.y : point.position.y;
        return new Vector3(point.position.x, groundY + dropDistance, point.position.z);
    }

#if UNITY_EDITOR
    // Above each RespawnPoint: the fall the player makes when reappearing
    void OnDrawGizmos()
    {
        if (!showDropGizmos) return;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        foreach (Transform t in FindObjectsOfType<Transform>())
        {
            if (t.name != respawnPointName || t.TryGetComponent(out RespawnPoint _)) continue;   // (those draw their own)
            Vector3 from = RespawnPosition(t, out float groundY);
            Gizmos.DrawLine(from, new Vector3(from.x, groundY, from.z));
            Gizmos.DrawWireSphere(from, 0.6f);
        }
    }
#endif

    // The island's respawn point nearest to a place (an island can have several: one before each part of a course), or null.
    // Once an island has a RespawnPoint (component), its old ones just named RespawnPoint are ignored
    public Transform RespawnPointOf(GameObject island, Vector3 near)
    {
        if (island == null) return null;
        Island root = island.GetComponentInParent<Island>();
        Transform container = root != null ? root.transform : island.transform;
        // Its RespawnPoints (the tool's: inside it, or kept in a level standing on it). Only if it has none: the old ones,
        // just named RespawnPoint (Game_Old)
        Transform best = null;
        float nearest = float.MaxValue;
        bool any = false;
        foreach (RespawnPoint point in RespawnPoint.All)
        {
            if (point.OwnIsland != container) continue;
            any = true;
            if (!point.Active) continue;   // kept in a level that isn't up yet
            float d = (point.transform.position - near).sqrMagnitude;
            if (d < nearest) { nearest = d; best = point.transform; }
        }
        if (any) return best;
        foreach (Transform child in container.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != respawnPointName) continue;
            float d = (child.position - near).sqrMagnitude;
            if (d < nearest) { nearest = d; best = child; }
        }
        return best;
    }

    public Transform RespawnPointOf(GameObject island)
    {
        if (island == null) return null;
        Island root = island.GetComponentInParent<Island>();
        Transform container = root != null ? root.transform : island.transform;
        foreach (Transform child in container.GetComponentsInChildren<Transform>(true))
            if (child.name == respawnPointName) return child;
        return null;
    }

    IEnumerator GameComplete()
    {
        GameObject.Find("Player").GetComponent<PlayerMove>().ascend = true;

        // Todas las islas caen (más adelante: primero los puentes, al azar, y que se desvanezcan)
        foreach (Island isla in islands.Values) isla.FallAway();

        yield return new WaitForSeconds(endingDuration);

        // Back to the desktop (Menu) instead of closing the game: the next visitor can play right away
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null) uiManager.GoToMenu();
        else FindObjectOfType<PauseMenu>().QuitGame();
    }

    // "Island_02" → "Island_2": numbers without leading zeros, so 1 and 01 are the same ID
    public static string NormalizeName(string objectName)
    {
        return Regex.Replace(objectName, @"\d+", match =>
        {
            string number = match.Value.TrimStart('0');
            return number.Length > 0 ? number : "0";
        });
    }

    // Busca y avisa en la consola si un pedestal pide algo que no existe
    bool TryGet<T>(Dictionary<string, T> table, string objectName, out T value)
    {
        if (table.TryGetValue(NormalizeName(objectName), out value)) return true;
        Debug.LogWarning("[LevelManager] No existe '" + objectName + "' en la escena (revisá el ID en el pedestal).", this);
        return false;
    }
}
