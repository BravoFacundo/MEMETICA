using System.Collections;
using UnityEngine;

// A pedestal that is used once and then goes inactive (Game): once it did its job (right) and its meme is taken off,
// it goes into the ground and takes nothing until it comes back up.
//   Swallowed   the earth swallows it, slowly and shaking, for good (the target island's pedestal: its meme was
//               downloaded)
//   Stays       it stays where it is, but takes nothing more (the tutorial's: you see it, it is done)
//   Slammed     it slams into the ground, not all the way (its top still shows), and comes back up when you land
//               near it from a respawn (Rise Radius), or when something calls Restore (the hub's pedestal: ready
//               for the next round; Rise On Never: it stays in for good, the target island's: the meme is not
//               placed again and the portal stays up). On a randomizer it slams in as soon as the meme is chosen, with the meme on it
//               (RandomizerPedestal), so you see the island on the horizon
// Lower Way (the tutorial's pedestal): taken off, its way goes down too, and your view is held on it a few seconds
// (Watch Time) while the pedestal goes into the ground: you see the bridge fall, and nothing snaps onto it meanwhile.
// Respawn Swap (the tutorial's): two RespawnPoints of its island. Before its meme is taken off, Respawn Before is the
// island's; after, Respawn After (in front of the pedestal) replaces it: any fall brings you back there. And landing on
// ground Catch Depth meters below where you were then (the island's lower floor, with no way back up) does too, until you
// stand on another island.
// Goes on a Pedestal (white, black or randomizer). What moves is the pedestal's Body (the randomizer: the pedestal on its
// platform, not the platform). Without it, the pedestal works as always (OriginalGame).
// Rocks (a rock model): out of the ground in a diagonal around its base. Slammed: stuck there, locked; Swallowed: closing over it.
[RequireComponent(typeof(Pedestal))]
public class PedestalRetire : MonoBehaviour
{
    public enum Mode { Swallowed, Slammed, Stays }
    public enum When { MemeTakenOff, Result }

    public Mode mode = Mode.Slammed;
    [Tooltip("Meme Taken Off: once right and its meme is taken off. Result: as soon as it is right (a randomizer: always " +
             "after its raffle)")]
    public When when = When.MemeTakenOff;
    [Tooltip("Slammed at Result: lets its meme go as it slams in (a little hop out, no 'wrong' sound). Off: the meme " +
             "goes down with it. Swallowed always lets it go")]
    public bool dropMeme = false;
    [Tooltip("Seconds after its meme is taken off before it goes down (so you see it)")]
    public float delay = 0.4f;
    [Tooltip("Meme Taken Off: its way (what it raised) goes down too, as a pedestal without this would. Off: its way stays up")]
    public bool lowerWay = false;
    [Tooltip("Lower Way: seconds your view is held on its way going down (as the randomizer does). 0: free")]
    [ShowIf(nameof(lowerWay))] public float watchTime = 2f;
    [Tooltip("Lower Way: seconds the view takes to turn to it (short: it falls fast, you must get there in time)")]
    [ShowIf(nameof(lowerWay))] public float watchTurn = 0.1f;

    [Section("Respawn Swap", Closed = true)]
    [Tooltip("The island's RespawnPoint until its meme is taken off (then turned off). Empty: none to turn off")]
    public RespawnPoint respawnBefore;
    [Tooltip("The RespawnPoint from then on, in front of the pedestal (off until then: it turns it off at the start). Empty: off")]
    public RespawnPoint respawnAfter;
    [Tooltip("Meters below where you were when its meme was taken off: landing lower than that (the floor below) brings " +
             "you back to Respawn After")]
    public float catchDepth = 4f;

    [Section("Slammed", nameof(mode), Mode.Slammed)]
    [Tooltip("Part of its height that goes into the ground (0.5 = half; less than 1: its top still shows)")]
    [Range(0.1f, 0.95f)] public float slamPortion = 0.6f;
    [Tooltip("Seconds it takes to slam in (short: violent)")]
    public float slamTime = 0.18f;
    public enum RiseOn { PortalReturn, AnyRespawn, Never }
    [Tooltip("Portal Return: comes back up when you come back to the hub through a portal. Any Respawn: also when you " +
             "land from a fall's respawn this close (Rise Radius). Never: it stays in for good (Restore does nothing)")]
    public RiseOn riseOn = RiseOn.PortalReturn;
    [Tooltip("Any Respawn: meters. 0 = only by Restore")]
    public float riseRadius = 30f;
    [Tooltip("Seconds it takes to come back up (grinding out of the ground)")]
    public float riseTime = 1.4f;
    public AudioClip slamClip;
    public AudioClip riseClip;

    [Section("Swallowed", nameof(mode), Mode.Swallowed)]
    [Tooltip("Seconds the earth takes to swallow it")]
    public float swallowTime = 2.5f;
    public AudioClip swallowClip;

    [Section("Rocks", nameof(mode), Mode.Slammed, Mode.Swallowed)]
    [Tooltip("Rocks out of the ground in a diagonal around its base (RockDebries). Slammed: they burst out as it slams in " +
             "and stay (stuck there, locked). Swallowed: they push out, tremble, then close over the hole. Empty: none")]
    public GameObject rock;
    [Tooltip("Their material. Empty: the pedestal's own (its biggest mesh: white, black…)")]
    public Material rockMaterial;
    [Tooltip("How many (between these two)")]
    public Vector2Int rockCount = new Vector2Int(5, 7);
    [Tooltip("Meters wide of each rock (between these two)")]
    public Vector2 rockWidth = new Vector2(0.25f, 0.45f);
    [Tooltip("Length as a multiple of its width (2: twice as long as wide, a shard)")]
    public float rockStretch = 2f;
    [Tooltip("Degrees from straight up, leaning away from the pedestal")]
    [Range(0f, 80f)] public float rockTilt = 40f;
    [Tooltip("Part of its length that shows out of the ground")]
    [Range(0.1f, 1f)] public float rockReach = 0.6f;
    [Tooltip("The model's tip points down in the model (RockDebries): turned over, so its tip comes out of the ground")]
    public bool rockFlip = true;
    [Tooltip("Meters from the middle of its base. 0: measured from its size (70% of its widest part)")]
    public float rockRadius = 0f;
    [Tooltip("Meters out from that edge (-: a little under it)")]
    public float rockGap = -0.05f;
    [Tooltip("Slammed: seconds they take to burst out")]
    public float rockBurstTime = 0.1f;
    [Tooltip("Swallowed: how much they tremble (meters)")]
    public float rockTremble = 0.04f;
    [Tooltip("Swallowed: degrees they lean in over the hole as they close")]
    public float rockCloseLean = 25f;

    [Section("Feel", Closed = true)]
    [Tooltip("Camera shake when it slams in. 0 = off")]
    public float slamShake = 0.08f;
    [Tooltip("How much it trembles while it grinds in or out (meters)")]
    public float tremble = 0.03f;
    [Tooltip("Optional dust where it meets the ground (played when it goes in or comes out)")]
    public ParticleSystem dust;

    // Up and ready (the pedestal takes memes only then)
    public bool IsUp { get; private set; } = true;
    // Going in or coming out now
    public bool IsMoving => moving != null;

    private Transform body;      // what goes in and out (the Pedestal's Body)
    private Vector3 restLocal;   // its place, on its parent (it floats with its island)
    private float depth;         // meters below its place now
    private Vector3 jitter;
    private Coroutine moving;
    private AudioSource audioSource;

    void Awake()
    {
        body = GetComponent<Pedestal>().Body;
        restLocal = body.localPosition;
        height = MeasureHeight();   // now, before it holds a meme
        audioSource = GetComponent<AudioSource>();
        MeasureBase();              // (now, at rest)
        rockLook = rockMaterial != null ? rockMaterial : PedestalMaterial();   // (now, before it holds a meme)
        if (respawnAfter != null) respawnAfter.gameObject.SetActive(false);   // (its turn comes when the meme is taken off)
    }

    void OnEnable() { PlayerRespawn.RespawnTouchedDown += OnRespawn; PortalWindow.Arrived += OnPortal; }
    void OnDisable() { PlayerRespawn.RespawnTouchedDown -= OnRespawn; PortalWindow.Arrived -= OnPortal; }

    void OnPortal()
    {
        if (!IsUp && mode == Mode.Slammed) Restore();
    }

    void LateUpdate()
    {
        if (depth <= 0f && jitter == Vector3.zero) return;
        Vector3 down = body.parent != null ? body.parent.InverseTransformDirection(Vector3.down) : Vector3.down;
        body.localPosition = restLocal + down * depth + jitter;
    }

    //---- Called by the Pedestal

    // Its job is done and its meme was taken off
    public void Retire()
    {
        if (!IsUp) return;
        IsUp = false;
        if (respawnAfter != null && when == When.MemeTakenOff) SwapRespawn();
        if (mode == Mode.Stays) return;   // (where it is: it only takes nothing more)
        Run(mode == Mode.Swallowed ? Swallow() : Slam());
    }

    // Respawn Before off, Respawn After on and yours now; then the floor below watched
    void SwapRespawn()
    {
        if (respawnBefore != null) respawnBefore.gameObject.SetActive(false);
        respawnAfter.gameObject.SetActive(true);
        if (PlayerRespawn.Instance != null) PlayerRespawn.Instance.SetRespawn(respawnAfter.transform);
        StartCoroutine(CatchFalls());
    }

    // Landed on ground Catch Depth below where you were: back to Respawn After. Until you stand on another island
    IEnumerator CatchFalls()
    {
        PlayerMove player = FindObjectOfType<PlayerMove>();
        PlayerRespawn respawn = PlayerRespawn.Instance;
        if (player == null || respawn == null) yield break;
        float floor = player.transform.position.y - catchDepth;
        Transform home = Outermost(Island.Of(transform));
        while (true)
        {
            yield return null;
            Transform under = respawn.GroundUnder;
            Transform island = under != null ? Outermost(Island.Of(under)) : null;
            if (island != null && home != null && island != home) yield break;   // somewhere else now: done
            if (player.isGrounded && !respawn.Respawning && player.transform.position.y < floor)
            {
                respawn.RespawnAt(respawnAfter.transform);
                yield return new WaitForSeconds(1f);   // (landing there first)
            }
        }
    }

    // Back up and ready (the portal back to the hub will call it; for now landing from a respawn nearby does)
    public void Restore()
    {
        if (IsUp || mode != Mode.Slammed || riseOn == RiseOn.Never || moving != null) return;   // (only a slammed one comes back; still going in: not yet)
        Run(Rise());
    }

    void OnRespawn()
    {
        if (IsUp || mode != Mode.Slammed || riseOn != RiseOn.AnyRespawn || riseRadius <= 0f) return;
        PlayerMove player = FindObjectOfType<PlayerMove>();
        if (player != null && Vector3.Distance(player.transform.position, transform.position) <= riseRadius) Restore();
    }

    // The island everything here is part of (the tutorial's elevator is part of its island)
    static Transform Outermost(Transform island)
    {
        for (Transform p = island != null ? island.parent : null; p != null; p = p.parent)
            if (Island.IsIsland(p)) island = p;
        return island;
    }

    //---- Movements

    IEnumerator Slam()
    {
        yield return new WaitForSeconds(delay);
        if (dropMeme) DropMeme();
        float slamDepth = Height() * slamPortion;
        for (float t = 0f; t < slamTime; t += Time.deltaTime)
        {
            float p = t / slamTime;
            depth = slamDepth * p * p;   // speeding up: hammered in
            yield return null;
        }
        depth = slamDepth;
        BurstRocks();
        Play(slamClip);
        Dust();
        Shake(0.25f, slamShake);
        moving = null;
    }

    IEnumerator Rise()
    {
        Play(riseClip);
        Dust();
        RetractRocks(riseTime * 0.5f);
        Shake(riseTime, slamShake * 0.25f);
        float from = depth;
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            float p = t / riseTime;
            depth = Mathf.Lerp(from, 0f, p * p * (3f - 2f * p));
            jitter = Random.insideUnitSphere * tremble * (1f - p);
            yield return null;
        }
        depth = 0f;
        jitter = Vector3.zero;
        body.localPosition = restLocal;
        IsUp = true;
        moving = null;
    }

    IEnumerator Swallow()
    {
        yield return new WaitForSeconds(delay);
        DropMeme();   // (not swallowed with it)
        Play(swallowClip);
        Dust();
        GrindRocks(swallowTime);
        Shake(swallowTime, slamShake * 0.3f);
        float full = Height() + 0.2f;
        for (float t = 0f; t < swallowTime; t += Time.deltaTime)
        {
            float p = t / swallowTime;
            depth = full * p * p;   // slow at first, then gone
            jitter = Random.insideUnitSphere * tremble;
            yield return null;
        }
        jitter = Vector3.zero;
        HideRocks();
        body.gameObject.SetActive(false);   // for good
    }

    //---- Shared

    void DropMeme()
    {
        if (TryGetComponent(out Pedestal pedestal)) pedestal.DropMeme();
    }

    void Run(IEnumerator routine)
    {
        if (moving != null) StopCoroutine(moving);
        moving = StartCoroutine(routine);
    }

    // Meters tall of its meshes (measured once, at the start)
    float height = -1f;
    float Height()
    {
        if (height > 0f) return height;
        height = MeasureHeight();
        return height;
    }

    float MeasureHeight()
    {
        float top = float.MinValue, bottom = float.MaxValue;
        Transform from = body != null ? body : GetComponent<Pedestal>().Body;
        foreach (MeshRenderer r in from.GetComponentsInChildren<MeshRenderer>())
        {
            top = Mathf.Max(top, r.bounds.max.y);
            bottom = Mathf.Min(bottom, r.bounds.min.y);
        }
        return top > bottom ? top - bottom : 2f;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    void Dust()
    {
        if (dust != null) dust.Play();
    }

    void Shake(float duration, float amount)
    {
        if (amount <= 0f) return;
        PlayerLook.Shake(duration, amount);
    }

    //---- Rocks (around its base, only seen: no colliders). Selected, the gizmo shows where they go

    struct Rock
    {
        public Transform t;
        public Vector3 ground;      // where it comes out (in the container)
        public Quaternion rot;      // leaning out
        public Vector3 tangent;     // the axis it leans around
        public Vector3 center;      // its middle, from its pivot (scaled)
        public float length;
        public float delay;
    }

    struct RockSpot { public Vector3 ground; public Quaternion rot; public Vector3 tangent; public float width, delay; }

    private Rock[] rocks;
    private float[] rockOuts;       // 0 in the ground, 1 out
    private Vector3 rockBase;       // the middle of its base, at rest (in its Body's parent: on the randomizer, the
                                    // platform, so they turn with it and come out wherever it slams in)
    private Material rockLook;      // the pedestal's own (measured at the start)
    private float rockEdge;         // meters from there to where they come out
    private Coroutine rocksMoving;

    // Slammed: out at once (with a little overshoot), and they stay
    void BurstRocks()
    {
        if (BuildRocks()) RunRocks(BurstRoutine());
    }

    // Slammed, coming back up: back into the ground
    void RetractRocks(float seconds)
    {
        if (rocks != null) RunRocks(RetractRoutine(seconds));
    }

    // Swallowed: out slowly, trembling, then closing over the hole as it goes
    void GrindRocks(float seconds)
    {
        if (BuildRocks()) RunRocks(GrindRoutine(seconds));
    }

    void HideRocks()
    {
        if (rocksMoving != null) StopCoroutine(rocksMoving);
        rocksMoving = null;
        if (rocks == null) return;
        for (int i = 0; i < rocks.Length; i++) { rockOuts[i] = 0f; rocks[i].t.gameObject.SetActive(false); }
    }

    IEnumerator BurstRoutine()
    {
        float time = Mathf.Max(0.01f, rockBurstTime);
        for (float t = 0f; ; t += Time.deltaTime)
        {
            bool done = true;
            for (int i = 0; i < rocks.Length; i++)
            {
                float p = Mathf.Clamp01((t - rocks[i].delay) / time);
                if (p < 1f) done = false;
                rockOuts[i] = OutBack(p);
                PoseRock(i, rockOuts[i], 0f, Vector3.zero, Quaternion.identity);
            }
            if (done) break;
            yield return null;
        }
        rocksMoving = null;
    }

    IEnumerator RetractRoutine(float seconds)
    {
        float[] from = (float[])rockOuts.Clone();
        seconds = Mathf.Max(0.01f, seconds);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float p = t / seconds;
            for (int i = 0; i < rocks.Length; i++)
            {
                rockOuts[i] = from[i] * (1f - p * p);
                PoseRock(i, rockOuts[i], 0f, Random.insideUnitSphere * rockTremble * 0.5f, Quaternion.identity);
            }
            yield return null;
        }
        HideRocks();
    }

    IEnumerator GrindRoutine(float seconds)
    {
        seconds = Mathf.Max(0.1f, seconds);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float p = t / seconds;
            for (int i = 0; i < rocks.Length; i++)
            {
                // Each a little apart: out (0 to 0.3), trembling, then in, leaning over the hole (0.7 to 1)
                float emerge = Smooth(Mathf.InverseLerp(0f, 0.3f, Mathf.Clamp01(p - rocks[i].delay)));
                float close = Smooth(Mathf.InverseLerp(0.7f, 1f, p));
                rockOuts[i] = emerge * (1f - close) * 0.75f;
                Quaternion wobble = Quaternion.Euler(Random.insideUnitSphere * 4f * (1f - close));
                PoseRock(i, rockOuts[i], rockCloseLean * close, Random.insideUnitSphere * rockTremble, wobble);
            }
            yield return null;
        }
        HideRocks();
    }

    // Rock i: how far out (0 in, 1 out), degrees leaning in over the hole, a shake and a wobble
    void PoseRock(int i, float amount, float lean, Vector3 shake, Quaternion wobble)
    {
        Rock r = rocks[i];
        Quaternion rot = Quaternion.AngleAxis(-lean, r.tangent) * r.rot * wobble;
        Vector3 up = rot * Vector3.up;
        Vector3 hidden = r.ground - up * (r.length * 0.5f + 0.05f);
        Vector3 shown = r.ground + up * (r.length * (rockReach - 0.5f));
        Quaternion model = rockFlip ? rot * Quaternion.Euler(180f, 0f, 0f) : rot;   // (tip up)
        r.t.gameObject.SetActive(amount > 0.001f);
        r.t.localRotation = model;
        r.t.localPosition = Vector3.LerpUnclamped(hidden, shown, amount) - model * r.center + shake;
    }

    // The rocks, made the first time (hidden). False: no rock model
    bool BuildRocks()
    {
        if (rocks != null) return true;
        if (rock == null) return false;

        Transform parent = body.parent;   // (what the pedestal rides: they go with it)
        Transform container = new GameObject("Rocks (" + name + ")").transform;
        container.SetParent(parent, false);
        container.localPosition = rockBase;
        container.localRotation = Quaternion.identity;
        Vector3 lossy = parent != null ? parent.lossyScale : Vector3.one;
        container.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);   // (meters, whatever the island's scale)

        Material look = rockLook;
        Bounds b = MeshBounds(rock);   // (the model's own size)
        Vector3 size = Vector3.Max(b.size, Vector3.one * 0.01f);
        RockSpot[] layout = RockLayout();
        rocks = new Rock[layout.Length];
        rockOuts = new float[layout.Length];
        for (int i = 0; i < layout.Length; i++)
        {
            RockSpot s = layout[i];
            float w = s.width, l = s.width * rockStretch;
            Vector3 f = new Vector3(w / size.x, l / size.y, w / size.z);
            GameObject go = Instantiate(rock, container);
            go.name = "Rock " + i;
            StripRock(go);
            if (look != null)
                foreach (Renderer rr in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[rr.sharedMaterials.Length];
                    for (int k = 0; k < mats.Length; k++) mats[k] = look;
                    rr.sharedMaterials = mats;
                }
            go.transform.localScale = f;
            go.SetActive(false);
            rocks[i] = new Rock { t = go.transform, ground = s.ground, rot = s.rot, tangent = s.tangent,
                                  center = Vector3.Scale(b.center, f), length = l, delay = s.delay };
        }
        return true;
    }

    // Where each goes, around the base (up: the island's up). Always the same for this pedestal
    RockSpot[] RockLayout()
    {
        Random.State state = Random.state;
        Vector3 p = transform.position;
        Random.InitState(Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.z * 10f) * 19349663);
        int n = Random.Range(Mathf.Max(1, rockCount.x), Mathf.Max(rockCount.x, rockCount.y) + 1);
        var spots = new RockSpot[n];
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < n; i++)
        {
            float angle = start + 360f * i / n + Random.Range(-0.3f, 0.3f) * 360f / n;   // around, not too even
            Vector3 outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 tangent = Vector3.Cross(Vector3.up, outward);
            spots[i] = new RockSpot
            {
                ground = outward * (rockEdge + rockGap + Random.Range(-0.05f, 0.1f)),
                tangent = tangent,
                rot = Quaternion.AngleAxis(rockTilt + Random.Range(-10f, 10f), tangent) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                width = Random.Range(rockWidth.x, Mathf.Max(rockWidth.x, rockWidth.y)),
                delay = Random.Range(0f, 0.05f)
            };
        }
        Random.state = state;
        return spots;
    }

    // The middle of its base and how far its edge is (its Body's meshes, now)
    void MeasureBase()
    {
        Transform from = body != null ? body : GetComponent<Pedestal>().Body;
        bool any = false;
        Bounds b = new Bounds(from.position, Vector3.zero);
        foreach (MeshRenderer r in from.GetComponentsInChildren<MeshRenderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        Vector3 world = new Vector3(b.center.x, b.min.y, b.center.z);
        rockBase = from.parent != null ? from.parent.InverseTransformPoint(world) : world;
        rockEdge = rockRadius > 0f ? rockRadius : Mathf.Max(b.extents.x, b.extents.z, 0.3f) * 0.7f;
    }

    // The pedestal's look: its biggest mesh's material (not the meme on it)
    Material PedestalMaterial()
    {
        Material best = null;
        float most = 0f;
        foreach (MeshRenderer r in body.GetComponentsInChildren<MeshRenderer>())
        {
            Vector3 size = r.bounds.size;
            float volume = size.x * size.y * size.z;
            if (r.sharedMaterial == null || volume <= most) continue;
            most = volume;
            best = r.sharedMaterial;
        }
        return best;
    }

    // Only seen: nothing that collides, falls or runs
    static void StripRock(GameObject go)
    {
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; Destroy(rb); }
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (MonoBehaviour m in go.GetComponentsInChildren<MonoBehaviour>()) m.enabled = false;
    }

    // Its meshes' size in its root's own space (works on the prefab itself)
    static Bounds MeshBounds(GameObject model)
    {
        bool any = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.one);
        foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 toRoot = model.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds m = mf.sharedMesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = m.center + Vector3.Scale(m.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                Vector3 point = toRoot.MultiplyPoint3x4(corner);
                if (!any) { b = new Bounds(point, Vector3.zero); any = true; }
                else b.Encapsulate(point);
            }
        }
        return b;
    }

    void RunRocks(IEnumerator routine)
    {
        if (rocksMoving != null) StopCoroutine(rocksMoving);
        rocksMoving = StartCoroutine(routine);
    }

    static float Smooth(float p) => p * p * (3f - 2f * p);

    // Past 1 a little, then back (a burst)
    static float OutBack(float p)
    {
        const float c = 1.70158f;
        float q = p - 1f;
        return 1f + (c + 1f) * q * q * q + c * q * q;
    }

    // Where the rocks go (selected): a line for each, from the ground out
    void OnDrawGizmosSelected()
    {
        if (rock == null || mode == Mode.Stays) return;
        if (!Application.isPlaying) MeasureBase();
        Transform parent = (body != null ? body : GetComponent<Pedestal>().Body).parent;
        Quaternion turn = parent != null ? parent.rotation : Quaternion.identity;
        Vector3 middle = parent != null ? parent.TransformPoint(rockBase) : rockBase;
        Gizmos.color = new Color(0.8f, 0.6f, 0.4f);
        foreach (RockSpot s in RockLayout())
        {
            Vector3 ground = middle + turn * s.ground;
            Vector3 up = turn * (s.rot * Vector3.up);
            Gizmos.DrawLine(ground, ground + up * s.width * rockStretch * rockReach);
            Gizmos.DrawWireSphere(ground, s.width * 0.3f);
        }
    }
}
