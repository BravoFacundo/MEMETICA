using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

// Bridge: appears when its pedestal is solved and goes away when the meme is taken off.
// It waits until the islands that are rising have arrived, so it always connects to something that is there.
//   Rise From Below  the whole bridge rises from the void, passes the top a little and settles (see Lift)
//   Extend From A    hidden in island A, it comes out of one spot (anchor A, like a hole: debris flying) like a dolphin:
//                    the whole bridge follows one arc above its place, then settles onto anchor B and slams (needs a BridgeBend)
// Place it in its final place, from island to island: that is where it ends up.
// Bridge ends: the two far ends of everything visible in it (used when an anchor is missing).
// Anchors: one at each end, inside each island's floating part (right click > Create Anchors).
// With a BridgeBend, the bridge bends between them so its ends stay on the islands while they float.
//   Stone By Stone   a stone path (StonePath): its stones rise from the void one after another, from A to B
// It also owns the settings of its falling platforms (each platform can override them).
public class Bridge : Lift
{
    // Numbers fixed: they are what the scene saves (1 was Swing Up, removed)
    public enum Appearance { RiseFromBelow = 0, ExtendFromA = 2, StoneByStone = 3 }

    [Section("Appearance")]
    [Tooltip("In its place from the start and it stays there: visible and solid, it never lowers or hides " +
             "(e.g. the tutorial stones resting on the island). Its pedestal doesn't move it")]
    public bool staysInPlace = false;
    public Appearance appearance = Appearance.RiseFromBelow;
    [Tooltip("Waits until the rising islands arrive before appearing")]
    public bool waitForIslands = true;
    [Tooltip("Seconds after the islands arrive")]
    [ShowIf(nameof(waitForIslands))] public float delayAfterIslands = 0.3f;

    [Section("Rise From Below", nameof(appearance), Appearance.RiseFromBelow)]
    [Tooltip("Meters it waits above the Drop Distance (negative: further down). Usually 0")]
    [FormerlySerializedAs("offset")] public float lowerOffset = 0f;

    [Section("Extend From A", nameof(appearance), Appearance.ExtendFromA)]
    [Tooltip("Meters the arc it flies along rises above its place (in the middle)")]
    public float launchHeight = 1.5f;
    [Tooltip("Seconds the ground shakes at anchor A before it bursts out (a warning)")]
    public float launchWarning = 0.6f;
    [Tooltip("Meters around anchor A where the burst throws the player. In front of it: off into the void; anywhere else: back")]
    public float launchPushRadius = 3f;
    [Tooltip("Speed the player is thrown at (m/s)")]
    public float launchPushSpeed = 9f;
    [Tooltip("Camera shake when it bursts out. 0 = off")]
    public float launchShake = 0.04f;
    public AudioClip launchClip;
    [Tooltip("Pieces that break off island A when it bursts out (small rock and grass prefabs)")]
    public GameObject[] launchDebris;
    public int debrisCount = 14;

    [Tooltip("Seconds to fly into place")]
    public float moveDuration = 1.2f;
    [Tooltip("Camera shake when it slams. 0 = off")]
    public float slamShake = 0.05f;
    [Tooltip("Optional particles where it slams")]
    public ParticleSystem landingParticles;
    public AudioClip landClip;

    [Section("Anchors")]
    [Tooltip("End on one island. With both anchors, the bridge follows the islands while they float")]
    public Transform anchorA;
    [Tooltip("End on the other island")]
    public Transform anchorB;

    [Section("Falling Platforms", Closed = true)]
    [Tooltip("Settings for every falling platform in this bridge (a platform can override them)")]
    public FallingPlatformSettings platformSettings = new FallingPlatformSettings();

    private Renderer[] allRenderers;
    private Collider[] allColliders;
    private Vector3 finalPosition;
    private Quaternion finalRotation;
    private Vector3 endA, endB;   // the two far ends in its final place, measured once at Start

    // Waiting for its islands before appearing
    public bool IsWaiting { get; private set; }
    // From Raise until it has appeared (waiting, extending; Rise From Below: also see IsMoving)
    public bool IsAppearing { get; private set; }

    // Where it ends when it appears (BridgeBend bends toward the anchors as seen from there, so every animation lands on the curve)
    public Vector3 FinalPosition => appearance != Appearance.RiseFromBelow ? finalPosition : new Vector3(transform.position.x, TopY, transform.position.z);
    public Quaternion FinalRotation => appearance != Appearance.RiseFromBelow ? finalRotation : transform.rotation;

    protected override float DropFrom(LevelManager lm) => lm.dropDistance - lowerOffset;
    protected override bool HidesWhenDown => appearance == Appearance.RiseFromBelow;   // Extend From A hides inside island A

    protected override void Start()
    {
        base.Start();
        allRenderers = GetComponentsInChildren<Renderer>(true);
        allColliders = System.Array.FindAll(GetComponentsInChildren<Collider>(true), c => !c.isTrigger);

        if (staysInPlace)
        {
            SetUp(true);
            finalPosition = transform.position;
            finalRotation = transform.rotation;
            MeasureEnds();
            return;
        }
        if (appearance == Appearance.RiseFromBelow) { SetHeight(BottomY); SetPresent(false); return; }   // waits below, invisible
        if (appearance == Appearance.StoneByStone)   // in its place: its stones hide and rise themselves (StonePath)
        {
            finalPosition = transform.position;
            finalRotation = transform.rotation;
            MeasureEnds();
            return;
        }

        SetHeight(TopY);   // always in its final place, hidden until it appears
        finalPosition = transform.position;
        finalRotation = transform.rotation;
        MeasureEnds();
        SetVisible(false);
    }

    //---- Called by the LevelManager

    public override void Raise()
    {
        if (IsUp || staysInPlace) return;
        SetUp(true);
        StopAllCoroutines();
        IsAppearing = true;
        StartCoroutine(AppearRoutine());
    }

    public override void Lower()
    {
        if (!IsUp || staysInPlace) return;
        SetUp(false);
        StopAllCoroutines();
        IsWaiting = false;
        IsAppearing = false;
        if (appearance == Appearance.RiseFromBelow) StartMove(LowerDuration, false);
        else if (appearance == Appearance.StoneByStone) { if (TryGetComponent(out StonePath path)) StartCoroutine(path.SinkWave()); }
        else StartCoroutine(ExtendRoutine(false));
    }

    // Debug: already in place, no animation (DebugTools starts)
    public override void SnapUp()
    {
        if (appearance == Appearance.RiseFromBelow) { base.SnapUp(); return; }
        StopAllCoroutines();
        SetUp(true);
        if (appearance == Appearance.StoneByStone) { if (TryGetComponent(out StonePath path)) path.ShowAll(); return; }
        ResetPose();
        SetVisible(true);
        if (TryGetComponent(out BridgeBend bend)) bend.SetReveal(0f, 1f);
    }

    //---- Anchors

#if UNITY_EDITOR
    // Right click on the component > Create Anchors: an empty at each end, inside the island (or floating part) under it
    [ContextMenu("Create Anchors")]
    void CreateAnchors()
    {
        allRenderers = GetComponentsInChildren<Renderer>(true);
        finalPosition = transform.position;
        MeasureEnds();
        anchorA = CreateAnchor(endA, "A");
        anchorB = CreateAnchor(endB, "B");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    Transform CreateAnchor(Vector3 end, string label)
    {
        // The ground just past the end, toward the outside of the bridge
        Vector3 outward = (end - transform.position); outward.y = 0f;
        Vector3 probe = end + outward.normalized * 1f + Vector3.up * 3f;
        Transform parent = null;
        foreach (RaycastHit hit in Physics.RaycastAll(probe, Vector3.down, 20f))
        {
            if (hit.transform.IsChildOf(transform)) continue;
            FloatMotion floating = hit.transform.GetComponentInParent<FloatMotion>();
            Island island = hit.transform.GetComponentInParent<Island>();
            parent = floating != null ? floating.transform : island != null ? island.transform : null;
            if (parent != null) break;
        }
        if (parent == null) Debug.LogWarning($"[Bridge] '{name}': no island found under end {label}. Move the anchor onto its island by hand.", this);

        GameObject anchor = new GameObject($"BridgeAnchor_{name}_{label}");
        UnityEditor.Undo.RegisterCreatedObjectUndo(anchor, "Create Anchors");
        anchor.transform.position = end;
        if (parent != null) anchor.transform.SetParent(parent, true);
        return anchor.transform;
    }
#endif

    IEnumerator AppearRoutine()
    {
        if (waitForIslands)
        {
            IsWaiting = true;
            yield return null;   // the islands of the same pedestal start moving this frame
            while (level.AnyIslandMoving) yield return null;
            yield return new WaitForSeconds(delayAfterIslands);
            IsWaiting = false;
        }

        if (appearance == Appearance.RiseFromBelow) { SetPresent(true); StartMove(RiseDuration, true); }
        else if (appearance == Appearance.StoneByStone)
        {
            if (TryGetComponent(out StonePath path)) yield return path.RiseWave();
            else Debug.LogWarning($"[Bridge] '{name}': Stone By Stone needs a StonePath.", this);
        }
        else yield return ExtendRoutine(true);
        IsAppearing = false;
    }

    //---- Extend From A

    // The bridge sits on the part [tail, head] of its curve (0 = anchor A, 1 = anchor B); below 0 it is hidden in island A
    IEnumerator ExtendRoutine(bool appearing)
    {
        if (!TryGetComponent(out BridgeBend bend))
        {
            Debug.LogWarning($"[Bridge] '{name}': Extend From A needs a BridgeBend.", this);
            SetVisible(appearing);
            yield break;
        }

        float travel = moveDuration * 0.75f, settle = moveDuration * 0.25f;
        if (appearing)
        {
            // Warning: the ground shakes at anchor A
            bend.SetReveal(-1f, 0f, launchHeight);
            if (launchWarning > 0f)
            {
                Rumble();
                yield return new WaitForSeconds(launchWarning);
            }

            // Dives up out of island A like a dolphin: the whole bridge follows one arc path (its front first),
            // same length all the way, then the arc settles down onto its place and slams.
            // Solid while it flies: whoever lands on it rides it to the other island
            SetVisible(true, false);
            Launch();
            SetSolid(true);
            for (float t = 0f; t < travel; t += Time.deltaTime)
            {
                float head = Mathf.Sin(t / Mathf.Max(travel, 0.01f) * Mathf.PI * 0.5f);   // fast out of the island, easing in
                bend.SetReveal(head - 1f, head, launchHeight);
                yield return null;
            }
            for (float t = 0f; t < settle; t += Time.deltaTime)
            {
                float q = t / Mathf.Max(settle, 0.01f);
                bend.SetReveal(0f, 1f, launchHeight * (1f - q * q));   // comes down faster and faster
                yield return null;
            }

            bend.SetReveal(0f, 1f);
            SetSolid(true);
            Land(anchorB != null ? anchorB.position : endB);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)   // small bounce from the slam
            {
                bend.SetReveal(0f, 1f, launchHeight * 0.08f * Mathf.Sin(t / 0.3f * Mathf.PI) * (1f - t / 0.3f));
                yield return null;
            }
            bend.SetReveal(0f, 1f);
        }
        else
        {
            // The other way: rises into its arc and dives back into island A through the same spot (whoever stands on it falls)
            SetSolid(false);
            Launch();
            for (float t = 0f; t < settle; t += Time.deltaTime)
            {
                float q = t / Mathf.Max(settle, 0.01f);
                bend.SetReveal(0f, 1f, launchHeight * q * (2f - q));
                yield return null;
            }
            for (float t = 0f; t < travel; t += Time.deltaTime)
            {
                float p = t / Mathf.Max(travel, 0.01f);
                float head = 1f - p * p;
                bend.SetReveal(head - 1f, head, launchHeight);
                yield return null;
            }
            SetVisible(false);
            bend.SetReveal(0f, 1f);
        }
    }

    void ResetPose() => transform.SetPositionAndRotation(finalPosition, finalRotation);

    void MeasureEnds()
    {
        MeasureLocalEnds(out Vector3 a, out Vector3 b);
        endA = transform.TransformPoint(a);
        endB = transform.TransformPoint(b);
    }

    // The two far ends of every mesh in it, along the direction its pieces line up in (diagonal bridges too), at its pivot height.
    // In its own space, so it is right however the bridge is turned (BridgeBend and Create Anchors use it too)
    public void MeasureLocalEnds(out Vector3 a, out Vector3 b)
    {
        var points = new System.Collections.Generic.List<Vector2>();
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Matrix4x4 toLocal = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = toLocal.MultiplyPoint3x4(corner);
                points.Add(new Vector2(point.x, point.z));
            }
        }
        if (points.Count == 0) { a = b = Vector3.zero; return; }

        // Main direction seen from above (before: only its X or Z side, wrong for pieces laid out diagonally)
        Vector2 mean = Vector2.zero;
        foreach (Vector2 p in points) mean += p;
        mean /= points.Count;
        float xx = 0f, xz = 0f, zz = 0f;
        foreach (Vector2 p in points) { Vector2 d = p - mean; xx += d.x * d.x; xz += d.x * d.y; zz += d.y * d.y; }
        float angle = 0.5f * Mathf.Atan2(2f * xz, xx - zz);
        Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        float min = float.MaxValue, max = float.MinValue;
        foreach (Vector2 p in points) { float s = Vector2.Dot(p - mean, axis); min = Mathf.Min(min, s); max = Mathf.Max(max, s); }
        Vector2 start = mean + axis * min, end = mean + axis * max;
        a = new Vector3(start.x, 0f, start.y);
        b = new Vector3(end.x, 0f, end.y);
    }

    //---- Shared

    void SetVisible(bool visible, bool withColliders = true)
    {
        foreach (Renderer r in allRenderers) if (r != null) r.enabled = visible;
        if (withColliders) SetSolid(visible);
    }

    void SetSolid(bool solid)
    {
        foreach (Collider c in allColliders) if (c != null) c.enabled = solid;
    }

    Vector3 LaunchPoint => anchorA != null ? anchorA.position : endA;

    // Flat direction from anchor A to anchor B
    Vector3 LaunchDirection
    {
        get { Vector3 toward = (anchorB != null ? anchorB.position : endB) - LaunchPoint; toward.y = 0f; return toward.sqrMagnitude > 0.0001f ? toward.normalized : transform.forward; }
    }

    // Before bursting out: the ground shakes and crumbles a little at anchor A
    void Rumble()
    {
        DebrisBurst.Spawn(launchDebris, Mathf.Max(1, debrisCount / 4), LaunchPoint, Vector3.up, 2.5f, 1f);
        if (launchShake > 0f && Camera.main != null && Camera.main.TryGetComponent(out PlayerLook look))
            look.StartCoroutine(look.ShakeTheCamera(launchWarning, launchShake * 0.5f));
    }

    // Extend From A bursting out of its island: rock and grass fly up, back and to the sides (never along the bridge),
    // small shake, sound, and the player near it gets thrown
    void Launch()
    {
        Vector3 from = LaunchPoint;
        Vector3 toward = LaunchDirection;
        DebrisBurst.Spawn(launchDebris, debrisCount, from, Vector3.up * 2f - toward * 0.6f, 6f, 1f);
        PushPlayer(from, toward);
        if (launchClip != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 10f;
            }
            audioSource.PlayOneShot(launchClip);
        }
        if (launchShake > 0f && Camera.main != null && Camera.main.TryGetComponent(out PlayerLook look))
            look.StartCoroutine(look.ShakeTheCamera(0.25f, launchShake));
    }

    // In front of anchor A (where the bridge comes out): thrown off along it, into the void. Anywhere else: thrown back
    void PushPlayer(Vector3 from, Vector3 toward)
    {
        PlayerMove player = FindObjectOfType<PlayerMove>();
        if (player == null || launchPushRadius <= 0f) return;

        Vector3 offset = player.transform.position - from;
        if (Mathf.Abs(offset.y) > 3f) return;
        offset.y = 0f;
        if (offset.magnitude > launchPushRadius) return;

        Vector3 side = Vector3.Cross(Vector3.up, toward);
        bool inThePath = Vector3.Dot(offset, toward) > -0.5f && Mathf.Abs(Vector3.Dot(offset, side)) < 1.5f;
        Vector3 away = inThePath ? toward : (offset.sqrMagnitude > 0.01f ? offset.normalized : -toward);
        player.Knockback(away * launchPushSpeed + Vector3.up * launchPushSpeed * 0.5f);
    }

    void Land(Vector3 point)
    {
        if (landingParticles != null) Instantiate(landingParticles, point, Quaternion.identity).Play();
        if (landClip != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 10f;
            }
            audioSource.PlayOneShot(landClip);
        }
        if (slamShake > 0f && Camera.main != null && Camera.main.TryGetComponent(out PlayerLook look))
            look.StartCoroutine(look.ShakeTheCamera(0.2f, slamShake));
    }
}
