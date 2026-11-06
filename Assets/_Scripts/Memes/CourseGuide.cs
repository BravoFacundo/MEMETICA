using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Keeps the player on the right course with the meme window (Game). Goes in Managers > CourseGuide.
//   Another level   carried onto an island of a level that isn't the meme's (its axis: the level of Island_<Title>,
//                   or the randomizer's first island for it), the meme doesn't change there (no Move), the Wrong
//                   Way Popup shows and the arrows show the way back (to the last island of its own course, or the hub)
//   Going back      the wrong way while its course isn't done: stepping onto a bridge from the newest island it reached
//                   toward one it was already on, or carried back onto an old island: the arrows show the way to the
//                   next island (the nearest one of its level not visited yet, from the newest one it reached)
//   Respawn         optionally, reappearing after a fall (Arrows On Respawn)
// The way: from the island you are on, across the bridges that join them (the fewest; found by their anchors), one
// arrow on each stone pointing at the next one, as the stones move. Islands no bridge joins (an elevator, the portal):
// straight at it. Arrow Sprite (your own, a UI image lying flat, pointing up in the image) or Arrow Prefab (pointing along
// its blue axis), else flat chevrons. They stay Arrow Time.
// It lives here, not in the meme: what it knows is the scene's layout (levels, islands, bridges); the meme only asks
// (CountsFor) and tells (Reached). Without a CourseGuide in the scene the meme changes on any island (OriginalGame).
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
    [Tooltip("Arrows when you go the wrong way: onto a bridge back toward an island already done, or onto that island")]
    public bool arrowsGoingBack = true;
    [Tooltip("Arrows when you reappear after a fall")]
    public bool arrowsOnRespawn = true;
    [Tooltip("Your own arrow as an image (a UI sprite): lies flat, pointing where the top of the image points. Tinted by Arrow Color (white: as it is)")]
    public Sprite arrowSprite;
    [Tooltip("Your own arrow as an object (points along its blue axis, lies on the ground). Used if there is no Arrow Sprite. Empty: flat chevrons")]
    public GameObject arrowPrefab;
    public Color arrowColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    [Tooltip("Most arrows along the way (one per stone): far ones are left out")]
    [FormerlySerializedAs("arrowCount")] [Range(3, 30)] public int maxArrows = 14;
    [Tooltip("Meters from you to the first one")]
    public float arrowStart = 1.5f;
    [Tooltip("Meters between them on the ground (islands, solid bridges)")]
    public float arrowSpacing = 1.2f;
    [Tooltip("Arrow size (meters)")]
    public float arrowSize = 0.8f;
    [Tooltip("Meters they float above the ground or the stone")]
    public float arrowHover = 0.15f;
    [Tooltip("Seconds between one arrow popping in and the next (they show the way in order)")]
    public float arrowPop = 0.06f;
    [Tooltip("Seconds they stay once all are in (they fade out at the end)")]
    public float arrowTime = 5f;
    [Tooltip("Logs to the Console what it decides ([CourseGuide])")]
    public bool log = false;

    private Coroutine arrowRoutine, popupRoutine;
    private Material arrowMaterial;
    private Mesh chevron;
    private bool warnedWrongLevel;

    void Awake() => Instance = this;

    // The wrong way, early: stepping onto a bridge that goes from the newest island back to one already done
    private PlayerMove player;
    private Bridge lastBridge;
    private float nextCheck;
    void Update()
    {
        if (!arrowsGoingBack || Time.time < nextCheck) return;
        nextCheck = Time.time + 0.2f;
        if (player == null) player = FindObjectOfType<PlayerMove>();
        Meme meme = Meme.Held;
        if (player == null || meme == null) { lastBridge = null; return; }
        Bridge bridge = player.GroundUnder != null ? player.GroundUnder.GetComponentInParent<Bridge>() : null;
        if (bridge == lastBridge) return;
        lastBridge = bridge;
        if (bridge == null || !GoingBack(meme, bridge, out Transform next)) return;
        Log($"'{meme.name}' onto {bridge.name}, back toward a done island: arrows to {next.name}.");
        ShowWay(next);
    }

    // This bridge leaves the newest island toward one already done (and the next one isn't there)
    bool GoingBack(Meme meme, Bridge bridge, out Transform next)
    {
        next = null;
        if (meme.LastNewIsland == null || bridge.anchorA == null || bridge.anchorB == null) return false;
        Transform newest = Outer(meme.LastNewIsland.transform);
        Transform a = Outer(Island.Of(bridge.anchorA)), b = Outer(Island.Of(bridge.anchorB));
        Transform other = a == newest ? b : b == newest ? a : null;
        if (other == null || !Visited(meme, other)) return false;
        next = NextIsland(meme);
        return next != null && Outer(next) != other;
    }

    static bool Visited(Meme meme, Transform island)
    {
        foreach (GameObject reached in meme.Reached) if (reached != null && Outer(reached.transform) == island) return true;
        return false;
    }
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
            if (back != null) ShowWay(back);
            return;
        }
        warnedWrongLevel = false;
        if (firstTime || !arrowsGoingBack || !IsCourse(island) || island == meme.LastNewIsland) return;
        Transform next = NextIsland(meme);
        if (next == null) return;   // its course is done: going back is fine
        Log($"'{meme.name}' going back ({island.name}): arrows to {next.name}.");
        ShowWay(next);
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
        ShowWay(next);
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
        foreach (RandomizerPedestal randomizer in FindObjectsOfType<RandomizerPedestal>(true))
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

    //---- The way: from the island you are on, along the bridges, to the island

    // One bridge of the way, and which way you cross it (from its anchor A to B, or back)
    struct Crossing { public Bridge bridge; public bool forward; }

    // The bridges that join From and To, in order (the fewest), only the ones that are up. Null: no bridges join them
    // (an elevator, the portal): then the arrows point straight at it
    static List<Crossing> Route(Transform from, Transform to)
    {
        if (from == null || to == null || from == to) return null;
        var links = new List<(Bridge bridge, Transform a, Transform b)>();
        foreach (Bridge bridge in FindObjectsOfType<Bridge>())
        {
            if (bridge.IsHidden || bridge.anchorA == null || bridge.anchorB == null) continue;
            Transform a = Outer(Island.Of(bridge.anchorA)), b = Outer(Island.Of(bridge.anchorB));
            if (a != null && b != null && a != b) links.Add((bridge, a, b));
        }

        // Breadth first: the way with the fewest bridges
        var cameFrom = new Dictionary<Transform, (Transform island, Crossing crossing)>();
        var queue = new Queue<Transform>();
        queue.Enqueue(from);
        cameFrom[from] = (null, default);
        while (queue.Count > 0)
        {
            Transform here = queue.Dequeue();
            if (here == to) break;
            foreach (var link in links)
            {
                Transform next = link.a == here ? link.b : link.b == here ? link.a : null;
                if (next == null || cameFrom.ContainsKey(next)) continue;
                cameFrom[next] = (here, new Crossing { bridge = link.bridge, forward = link.a == here });
                queue.Enqueue(next);
            }
        }
        if (!cameFrom.ContainsKey(to)) return null;
        var route = new List<Crossing>();
        for (Transform t = to; cameFrom[t].island != null; t = cameFrom[t].island) route.Insert(0, cameFrom[t].crossing);
        return route;
    }

    // An island inside another one counts as that one (as IslandsIn)
    static Transform Outer(Transform island)
    {
        if (island == null) return null;
        for (Transform p = island.parent != null ? Island.Of(island.parent) : null; p != null; p = p.parent != null ? Island.Of(p.parent) : null)
            island = p;
        return island;
    }

    //---- Arrows: over the ground and the stones, each one pointing at the next

    class Arrow { public Transform root, look; public float bob; }
    private readonly List<Arrow> shown = new List<Arrow>();
    private Transform wayEnd;          // where the last arrow points (the island)
    private Vector3 wayEndOffset;      // (its middle, from it: it floats)

    // Arrows from you to that island: across its bridges, stone by stone. No bridges between you: straight at it
    void ShowWay(Transform target)
    {
        PlayerMove player = FindObjectOfType<PlayerMove>();
        if (player == null || target == null) return;
        Transform island = Outer(Island.Of(target)) ?? target;
        Transform here = player.CurrentIsland != null ? Outer(player.CurrentIsland.transform) : null;
        ClearArrows();
        wayEnd = island;
        wayEndOffset = Center(island) - island.position;

        List<Crossing> route = Route(here, island);
        Vector3 from = player.transform.position;
        if (route == null || route.Count == 0)
        {
            Log($"No bridges from {(here != null ? here.name : "where you are")} to {island.name}: straight at it.");
            OverGround(from, Center(island), here, arrowSpacing * 3f);
        }
        else
        {
            Log($"The way to {island.name}: {string.Join(" > ", route.ConvertAll(c => c.bridge.name))}.");
            foreach (Crossing crossing in route)
            {
                if (shown.Count >= maxArrows) break;
                Transform start = crossing.forward ? crossing.bridge.anchorA : crossing.bridge.anchorB;
                Transform end = crossing.forward ? crossing.bridge.anchorB : crossing.bridge.anchorA;
                OverGround(from, start.position, null, Vector3.Distance(from, start.position));   // on the island, to the bridge
                AcrossBridge(crossing, start.position, end.position);
                from = end.position;
            }
            if (shown.Count < maxArrows) OverGround(from, Center(island), island, arrowSpacing * 1.5f);   // one onto the island
        }

        if (arrowRoutine != null) StopCoroutine(arrowRoutine);
        if (shown.Count > 0) arrowRoutine = StartCoroutine(PlayArrows());
    }

    // On the ground from From toward To (up to Length meters, every Arrow Spacing), only where there is ground
    // (Only: on that island)
    void OverGround(Vector3 from, Vector3 to, Transform only, float length)
    {
        Vector3 flat = to - from;
        flat.y = 0f;
        float far = Mathf.Min(length, flat.magnitude);
        if (far < 0.5f) return;
        flat.Normalize();
        for (float d = arrowStart; d <= far && shown.Count < maxArrows; d += arrowSpacing)
        {
            if (!Ground(from + flat * d, out RaycastHit hit)) continue;
            if (only != null && Outer(Island.Of(hit.transform)) != only) continue;
            AddArrow(hit.point, hit.transform);
        }
    }

    // Over its stones in order (a stone path, each one carries its arrow as it moves), else along it every Arrow Spacing
    void AcrossBridge(Crossing crossing, Vector3 start, Vector3 end)
    {
        StonePath path = crossing.bridge.GetComponentInChildren<StonePath>();
        if (path != null)
        {
            List<PathStone> stones = path.Stones;
            if (!crossing.forward) stones.Reverse();
            foreach (PathStone stone in stones)
            {
                if (shown.Count >= maxArrows) return;
                Renderer shape = stone.GetComponentInChildren<Renderer>();
                if (shape == null) continue;
                Bounds b = shape.bounds;
                AddArrow(new Vector3(b.center.x, b.max.y, b.center.z), stone.transform);
            }
            return;
        }
        float length = Vector3.Distance(start, end);
        for (float d = arrowSpacing; d < length && shown.Count < maxArrows; d += arrowSpacing)
            if (Ground(Vector3.Lerp(start, end, d / length), out RaycastHit hit) && hit.transform.IsChildOf(crossing.bridge.transform))
                AddArrow(hit.point, hit.transform);
    }

    static bool Ground(Vector3 at, out RaycastHit hit)
    {
        int mask = Physics.DefaultRaycastLayers & ~PhysicsLayers.PropsMask;   // (not the player: Ignore Raycast)
        return Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out hit, 8f, mask, QueryTriggerInteraction.Ignore);
    }

    void AddArrow(Vector3 at, Transform carrier)
    {
        var root = new GameObject("GuideArrow").transform;
        root.position = at + Vector3.up * arrowHover;
        root.SetParent(carrier, true);   // (moves with a moving stone or a floating island)
        GameObject look = MakeArrow();
        look.transform.SetParent(root, false);
        look.transform.localScale = Vector3.zero;   // (it pops in, PlayArrows)
        shown.Add(new Arrow { root = root, look = look.transform, bob = shown.Count * 0.7f });
    }

    // One after another along the way (popping in), floating a little, each one turned to the next as the stones move;
    // a light runs along them, from you to the island. The last second they fade out
    IEnumerator PlayArrows()
    {
        var block = new MaterialPropertyBlock();
        float total = arrowTime + shown.Count * arrowPop;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float fade = Mathf.Clamp01((total - t) / 1f);
            for (int i = 0; i < shown.Count; i++)
            {
                Arrow arrow = shown[i];
                if (arrow.root == null) continue;
                Vector3 next = i + 1 < shown.Count && shown[i + 1].root != null ? shown[i + 1].root.position
                             : wayEnd != null ? wayEnd.position + wayEndOffset : arrow.root.position + arrow.root.forward;
                Vector3 flat = next - arrow.root.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f) arrow.root.rotation = Quaternion.LookRotation(flat, Vector3.up);

                float age = t - i * arrowPop;   // (popping in one after another)
                float pop = age <= 0f ? 0f : age >= 0.25f ? 1f : EaseOutBack(age / 0.25f);
                arrow.look.localScale = Vector3.one * (arrowSize * pop);
                arrow.look.localPosition = Vector3.up * (Mathf.Sin(t * 3f + arrow.bob) * 0.05f);

                if (arrowPrefab != null && arrowSprite == null) continue;
                float run = Mathf.Repeat(t * 1.5f - i * 0.25f, 1f);   // a light running along the way
                Color c = arrowColor;
                c.a *= fade * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(1f - Mathf.Abs(run - 0.5f) * 4f));
                SpriteRenderer sprite = arrow.look.GetComponentInChildren<SpriteRenderer>();
                if (sprite != null) { sprite.color = c; continue; }
                block.SetColor("_Color", c);
                arrow.look.GetComponent<MeshRenderer>().SetPropertyBlock(block);
            }
            yield return null;
        }
        ClearArrows();
        arrowRoutine = null;
    }

    static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        p -= 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    GameObject MakeArrow()
    {
        if (arrowSprite != null)   // lying flat, the top of the image pointing forward; 1 m wide at Arrow Size 1
        {
            var holder = new GameObject("ArrowSprite");
            var flat = new GameObject("Image");
            flat.transform.SetParent(holder.transform, false);
            flat.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SpriteRenderer image = flat.AddComponent<SpriteRenderer>();
            image.sprite = arrowSprite;
            image.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float width = arrowSprite.bounds.size.x;
            if (width > 0f) flat.transform.localScale = Vector3.one / width;
            return holder;
        }
        if (arrowPrefab != null)
        {
            GameObject own = Instantiate(arrowPrefab);
            foreach (Collider c in own.GetComponentsInChildren<Collider>()) c.enabled = false;
            return own;
        }
        if (arrowMaterial == null) arrowMaterial = new Material(Shader.Find("Sprites/Default"));
        if (chevron == null) chevron = MakeChevron();
        GameObject arrow = new GameObject("Chevron");
        arrow.AddComponent<MeshFilter>().sharedMesh = chevron;
        MeshRenderer renderer = arrow.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = arrowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
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

    void ClearArrows()
    {
        foreach (Arrow arrow in shown) if (arrow.root != null) Destroy(arrow.root.gameObject);
        shown.Clear();
    }

    void Log(string message) { if (log) Debug.Log("[CourseGuide] " + message, this); }
}
