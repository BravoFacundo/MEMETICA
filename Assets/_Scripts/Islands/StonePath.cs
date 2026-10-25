using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// A path of stones between two islands (a Bridge with Appearance: Stone By Stone). The new version of the bridge of
// falling platforms. Its stones are its children with a PathStone, in order from island A to island B (the first one
// next to anchor A): that is the order they rise in, one after another from the void, when the bridge appears (and
// sink, from the last one, when it goes away).
// Design (Memetica > Level > Stone Path): New Stone Path makes it with a stone at each end; place those two where
// the path meets each island, set the Curve (the line in the Scene view) and Spacing / Size, and Generate Stones fills
// the middle (they look like the first end stone; change any with its Model). Then move, add or delete stones by
// hand as you like: the path keeps them where you put them. Create Anchors puts its anchors on the islands under the
// end stones; Sort Stones orders them from A to B.
// Playing, every stone keeps its place relative to the two anchors (they float with their islands, so the path floats
// and stretches with them) plus what the stone is doing (PathStone: fall, slide, hop).
[ExecuteAlways]
[DefaultExecutionOrder(-5)]   // after FloatMotion moved the islands, before the player follows the stones
[RequireComponent(typeof(Bridge))]
public class StonePath : MonoBehaviour
{
    [Section("Curve")]
    [Tooltip("Meters the middle rises above the straight line between the end stones (negative: sags)")]
    public float arch = 0.5f;
    [Tooltip("Meters the middle bends to one side (negative: the other)")]
    public float bend = 0f;
    [Tooltip("Vertical S: meters the first half goes up and the second down (negative: the other way)")]
    public float sVertical = 0f;
    [Tooltip("Horizontal S: meters the first half goes to one side and the second to the other")]
    public float sHorizontal = 0f;

    [Section("Generate")]
    [Tooltip("Size of the stones in between: 0.5 = the first stone's size (0 = half, 1 = double). Or type Size Scale")]
    [Range(0f, 1f)] public float size = 0.5f;
    [Tooltip("Size of the stones in between, times the size of the end stones (it goes from one to the other)")]
    public float sizeScale = 1f;
    [Tooltip("Room between stones: 0.5 = a gap as long as a stone; lower they get closer until they overlap by half a stone " +
             "(0); 1 = 2.5 stones of gap. Or type Spacing")]
    [Range(0f, 1f)] public float gap = 0.5f;
    [Tooltip("Meters between stone centers along the curve")]
    public float spacing = 2.2f;
    [Tooltip("Degrees each stone in between turns at random (around its vertical), so it doesn't look so perfect")]
    [Range(0f, 180f)] public float randomTurn = 25f;
    [Tooltip("Meters each stone in between goes up or down at random")]
    public float randomHeight = 0.15f;

    [System.Serializable]
    public class Model
    {
        public GameObject prefab;
        [Tooltip("Size of its look, times the prefab's own scale (fix a model that is too big here, once)")]
        public float size = 1f;
        [Tooltip("Generate Stones picks it at random for the stones in between (off: only by hand, e.g. a pushing rock)")]
        public bool inGenerate = true;
    }

    [Section("Models")]
    [Tooltip("Platform prefabs a stone can look like (each stone picks one: PathStone > Model). New Stone Path fills it " +
             "with the selected prefabs, or copies it from another stone path. Changing a Size resizes every stone using it")]
    public List<Model> models = new List<Model>();

    [Section("Look")]
    [Tooltip("Tints the stones: safe ones lighter, interactive ones darker")]
    public bool tint = true;
    public Color safeColor = new Color(0.85f, 0.85f, 0.85f);
    public Color interactiveColor = new Color(0.55f, 0.55f, 0.58f);

    [Section("Appearance (stone by stone)")]
    [Tooltip("Seconds each stone takes to rise")]
    public float riseTime = 1f;
    [Tooltip("Seconds between one stone starting to rise and the next")]
    public float stoneGap = 0.12f;
    [Tooltip("Meters a stone passes its place before settling")]
    public float overshoot = 0.3f;
    [Tooltip("Meters below its place a stone waits hidden")]
    public float riseDrop = 15f;
    [Tooltip("Seconds each stone takes to sink when the bridge goes away")]
    public float sinkTime = 0.7f;
    public AudioClip stoneRiseClip;

    [Section("Stones")]
    [Tooltip("Meters a falling stone drops before it comes back")]
    public float fallDepth = 8f;
    [Tooltip("Speed a moving stone pushes the player at when it hits them (m/s)")]
    public float pushSpeed = 7f;

    struct Rest { public float t; public Vector2 side; public Quaternion rotation; }

    private Bridge bridge;
    private readonly List<PathStone> stones = new List<PathStone>();
    private readonly Dictionary<Transform, Rest> rests = new Dictionary<Transform, Rest>();
    private readonly Dictionary<PathStone, float> lastPush = new Dictionary<PathStone, float>();
    private Vector3 endA, endB;   // designed ends when there are no anchors
    private CharacterController player;
    private PlayerMove playerMove;
    private AudioSource audioSource;
    private bool ready;

    // The stones, in order from A to B (their order in the hierarchy)
    public List<PathStone> Stones
    {
        get
        {
            var list = new List<PathStone>();
            foreach (Transform child in transform) if (child.TryGetComponent(out PathStone stone)) list.Add(stone);
            return list;
        }
    }

    // Its stones rise themselves: the bridge must appear Stone By Stone (Rise From Below would show them all at once,
    // already in place, and move only its empty root)
    void Awake()
    {
        if (TryGetComponent(out Bridge b) && b.appearance != Bridge.Appearance.StoneByStone)
        {
            if (Application.isPlaying) Debug.LogWarning($"[StonePath] '{name}': its Bridge was {b.appearance}, set to Stone By Stone.", this);
            b.appearance = Bridge.Appearance.StoneByStone;
        }
    }

    void Start()
    {
        if (Application.isPlaying) Init();
    }

    // Once, at Start or when the bridge appears first (whichever comes first)
    void Init()
    {
        if (ready) return;
        ready = true;
        bridge = GetComponent<Bridge>();
        stones.AddRange(Stones);
        if (stones.Count == 0) { Debug.LogWarning($"[StonePath] '{name}' has no stones (children with PathStone).", this); return; }
        endA = stones[0].transform.position;
        endB = stones[stones.Count - 1].transform.position;

        // Each stone (and shadow) where it is relative to the two ends now: playing, it keeps that as they float
        Frame(out Vector3 origin, out Quaternion rotation, out float length);
        foreach (PathStone stone in stones)
        {
            Capture(stone.transform, origin, rotation, length);
            foreach (Transform shadow in stone.shadows) if (shadow != null) Capture(shadow, origin, rotation, length);
        }

        player = FindObjectOfType<CharacterController>();
        if (player != null) playerMove = player.GetComponent<PlayerMove>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 10f;

        // Waiting below, hidden, until the bridge appears (unless it is there from the start)
        bool hidden = bridge.appearance == Bridge.Appearance.StoneByStone && !bridge.staysInPlace;
        foreach (PathStone stone in stones) stone.Place(!hidden, riseDrop);
    }

    void Capture(Transform t, Vector3 origin, Quaternion rotation, float length)
    {
        Vector3 local = Quaternion.Inverse(rotation) * (t.position - origin);
        rests[t] = new Rest { t = local.z / Mathf.Max(length, 0.01f), side = new Vector2(local.x, local.y), rotation = Quaternion.Inverse(rotation) * t.rotation };
    }

    // The path's space now: from anchor A (or the first stone as designed) toward anchor B, upright
    void Frame(out Vector3 origin, out Quaternion rotation, out float length)
    {
        Vector3 a = bridge.anchorA != null ? bridge.anchorA.position : endA;
        Vector3 b = bridge.anchorB != null ? bridge.anchorB.position : endB;
        origin = a;
        Vector3 along = b - a;
        length = along.magnitude;
        rotation = length > 0.001f ? Quaternion.LookRotation(along / length, Vector3.up) : Quaternion.identity;
    }

    Vector3 PlaceOf(Transform t, Vector3 origin, Quaternion rotation, float length, out Quaternion turn)
    {
        Rest rest = rests[t];
        turn = rotation * rest.rotation;
        return origin + rotation * new Vector3(rest.side.x, rest.side.y, rest.t * length);
    }

    // Point 0: the stone's place. 1..: its shadows
    Vector3 PointOf(PathStone stone, int point, Vector3 origin, Quaternion rotation, float length, out Quaternion turn)
    {
        Transform t = point > 0 && point <= stone.shadows.Count && stone.shadows[point - 1] != null ? stone.shadows[point - 1] : stone.transform;
        if (!rests.ContainsKey(t)) t = stone.transform;
        return PlaceOf(t, origin, rotation, length, out turn);
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { FollowEnds(); return; }
#endif
        if (stones.Count == 0) return;
        Frame(out Vector3 origin, out Quaternion rotation, out float length);

        foreach (PathStone stone in stones)
        {
            stone.Enforce();
            stone.Tick();
            foreach (Transform shadow in stone.shadows)   // (they float with the path too)
                if (shadow != null && rests.ContainsKey(shadow))
                    shadow.SetPositionAndRotation(PlaceOf(shadow, origin, rotation, length, out Quaternion shadowTurn), shadowTurn);
            Vector3 from = PointOf(stone, stone.From, origin, rotation, length, out Quaternion fromTurn);
            Vector3 to = PointOf(stone, stone.To, origin, rotation, length, out Quaternion toTurn);
            Vector3 place = Vector3.Lerp(from, to, stone.Progress);
            Quaternion turn = Quaternion.Slerp(fromTurn, toTurn, stone.Progress);
            Vector3 before = stone.transform.position;
            stone.transform.SetPositionAndRotation(place + stone.Shake + Vector3.down * stone.Down, turn);
            if (stone.Moves && stone.pushPlayer) PushPlayer(stone, stone.transform.position - before);
        }
    }

    // A moving stone that runs into the player (not under them) throws them along its way
    // Only when it really runs into you: its colliders inside your body, moving toward you, and you aren't standing on
    // it (it carries you). Standing on another stone right next to it is safe
    void PushPlayer(PathStone stone, Vector3 moved)
    {
        if (player == null || playerMove == null || moved.sqrMagnitude < 1e-6f || !stone.IsPresent) return;
        if (lastPush.TryGetValue(stone, out float last) && Time.time - last < 0.4f) return;
        Transform under = playerMove.GroundUnder;
        if (under != null && under.IsChildOf(stone.transform)) return;   // on it: it carries you

        Vector3 body = player.transform.position;
        foreach (Collider c in stone.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            if (!Physics.ComputePenetration(player, body, player.transform.rotation, c, c.transform.position, c.transform.rotation, out Vector3 away, out float depth)) continue;
            if (depth < 0.04f) continue;                                    // just touching
            Vector3 flat = new Vector3(moved.x, 0f, moved.z);
            if (Vector3.Dot(flat, new Vector3(away.x, 0f, away.z)) <= 0f) continue;   // moving away from you
            if (away.y > 0.7f) continue;                                    // it's under you (you're on it)
            playerMove.Knockback(flat.normalized * pushSpeed + Vector3.up * 2f);
            lastPush[stone] = Time.time;
            return;
        }
    }

    //---- Appearing (Bridge)

    // One after another from A, rising from below
    // Rising stone by stone (its moving stones wait until it is all up)
    public bool Appearing { get; private set; }

    public IEnumerator RiseWave()
    {
        Init();
        Appearing = true;
        List<PathStone> list = stones;
        for (int i = 0; i < list.Count; i++)
        {
            list[i].Rise(riseTime, overshoot, riseDrop);
            if (stoneRiseClip != null && audioSource != null) audioSource.PlayOneShot(stoneRiseClip);
            if (i < list.Count - 1) yield return new WaitForSeconds(stoneGap);
        }
        while (list.Exists(s => s.IsRising)) yield return null;
        Appearing = false;
    }

    // From the last one back, sinking into the void
    public IEnumerator SinkWave()
    {
        for (int i = stones.Count - 1; i >= 0; i--)
        {
            stones[i].Sink(sinkTime, riseDrop);
            if (i > 0) yield return new WaitForSeconds(stoneGap * 0.5f);
        }
    }

    // Debug starts: all up at once
    public void ShowAll()
    {
        Init();
        foreach (PathStone stone in stones) stone.Place(true, riseDrop);
    }

    //---- The designed curve (Generate Stones and the Scene view)

    // From a to b: t 0..1, with the Curve's arch, bend and S shapes
    public Vector3 CurvePoint(Vector3 a, Vector3 b, float t)
    {
        Vector3 along = b - a;
        Vector3 forward = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
        side.Normalize();
        Vector3 up = Vector3.Cross(forward, side);
        float hump = 4f * t * (1f - t);                 // 0 at the ends, 1 in the middle
        float s = Mathf.Sin(t * Mathf.PI * 2f);         // up in the first half, down in the second
        return Vector3.Lerp(a, b, t) + up * (arch * hump + sVertical * s) + side * (bend * hump + sHorizontal * s);
    }

    // t (0-1) where the curve has gone Distance meters
    public float AtDistance(Vector3 a, Vector3 b, float distance)
    {
        const int steps = 200;
        float walked = 0f;
        Vector3 previous = a;
        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = CurvePoint(a, b, i / (float)steps);
            float step = Vector3.Distance(previous, point);
            if (walked + step >= distance) return (i - 1 + (distance - walked) / Mathf.Max(step, 1e-6f)) / steps;
            walked += step;
            previous = point;
        }
        return 1f;
    }

    // Where Generate Stones puts the stones in between (t of each, 0-1), every Spacing meters
    public List<float> GenerateSpots(Vector3 a, Vector3 b)
    {
        float length = CurveLength(a, b);
        int count = Mathf.Max(0, Mathf.RoundToInt(length / Mathf.Max(spacing, 0.1f)) - 1);
        var spots = new List<float>();
        for (int i = 1; i <= count; i++) spots.Add(AtDistance(a, b, length * i / (count + 1)));
        return spots;
    }

    // The curve's way at t, flat (the stones in between face it, turned as the first one is)
    public Quaternion FacingAt(Vector3 a, Vector3 b, float t)
    {
        Vector3 way = CurvePoint(a, b, Mathf.Min(t + 0.01f, 1f)) - CurvePoint(a, b, Mathf.Max(t - 0.01f, 0f));
        way.y = 0f;
        return way.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(way.normalized, Vector3.up) : Quaternion.identity;
    }

    // The first stone's meshes in its own space (however it is turned): their middle, their size seen from above
    // (meters, its x and z) and their top (its y)
    public bool Footprint(out Vector3 center, out Vector2 size, out float top)
    {
        center = Vector3.zero; size = Vector2.zero; top = 0f;
        List<PathStone> list = Stones;
        if (list.Count == 0) return false;
        Transform stone = list[0].transform;
        bool any = false;
        Bounds b = default;
        foreach (MeshFilter filter in stone.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Matrix4x4 toStone = stone.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = toStone.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!any) { b = new Bounds(corner, Vector3.zero); any = true; } else b.Encapsulate(corner);
            }
        }
        if (!any) return false;
        Vector3 scale = stone.lossyScale;
        center = b.center;
        top = b.max.y;
        size = new Vector2(b.size.x * Mathf.Abs(scale.x), b.size.z * Mathf.Abs(scale.z));
        return true;
    }

    // Meters the first stone covers along the path: what Gap is measured with
    public float FirstStoneWidth()
    {
        List<PathStone> list = Stones;
        if (list.Count < 2 || !Footprint(out _, out Vector2 size, out _)) return 0f;
        Transform stone = list[0].transform;
        Vector3 way = FacingAt(stone.position, list[list.Count - 1].transform.position, 0f) * Vector3.forward;
        return Mathf.Abs(Vector3.Dot(way, stone.right)) * size.x + Mathf.Abs(Vector3.Dot(way, stone.forward)) * size.y;
    }

    public float CurveLength(Vector3 a, Vector3 b)
    {
        float length = 0f;
        Vector3 previous = a;
        for (int i = 1; i <= 64; i++)
        {
            Vector3 point = CurvePoint(a, b, i / 64f);
            length += Vector3.Distance(previous, point);
            previous = point;
        }
        return length;
    }

#if UNITY_EDITOR
    [SerializeField, HideInInspector] float lastSize = -1f, lastSizeScale = -1f, lastGap = -1f, lastSpacing = -1f;

    void OnValidate()
    {
        foreach (PathStone stone in GetComponentsInChildren<PathStone>(true)) stone.ApplyColor();
        SyncSliders();
        if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += ResizeModels;   // (not inside OnValidate)
    }

    // Editing: its anchors are where its end stones are (made the first time, inside the island under each end, its
    // floating part): move an end stone and its anchor goes with it, into the island it is now on
    void FollowEnds()
    {
        if (!gameObject.scene.IsValid() || UnityEditor.EditorUtility.IsPersistent(this) || !TryGetComponent(out Bridge bridge)) return;
        List<PathStone> list = Stones;
        if (list.Count < 2) return;
        Transform a = Follow(bridge, bridge.anchorA, list[0].transform, "A");
        Transform b = Follow(bridge, bridge.anchorB, list[list.Count - 1].transform, "B");
        if (a == bridge.anchorA && b == bridge.anchorB) return;
        UnityEditor.Undo.RecordObject(bridge, "Stone Path Anchors");
        bridge.anchorA = a;
        bridge.anchorB = b;
        UnityEditor.EditorUtility.SetDirty(bridge);
    }

    Transform Follow(Bridge bridge, Transform anchor, Transform end, string label)
    {
        if (anchor != null && (anchor.position - end.position).sqrMagnitude < 0.0001f) return anchor;   // (still there)
        if (anchor != null && TakenByAnother(bridge, anchor))   // a copied path: its own anchors (sharing them, both moved it every frame)
        {
            Debug.Log($"[StonePath] '{name}': anchor {label} was '{anchor.name}', used by another bridge too: it gets its own.", this);
            anchor = null;
        }
        Transform island = IslandUnder(end);
        if (anchor == null)
        {
            if (island == null) return null;   // not over an island yet
            anchor = new GameObject($"BridgeAnchor_{bridge.name}_{label}").transform;
            UnityEditor.Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Stone Path Anchors");
            Debug.Log($"[StonePath] '{name}': anchor {label} made in '{island.name}' (it follows its end stone).", anchor);
        }
        if (island != null && anchor.parent != island) anchor.SetParent(island, true);
        anchor.position = end.position;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        return anchor;
    }

    // Another bridge uses it as an anchor too, and it is that one's (named after it), or neither's
    static bool TakenByAnother(Bridge bridge, Transform anchor)
    {
        if (anchor.name.StartsWith($"BridgeAnchor_{bridge.name}_")) return false;   // ours: the other one lets go of it
        foreach (Bridge other in FindObjectsOfType<Bridge>(true))
            if (other != bridge && (other.anchorA == anchor || other.anchorB == anchor)) return true;
        return false;
    }

    // The island (its floating part) right under an end stone
    Transform IslandUnder(Transform end)
    {
        RaycastHit[] hits = Physics.RaycastAll(end.position + Vector3.up * 2f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            FloatMotion floating = hit.transform.GetComponentInParent<FloatMotion>();
            if (floating != null) return floating.transform;
            Island island = hit.transform.GetComponentInParent<Island>();
            if (island != null) return island.transform;
        }
        return null;
    }

    // The sliders and their numbers follow each other: move one, the other changes (Size: 0.5 = x1, halving / doubling
    // at the ends; Gap: the room between stones in stone widths, 0.5 = one)
    void SyncSliders()
    {
        bool first = lastSize < 0f;
        if (first || size != lastSize) { if (!first) sizeScale = Mathf.Pow(2f, (size - 0.5f) * 2f); }
        else if (sizeScale != lastSizeScale) size = Mathf.Clamp01(0.5f + Mathf.Log(Mathf.Max(sizeScale, 0.01f), 2f) / 2f);
        if (first) size = Mathf.Clamp01(0.5f + Mathf.Log(Mathf.Max(sizeScale, 0.01f), 2f) / 2f);

        float stone = FirstStoneWidth() * sizeScale;
        if (stone > 0.01f)
        {
            if (first || (spacing != lastSpacing && gap == lastGap)) gap = Mathf.Clamp01(0.5f + (spacing / stone - 2f) / 3f);   // typed
            else spacing = stone * (2f + 3f * (gap - 0.5f));   // the slider (or a new size): the same room, in stones
        }
        lastSize = size; lastSizeScale = sizeScale; lastGap = gap; lastSpacing = spacing;
    }

    // Each stone's look (and its shadows') at its model's Size
    void ResizeModels()
    {
        if (this == null) return;
        bool changed = false;
        foreach (PathStone stone in GetComponentsInChildren<PathStone>(true))
        {
            if (stone.model < 0 || stone.model >= models.Count || models[stone.model].prefab == null) continue;
            Vector3 scale = models[stone.model].prefab.transform.localScale * models[stone.model].size;
            foreach (Transform holder in stone.shadows.Prepend(stone.transform))
            {
                Transform look = holder != null ? holder.Find("Model") : null;
                if (look != null && look.localScale != scale) { look.localScale = scale; changed = true; }
            }
        }
        if (changed) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // The designed line between the end stones, each stone (1, 2…) and its shadows (61, 62…) with its route
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        List<PathStone> list = Stones;
        if (list.Count < 2) return;
        Vector3 a = list[0].transform.position, b = list[list.Count - 1].transform.position;
        bool selected = UnityEditor.Selection.activeTransform != null && UnityEditor.Selection.activeTransform.IsChildOf(transform);

        Gizmos.color = selected ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.85f, 0.2f, 0.35f);
        Vector3 previous = a;
        for (int i = 1; i <= 48; i++)
        {
            Vector3 point = CurvePoint(a, b, i / 48f);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        // Selected: where Generate Stones would put them now (Size, Gap): each one's outline seen from above, on its top
        if (selected && Footprint(out Vector3 center, out Vector2 size, out float top))
        {
            List<float> spots = GenerateSpots(a, b);
            Transform firstStone = list[0].transform, lastStone = list[list.Count - 1].transform;
            Quaternion startTurn = Quaternion.Inverse(FacingAt(a, b, 0f)) * firstStone.rotation;
            Vector3 scale = firstStone.lossyScale;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            foreach (float t in spots)
            {
                float ratio = Mathf.Lerp(1f, lastStone.localScale.x / Mathf.Max(firstStone.localScale.x, 1e-4f), t) * sizeScale;
                Quaternion turn = FacingAt(a, b, t) * startTurn;
                Vector3 at = CurvePoint(a, b, t) + turn * Vector3.Scale(new Vector3(center.x, top, center.z), scale) * ratio;
                DrawRing(at, turn * Vector3.right * (size.x * ratio * 0.5f), turn * Vector3.forward * (size.y * ratio * 0.5f));
            }
            UnityEditor.Handles.Label(CurvePoint(a, b, 0.5f) + Vector3.up * 1.5f, $"Generate: {spots.Count} stones");
        }

        // An anchor far from its end stone: the ends moved since Create Anchors
        Bridge bridge = GetComponent<Bridge>();
        if (bridge != null) { WarnAnchor(bridge.anchorA, a, "A"); WarnAnchor(bridge.anchorB, b, "B"); }
        for (int i = 0; i < list.Count; i++)
        {
            PathStone stone = list[i];
            Gizmos.color = stone.motion == PathStone.Motion.None ? Color.white : new Color(1f, 0.45f, 0.2f);
            Gizmos.DrawWireSphere(stone.transform.position, 0.15f);
            if (selected) UnityEditor.Handles.Label(stone.transform.position + Vector3.up * 0.6f, (i + 1).ToString());
            DrawRoute(stone, i + 1, selected);
        }
    }

    // An oval: half its width along X, half its length along Z
    static void DrawRing(Vector3 center, Vector3 x, Vector3 z)
    {
        const int segments = 24;
        for (int i = 0; i < segments; i++)
        {
            float u = i * Mathf.PI * 2f / segments, v = (i + 1) * Mathf.PI * 2f / segments;
            Gizmos.DrawLine(center + x * Mathf.Cos(u) + z * Mathf.Sin(u), center + x * Mathf.Cos(v) + z * Mathf.Sin(v));
        }
    }

    static void WarnAnchor(Transform anchor, Vector3 end, string label)
    {
        if (anchor == null || Vector3.Distance(anchor.position, end) < 1.5f) return;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(anchor.position, end);
        UnityEditor.Handles.Label(anchor.position + Vector3.up, $"Anchor {label}: not on its end stone");
    }

    void DrawRoute(PathStone stone, int number, bool labels)
    {
        var points = new List<Vector3> { stone.transform.position };
        for (int k = 0; k < stone.shadows.Count; k++)
        {
            Transform shadow = stone.shadows[k];
            if (shadow == null) continue;
            points.Add(shadow.position);
            if (labels) UnityEditor.Handles.Label(shadow.position + Vector3.up * 0.6f, $"{number}{k + 1}");
        }
        if (points.Count < 2) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f);
        switch (stone.route)
        {
            case PathStone.Route.BackAndForth:   // 0 - 61 - 62 …
            case PathStone.Route.Loop:           // … and back to 0
                for (int k = 1; k < points.Count; k++) Gizmos.DrawLine(points[k - 1], points[k]);
                if (stone.route == PathStone.Route.Loop && points.Count > 2) Gizmos.DrawLine(points[points.Count - 1], points[0]);
                break;
            case PathStone.Route.ThroughPlace:   // 0 to each one
                for (int k = 1; k < points.Count; k++) Gizmos.DrawLine(points[0], points[k]);
                break;
            default:                             // any to any
                for (int a = 0; a < points.Count; a++)
                    for (int b = a + 1; b < points.Count; b++) Gizmos.DrawLine(points[a], points[b]);
                break;
        }
    }
#endif
}
