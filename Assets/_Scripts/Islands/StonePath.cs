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
    [Tooltip("Meters between stone centers along the curve")]
    public float spacing = 2.2f;
    [Tooltip("Size of the stones in between, times the size of the end stones (it goes from one to the other)")]
    public float sizeScale = 1f;
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
        if (!Application.isPlaying || stones.Count == 0) return;
        Frame(out Vector3 origin, out Quaternion rotation, out float length);

        foreach (PathStone stone in stones)
        {
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
    void PushPlayer(PathStone stone, Vector3 moved)
    {
        if (player == null || playerMove == null || moved.sqrMagnitude < 1e-6f || !stone.IsPresent) return;
        if (lastPush.TryGetValue(stone, out float last) && Time.time - last < 0.4f) return;
        Bounds stoneBounds = default;
        bool any = false;
        foreach (Collider c in stone.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            if (!any) { stoneBounds = c.bounds; any = true; } else stoneBounds.Encapsulate(c.bounds);
        }
        if (!any) return;
        stoneBounds.Expand(0.1f);
        Bounds body = player.bounds;
        if (!stoneBounds.Intersects(body) || body.min.y > stoneBounds.max.y - 0.15f) return;   // standing on it: it carries them
        Vector3 push = new Vector3(moved.x, 0f, moved.z).normalized * pushSpeed + Vector3.up * 2f;
        playerMove.Knockback(push);
        lastPush[stone] = Time.time;
    }

    //---- Appearing (Bridge)

    // One after another from A, rising from below
    public IEnumerator RiseWave()
    {
        Init();
        List<PathStone> list = stones;
        for (int i = 0; i < list.Count; i++)
        {
            list[i].Rise(riseTime, overshoot, riseDrop);
            if (stoneRiseClip != null && audioSource != null) audioSource.PlayOneShot(stoneRiseClip);
            if (i < list.Count - 1) yield return new WaitForSeconds(stoneGap);
        }
        while (list.Exists(s => s.IsRising)) yield return null;
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
    void OnValidate()
    {
        foreach (PathStone stone in GetComponentsInChildren<PathStone>(true)) stone.ApplyColor();
        if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += ResizeModels;   // (not inside OnValidate)
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
        for (int i = 0; i < list.Count; i++)
        {
            PathStone stone = list[i];
            Gizmos.color = stone.motion == PathStone.Motion.None ? Color.white : new Color(1f, 0.45f, 0.2f);
            Gizmos.DrawWireSphere(stone.transform.position, 0.15f);
            if (selected) UnityEditor.Handles.Label(stone.transform.position + Vector3.up * 0.6f, (i + 1).ToString());
            DrawRoute(stone, i + 1, selected);
        }
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
