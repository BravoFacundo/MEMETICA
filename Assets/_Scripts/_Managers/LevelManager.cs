using UnityEngine;

// The level, the scenery: values every island, bridge and stone uses unless it overrides them. Goes in Managers > LevelManager.
// - Drop Distance: one distance for heights and falls. Islands and bridges are placed where they end up: hidden ones
//   wait Drop Distance below that and rise back to it (Island: Start Raised stays up)
// - Lift: rise / lower timing for every island and bridge (each one can Override Timing)
// - Platforms: the speed of every moving stone
// - Respawn: where you start, and the RespawnPoint of each island
// How the run starts and ends (fade in, skip tutorial, ending): the GameManager. Debug starts: DebugTools.
// Others use it through LevelManager.Instance. Pedestals raise islands, bridges, elevators and portals by reference.
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

    [Section("Start")]
    [Tooltip("Where everything reappears at the start: the RespawnPoint of the tutorial island")]
    public Transform startRespawnPoint;
    [Tooltip("Objects you can turn off in the editor to see better (e.g. Clouds_HideIslands): they are turned on when the game starts")]
    public GameObject[] activateOnPlay;

    [Tooltip("Old respawn points (OriginalGame): a child of the island with this name. The new ones are RespawnPoint components")]
    public string respawnPointName = "RespawnPoint";

    void Awake()
    {
        Instance = this;   // before the islands, bridges and pedestals use it in their Start
        foreach (GameObject o in activateOnPlay) if (o != null) o.SetActive(true);
    }

    // The end (a pedestal with Completes Game)
    public void CompleteGame()
    {
        if (GameManager.Instance != null) GameManager.Instance.Win();
    }

    // Bridges wait for this before appearing, so they connect to islands that are already there
    public bool AnyIslandMoving
    {
        get
        {
            foreach (Island island in Island.All) if (island.IsMoving) return true;
            return false;
        }
    }

    // Where the player reappears for a RespawnPoint: Drop Distance above the ground under it, falling down onto it
    public Vector3 RespawnPosition(Transform point, out float groundY)
    {
        bool hasGround = Physics.Raycast(point.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 200f, ~0, QueryTriggerInteraction.Ignore);
        groundY = hasGround ? ground.point.y : point.position.y;
        return new Vector3(point.position.x, groundY + dropDistance, point.position.z);
    }

#if UNITY_EDITOR
    // Above each RespawnPoint: the fall the player makes when reappearing
    private Transform[] gizmoPoints;
    private double gizmoSearchAt;
    void OnDrawGizmos()
    {
        if (!showDropGizmos) return;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        // The old ones (just named RespawnPoint), looked for again every 2 seconds, not on every repaint of the Scene view
        // (before: every object of the scene, many times a second: the Scene view went slow)
        if (gizmoPoints == null || UnityEditor.EditorApplication.timeSinceStartup > gizmoSearchAt)
        {
            gizmoSearchAt = UnityEditor.EditorApplication.timeSinceStartup + 2.0;
            gizmoPoints = System.Array.FindAll(FindObjectsOfType<Transform>(), x => x.name == respawnPointName && !x.TryGetComponent(out RespawnPoint _));   // (those draw their own)
        }
        foreach (Transform t in gizmoPoints)
        {
            if (t == null) continue;
            Vector3 from = RespawnPosition(t, out float groundY);
            Gizmos.DrawLine(from, new Vector3(from.x, groundY, from.z));
            Gizmos.DrawWireSphere(from, 0.6f);
        }
    }
#endif

    // The island's respawn point nearest to a place (an island can have several: one before each part of a course), or null.
    // Once an island has a RespawnPoint (component), its old ones just named RespawnPoint are ignored
    // An island with none at all (an elevator docked on the tutorial island): the ones of the island it is part of
    public Transform RespawnPointOf(GameObject island, Vector3 near)
    {
        if (island == null) return null;
        Island root = island.GetComponentInParent<Island>();
        Transform container = root != null ? root.transform : island.transform;
        for (int i = 0; container != null && i < 4; i++)
        {
            Transform found = RespawnPointIn(container, near, out bool has);
            if (has) return found;
            container = OuterIsland(container);
        }
        return null;
    }

    // The island (Island or Elevator) that holds this one, or null
    static Transform OuterIsland(Transform island)
    {
        for (Transform p = island.parent; p != null; p = p.parent)
            if (Island.IsIsland(p)) return p;
        return null;
    }

    // Has: it has respawn points (maybe none usable now)
    Transform RespawnPointIn(Transform container, Vector3 near, out bool has)
    {
        // Its RespawnPoints (the tool's: inside it, or kept in a level standing on it). Only if it has none: the old ones,
        // just named RespawnPoint (OriginalGame)
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
        has = any;
        if (any) return best;
        foreach (Transform child in LegacyPointsIn(container))
        {
            if (child == null) continue;
            float d = (child.position - near).sqrMagnitude;
            if (d < nearest) { nearest = d; best = child; }
        }
        has = best != null;
        return best;
    }

    public Transform RespawnPointOf(GameObject island)
    {
        if (island == null) return null;
        Island root = island.GetComponentInParent<Island>();
        Transform container = root != null ? root.transform : island.transform;
        foreach (Transform child in LegacyPointsIn(container)) if (child != null) return child;
        return null;
    }

    // The old respawn points of an island (just named RespawnPoint), looked for once per island (before: its whole
    // hierarchy, 4 times a second while you stood on an island without a RespawnPoint)
    private readonly System.Collections.Generic.Dictionary<Transform, Transform[]> legacyPoints = new System.Collections.Generic.Dictionary<Transform, Transform[]>();
    Transform[] LegacyPointsIn(Transform container)
    {
        if (!legacyPoints.TryGetValue(container, out Transform[] found))
        {
            found = System.Array.FindAll(container.GetComponentsInChildren<Transform>(true), x => x.name == respawnPointName);
            legacyPoints[container] = found;
        }
        return found;
    }
}
