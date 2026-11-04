using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Keeps the player on the right course with the meme window (Game). Goes in Managers > CourseGuide.
//   Another level   carried onto an island of a level that isn't the meme's (its axis: the level of Island_<Title>,
//                   or the randomizer's first island for it), the meme doesn't change there (no Move), the Wrong
//                   Way Popup shows and arrows on the ground point back (to the last island of its own course, or the hub)
//   Going back      carried back onto an island it was already on (not the newest one) while its course isn't done, or
//                   reappearing after a fall: arrows on the ground point to the next island (the nearest one of its
//                   level not visited yet, from the newest one it reached)
// Without a CourseGuide in the scene: the meme changes on any island, as before (Game_Old).
// The arrows: Arrow Prefab if set (pointing along its blue axis), else simple flat chevrons. They stay Arrow Time.
public class CourseGuide : MonoBehaviour
{
    public static CourseGuide Instance { get; private set; }

    [Section("Another Level")]
    [Tooltip("A meme carried onto another level's island doesn't change there")]
    public bool blockOtherLevels = true;
    [Tooltip("Shown when it is carried into another level ('This meme isn't from here: go back'), e.g. a panel on the " +
             "tutorial popup canvas (its fade) with Localized texts. Empty: only the arrows")]
    public GameObject wrongWayPopup;
    [Tooltip("Seconds it stays")]
    public float popupTime = 4f;

    [Section("Arrows")]
    [Tooltip("Arrows when you go back along the course you already did")]
    public bool arrowsGoingBack = true;
    [Tooltip("Arrows when you reappear after a fall")]
    public bool arrowsOnRespawn = true;
    [Tooltip("Your own arrow (points along its blue axis, lies on the ground). Empty: flat chevrons")]
    public GameObject arrowPrefab;
    public Color arrowColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    [Tooltip("How many arrows in a row")]
    [Range(1, 6)] public int arrowCount = 3;
    [Tooltip("Meters from you to the first one")]
    public float arrowStart = 1.5f;
    [Tooltip("Meters between them")]
    public float arrowSpacing = 1.2f;
    [Tooltip("Arrow size (meters)")]
    public float arrowSize = 0.8f;
    [Tooltip("Seconds they stay (they fade out at the end)")]
    public float arrowTime = 5f;
    [Tooltip("Logs to the Console what it decides ([CourseGuide])")]
    public bool log = false;

    private readonly List<GameObject> arrows = new List<GameObject>();
    private Coroutine arrowRoutine, popupRoutine;
    private Material arrowMaterial;
    private Mesh chevron;
    private bool warnedWrongLevel;

    void Awake() => Instance = this;
    void OnEnable() => PlayerMove.RespawnTouchedDown += OnRespawned;
    void OnDisable() => PlayerMove.RespawnTouchedDown -= OnRespawned;
    void OnDestroy() { if (Instance == this) Instance = null; }

    //---- What Meme asks

    // Does this island give the meme its changes? (no CourseGuide, a meme without a level, the hub, the tutorial or an
    // optional island: yes, as always)
    public static bool CountsFor(Meme meme, GameObject island)
    {
        if (Instance == null || !Instance.blockOtherLevels) return true;
        if (!IsCourse(island)) return true;
        Transform own = LevelOf(meme);
        return own == null || LevelRoot(island.transform) == own;
    }

    // Carried onto an island (Meme, after deciding if it counts)
    public static void Reached(Meme meme, GameObject island, bool firstTime, bool countsHere)
    {
        if (Instance != null) Instance.OnReached(meme, island, firstTime, countsHere);
    }

    void OnReached(Meme meme, GameObject island, bool firstTime, bool countsHere)
    {
        if (!countsHere)   // another level's island
        {
            if (warnedWrongLevel) return;   // (once until it is back on its own level or the hub)
            warnedWrongLevel = true;
            Log($"'{meme.name}' carried into another level ({island.name}): no change, back.");
            ShowPopup();
            Transform back = meme.LastNewIsland != null ? meme.LastNewIsland.transform : HubPoint();
            if (back != null) ShowArrows(Center(back));
            return;
        }
        warnedWrongLevel = false;
        if (firstTime || !arrowsGoingBack || !IsCourse(island) || island == meme.LastNewIsland) return;
        Transform next = NextIsland(meme);
        if (next == null) return;   // its course is done: going back is fine
        Log($"'{meme.name}' going back ({island.name}): arrows to {next.name}.");
        ShowArrows(Center(next));
    }

    void OnRespawned()
    {
        if (!arrowsOnRespawn) return;
        Meme meme = Meme.Held;
        if (meme == null) return;
        Transform next = NextIsland(meme);
        if (next == null) return;
        Log($"Reappeared: arrows to {next.name}.");
        StartCoroutine(ArrowsNextFrame(next));
    }

    IEnumerator ArrowsNextFrame(Transform next)
    {
        yield return new WaitForSeconds(0.3f);   // (on the ground, facing its way)
        ShowArrows(Center(next));
    }

    //---- The course

    // The next island of the meme's level: the nearest one it hasn't been on, from the newest one it reached (or you)
    Transform NextIsland(Meme meme)
    {
        Transform level = LevelOf(meme);
        if (level == null) return null;
        Vector3 from = meme.LastNewIsland != null ? Center(meme.LastNewIsland.transform) : PlayerPosition();
        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (Transform island in IslandsIn(level))
        {
            if (meme.Reached.Contains(island.gameObject) || !Meme.Unlocks(island.gameObject) || Lift.IsHiddenAt(island)) continue;   // (not side islands: Island_2A)
            float d = Vector3.Distance(from, Center(island));
            if (d < bestDistance) { bestDistance = d; best = island; }
        }
        return best;
    }

    // Part of a level's course: not the tutorial, not the hub (an island right under "Levels", in no level), not an
    // island marked Hub / Optional (its Lift Role)
    public static bool IsCourse(GameObject island)
    {
        if (island == null || island.name.Contains("Tutorial")) return false;
        if (Lift.RoleOf(island.transform) != Lift.Role.Course) return false;
        Transform level = LevelRoot(island.transform);
        return level != null && level != island.transform;
    }

    // The meme's level: the one holding its target island (Island_<Title>), else the randomizer's first island for it
    static readonly Dictionary<MemeData, Transform> levels = new Dictionary<MemeData, Transform>();
    public static Transform LevelOf(Meme meme)
    {
        MemeData data = meme != null ? meme.Data : null;
        if (data == null) return null;
        if (levels.TryGetValue(data, out Transform cached) && cached != null) return cached;
        Transform target = TargetIsland(data);
        Transform level = target != null ? LevelRoot(target) : null;
        if (level != null) levels[data] = level;
        return level;
    }

    static Transform TargetIsland(MemeData data)
    {
        string title = string.IsNullOrEmpty(data.title) ? "" : data.title.Replace(" ", "");
        if (title.Length > 0)
            foreach (Island island in FindObjectsOfType<Island>(true))
            {
                string id = island.name.Substring(island.name.LastIndexOf('_') + 1).Replace(" ", "");
                if (string.Equals(id, title, System.StringComparison.OrdinalIgnoreCase)) return island.transform;
            }
        foreach (MemeRandomizer randomizer in FindObjectsOfType<MemeRandomizer>(true))
        {
            Transform first = randomizer.FirstIslandOf(data);
            if (first != null) return first;
        }
        return null;
    }

    // Its level: the object right under "Levels" (Levels > Level_Cats > Island_21), else the LevelSettings above it
    public static Transform LevelRoot(Transform t)
    {
        if (t == null) return null;
        for (Transform p = t; p.parent != null; p = p.parent)
            if (p.parent.name == "Levels") return p;
        LevelSettings settings = t.GetComponentInParent<LevelSettings>();
        return settings != null ? settings.transform : null;
    }

    static List<Transform> IslandsIn(Transform level)
    {
        var list = new List<Transform>();
        foreach (Transform t in level.GetComponentsInChildren<Transform>(true))
            if (Island.IsIsland(t))
            {
                bool inner = false;   // (an island inside another one counts as that one)
                for (Transform p = t.parent; p != null && p != level; p = p.parent)
                    if (Island.IsIsland(p)) { inner = true; break; }
                if (!inner) list.Add(t);
            }
        return list;
    }

    static Vector3 Center(Transform t) => AimPlatform.CenterOf(t);

    static Transform HubPoint() => LevelManager.Instance != null ? LevelManager.Instance.startRespawnPoint : null;

    static Vector3 PlayerPosition()
    {
        PlayerMove player = FindObjectOfType<PlayerMove>();
        return player != null ? player.transform.position : Vector3.zero;
    }

    //---- Popup

    void ShowPopup()
    {
        if (wrongWayPopup == null) return;
        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(Popup());
    }

    IEnumerator Popup()
    {
        TutorialManager tutorial = TutorialManager.Instance;
        bool onTutorial = tutorial != null && tutorial.popupCanvas != null && wrongWayPopup.transform.IsChildOf(tutorial.popupCanvas);
        if (onTutorial) tutorial.Show(wrongWayPopup); else wrongWayPopup.SetActive(true);
        yield return new WaitForSeconds(popupTime);
        if (onTutorial) tutorial.Hide(wrongWayPopup); else wrongWayPopup.SetActive(false);
        popupRoutine = null;
    }

    //---- Arrows on the ground

    void ShowArrows(Vector3 toward)
    {
        PlayerMove player = FindObjectOfType<PlayerMove>();
        if (player == null) return;
        Vector3 origin = player.transform.position;
        Vector3 direction = toward - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 1f) return;   // already there
        direction.Normalize();

        ClearArrows();
        for (int i = 0; i < arrowCount; i++)
        {
            Vector3 spot = origin + direction * (arrowStart + i * arrowSpacing);
            int notPlayer = ~(1 << player.gameObject.layer);
            if (!Physics.Raycast(spot + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 6f, notPlayer, QueryTriggerInteraction.Ignore)) continue;   // over the void: none there (the next one may be on a stone)
            GameObject arrow = MakeArrow();
            arrow.transform.SetPositionAndRotation(ground.point + ground.normal * 0.03f,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, ground.normal), ground.normal));
            arrow.transform.SetParent(ground.transform, true);   // (moves with a moving stone or a floating island)
            arrows.Add(arrow);
        }
        if (arrowRoutine != null) StopCoroutine(arrowRoutine);
        if (arrows.Count > 0) arrowRoutine = StartCoroutine(FadeArrows());
    }

    GameObject MakeArrow()
    {
        if (arrowPrefab != null)
        {
            GameObject own = Instantiate(arrowPrefab);
            foreach (Collider c in own.GetComponentsInChildren<Collider>()) c.enabled = false;
            return own;
        }
        if (arrowMaterial == null) arrowMaterial = new Material(Shader.Find("Sprites/Default"));
        if (chevron == null) chevron = MakeChevron();
        GameObject arrow = new GameObject("GuideArrow");
        arrow.AddComponent<MeshFilter>().sharedMesh = chevron;
        MeshRenderer renderer = arrow.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = arrowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        arrow.transform.localScale = Vector3.one * arrowSize;
        return arrow;
    }

    // A flat ">" pointing forward (Z), lying on its XZ plane, 1 m wide
    static Mesh MakeChevron()
    {
        var mesh = new Mesh { name = "GuideChevron" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.25f), new Vector3(-0.5f, 0f, 0.05f), new Vector3(0f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.05f), new Vector3(0.5f, 0f, -0.25f), new Vector3(0f, 0f, 0.2f),
        };
        mesh.triangles = new[] { 0, 1, 5, 1, 2, 5, 5, 2, 3, 5, 3, 4 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    IEnumerator FadeArrows()
    {
        var block = new MaterialPropertyBlock();
        for (float t = 0f; t < arrowTime; t += Time.deltaTime)
        {
            float fade = Mathf.Clamp01((arrowTime - t) / 1f);                  // the last second: fading out
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 6f);                     // a soft pulse, so they are noticed
            for (int i = 0; i < arrows.Count; i++)
            {
                if (arrows[i] == null || arrowPrefab != null) continue;
                float wave = 0.6f + 0.4f * Mathf.Sin(t * 6f - i * 1.2f);         // a wave along the row: the way to go
                Color c = arrowColor;
                c.a *= fade * pulse * wave;
                block.SetColor("_Color", c);
                arrows[i].GetComponent<MeshRenderer>().SetPropertyBlock(block);
            }
            yield return null;
        }
        ClearArrows();
        arrowRoutine = null;
    }

    void ClearArrows()
    {
        foreach (GameObject arrow in arrows) if (arrow != null) Destroy(arrow);
        arrows.Clear();
    }

    void Log(string message) { if (log) Debug.Log("[CourseGuide] " + message, this); }
}
