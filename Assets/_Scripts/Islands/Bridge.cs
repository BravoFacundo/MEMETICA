using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

// Bridge: appears when its pedestal is solved and goes away when the meme is taken off.
// It waits until the islands that are rising have arrived, so it always connects to something that is there.
//   Rise From Below  the whole bridge rises from the void, passes the top a little and settles (see Lift)
//   Swing Up         hinged at the end next to the pedestal: it swings up from below and slams onto the other side
// Bridge ends: the two far ends of everything visible in it; the start is the end closest to the pedestal
// (or Start Point if assigned). With Swing Up its colliders turn on when it lands.
// Anchors: one at each end, inside each island's floating part (right click > Create Anchors).
// With a BridgeBend, the bridge bends between them so its ends stay on the islands while they float.
// It also owns the settings of its falling platforms (each platform can override them).
public class Bridge : Lift
{
    public enum Appearance { RiseFromBelow, SwingUp }

    [Header("Appearance")]
    public Appearance appearance = Appearance.RiseFromBelow;
    [Tooltip("Waits until the rising islands arrive before appearing")]
    public bool waitForIslands = true;
    [Tooltip("Seconds after the islands arrive")]
    public float delayAfterIslands = 0.3f;
    [Tooltip("Optional: rises up to this object's height instead of the general one (the tutorial bridge uses it)")]
    public Transform topOverride;
    [Tooltip("Rise From Below: meters above the lowest point where it waits")]
    [FormerlySerializedAs("offset")] public float lowerOffset = 0f;
    [Tooltip("Swing Up: optional hinge end. Empty = the end closest to the pedestal that activated it")]
    [FormerlySerializedAs("assembleFrom")] public Transform startPoint;

    [Header("Swing Up")]
    [Tooltip("Seconds to swing into place")]
    public float moveDuration = 1.2f;
    [Tooltip("Degrees it hangs below before swinging up")]
    public float swingAngle = 100f;
    [Tooltip("Degrees it bounces back after slamming. 0 = no bounce")]
    public float slamBounce = 3f;
    [Tooltip("Camera shake when it slams. 0 = off")]
    public float slamShake = 0.05f;
    [Tooltip("Optional particles where it slams")]
    public ParticleSystem landingParticles;
    public AudioClip landClip;

    [Header("Anchors")]
    [Tooltip("End on one island. With both anchors, the bridge follows the islands while they float")]
    public Transform anchorA;
    [Tooltip("End on the other island")]
    public Transform anchorB;

    [Header("Falling Platforms")]
    [Tooltip("Settings for every falling platform in this bridge (a platform can override them)")]
    public FallingPlatformSettings platformSettings = new FallingPlatformSettings();

    private Renderer[] allRenderers;
    private Collider[] allColliders;
    private Vector3 finalPosition;
    private Quaternion finalRotation;
    private Vector3 lastOrigin;
    private Vector3 endA, endB;   // the two far ends in its final place, measured once at Start
    private bool animating;

    // Waiting for its islands before appearing
    public bool IsWaiting { get; private set; }

    // Where it ends when it appears (BridgeBend bends toward the anchors as seen from there, so every animation lands on the curve)
    public Vector3 FinalPosition => appearance == Appearance.SwingUp ? finalPosition : new Vector3(transform.position.x, TopY, transform.position.z);
    public Quaternion FinalRotation => appearance == Appearance.SwingUp ? finalRotation : transform.rotation;

    protected override float TopY => topOverride != null ? topOverride.position.y : level.topY;
    protected override float BottomY => level.bottomY + lowerOffset;

    protected override void Start()
    {
        base.Start();
        allRenderers = GetComponentsInChildren<Renderer>(true);
        allColliders = System.Array.FindAll(GetComponentsInChildren<Collider>(true), c => !c.isTrigger);

        if (appearance == Appearance.RiseFromBelow) { SetHeight(BottomY); return; }   // waits below

        SetHeight(TopY);   // always in its final place, hidden until it appears
        finalPosition = transform.position;
        finalRotation = transform.rotation;
        MeasureEnds();
        SetVisible(false);
    }

    //---- Called by the LevelManager

    // origin: where the activation came from (the pedestal), to know which end is the hinge
    public void Raise(Vector3 origin)
    {
        lastOrigin = origin;
        Raise();
    }

    public override void Raise()
    {
        if (IsUp) return;
        SetUp(true);
        StopAllCoroutines();
        animating = true;   // until it is in place (waiting for islands, swinging); Rise From Below uses IsMoving
        StartCoroutine(AppearRoutine());
    }

    public override void Lower()
    {
        if (!IsUp) return;
        SetUp(false);
        StopAllCoroutines();
        animating = false;
        IsWaiting = false;
        if (appearance == Appearance.RiseFromBelow) StartMove(BottomY, LowerDuration, false);
        else StartCoroutine(SwingRoutine(false));
    }

    // Debug: already in place, no animation (DebugTools starts)
    public override void SnapUp()
    {
        if (appearance == Appearance.RiseFromBelow) { base.SnapUp(); return; }
        StopAllCoroutines();
        animating = false;
        SetUp(true);
        ResetPose();
        SetVisible(true);
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

        if (appearance == Appearance.RiseFromBelow) { StartMove(TopY, RiseDuration, true); animating = false; }
        else yield return SwingRoutine(true);
    }

    //---- Swing Up

    IEnumerator SwingRoutine(bool appearing)
    {
        animating = true;
        GetEnds(out Vector3 start, out Vector3 end);
        Vector3 axis = HingeAxis(start, end);
        if (appearing) SetVisible(true, false);
        else SetSolid(false);   // whoever stands on it falls

        // Appearing: accelerates into the slam. Going away: starts slow, speeds up into the void
        for (float t = 0f; t < moveDuration; t += Time.deltaTime)
        {
            float p = t / Mathf.Max(moveDuration, 0.01f);
            float hanging = appearing ? 1f - p * p : p * p;   // 1 = hanging below, 0 = in place
            PoseRotatedAround(start, Quaternion.AngleAxis(swingAngle * hanging, axis));
            yield return null;
        }

        if (!appearing) { SetVisible(false); ResetPose(); animating = false; yield break; }

        ResetPose();
        SetSolid(true);
        Land(end);

        // Small bounce after the slam
        if (slamBounce > 0f)
        {
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                PoseRotatedAround(start, Quaternion.AngleAxis(-slamBounce * Mathf.Sin(t / 0.25f * Mathf.PI), axis));
                yield return null;
            }
            ResetPose();
        }
        animating = false;
    }

    // Axis of the hinge at the start end: horizontal, across the bridge. Positive angles hang it down
    static Vector3 HingeAxis(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start; direction.y = 0f;
        return Vector3.Cross(Vector3.up, direction.normalized);
    }

    void PoseRotatedAround(Vector3 pivot, Quaternion rotation)
    {
        transform.SetPositionAndRotation(pivot + rotation * (finalPosition - pivot), rotation * finalRotation);
    }

    void ResetPose() => transform.SetPositionAndRotation(finalPosition, finalRotation);

    void MeasureEnds()
    {
        MeasureLocalEnds(out Vector3 a, out Vector3 b);
        endA = transform.TransformPoint(a);
        endB = transform.TransformPoint(b);
    }

    // The two far ends of every mesh in it, along its longest horizontal side, at its pivot height.
    // In its own space, so it is right however the bridge is turned (BridgeBend and Create Anchors use it too)
    public void MeasureLocalEnds(out Vector3 a, out Vector3 b)
    {
        Bounds local = new Bounds();
        bool first = true;
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Matrix4x4 toLocal = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = toLocal.MultiplyPoint3x4(corner);
                if (first) { local = new Bounds(point, Vector3.zero); first = false; } else local.Encapsulate(point);
            }
        }
        Vector3 half = local.extents.x >= local.extents.z ? new Vector3(local.extents.x, 0f, 0f) : new Vector3(0f, 0f, local.extents.z);
        Vector3 center = new Vector3(local.center.x, 0f, local.center.z);
        a = center - half;
        b = center + half;
    }

    // The start (hinge) is the end closest to the pedestal (or Start Point), the other one is where it lands
    void GetEnds(out Vector3 start, out Vector3 end)
    {
        Vector3 reference = startPoint != null ? startPoint.position : lastOrigin;
        bool aFirst = (endA - reference).sqrMagnitude <= (endB - reference).sqrMagnitude;
        start = startPoint != null ? startPoint.position : (aFirst ? endA : endB);
        end = aFirst ? endB : endA;
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
