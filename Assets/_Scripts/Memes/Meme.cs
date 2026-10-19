using System.Collections;
using UnityEngine;

// Anything the player carries that holds a meme: MemeOrb (Game_Old) or MemeWindow (Game).
// The environment (hold, pedestals, respawn, bridges and islands) only talks to this class,
// so it works the same with both. Each representation decides what to do in the On... methods.
// On its own (no MemeOrb / MemeWindow) it is a plain carryable meme with no visual logic, e.g. the tutorial orb.
// Falling into the void: it comes back to the last island it touched as a meteor: glowing with a trail, falling
// at an angle onto open ground near the island's middle (not trees, not pedestals), and breaking the ground on impact.
// While held it doesn't check: it travels with the player, and PlayerMove tells it when the player falls.
// Object name: Meme_<ID> (e.g. Meme_3Aa). Pedestals compare their Key ID with that ID.
[RequireComponent(typeof(Rigidbody))]
public class Meme : MonoBehaviour
{
    public const string NamePrefix = "Meme_";

    [Header("Rest")]
    [Tooltip("Seconds on the ground before it freezes in place, so it doesn't roll away")]
    public float restDelay = 3f;
    public float groundCheckRadius = 0.6f;

    [Header("Respawn (meteor)")]
    [Tooltip("Meters above the island it starts falling from")]
    public float meteorHeight = 25f;
    [Tooltip("Meters it travels sideways while falling (the angle of the fall)")]
    public float meteorDrift = 10f;
    [Tooltip("Seconds the fall takes (it speeds up)")]
    public float meteorDuration = 1.1f;
    [Tooltip("Meters around the island's middle (its RespawnPoint) where it can land")]
    public float landingRadius = 4f;
    [Tooltip("Never lands closer than this to a pedestal (it would place itself)")]
    public float pedestalClearance = 3f;
    public Color meteorColor = new Color(1f, 0.75f, 0.35f);
    [Tooltip("Pieces that fly out where it lands (small rock and grass prefabs, like the bridges)")]
    public GameObject[] impactDebris;
    public int impactDebrisCount = 10;
    public AudioClip impactClip;

    public string ID => IDOf(gameObject);
    public bool IsHeld { get; private set; }
    public Pedestal CurrentPedestal { get; private set; }

    protected Rigidbody body;
    private LayerMask groundMask;
    private float groundedTime;
    private Island startIsland;   // held still on it until it is up and still

    private Transform respawnPoint;
    private Coroutine meteor;
    private Transform lastGroundHit;
    private GameObject lastIsland;
    private float lastGroundY;

    // "Meme_3Aa" → "3Aa". Everything after the first "_", so old names ("Orb_3Aa") still work
    public static string IDOf(GameObject meme)
    {
        int separator = meme.name.IndexOf('_');
        return separator >= 0 ? meme.name.Substring(separator + 1) : meme.name;
    }

    protected virtual void Awake()
    {
        body = GetComponent<Rigidbody>();
        groundMask = LayerMask.GetMask("Ground");
        lastGroundY = transform.position.y;
        HoldOnStartIsland();
    }

    // On an island in the scene (its child, or resting on it): put down on its ground and held still, part of the island,
    // before the islands move (Awake runs before their Start). It goes down and comes up with its island, and once the
    // island has arrived it is let go with physics. Before: the island dropping 30 m at start counted as a fall into the void
    private void HoldOnStartIsland()
    {
        if (!FindGroundBelow(20f, out RaycastHit ground)) return;
        Island island = transform.parent != null ? transform.parent.GetComponentInParent<Island>() : null;
        if (island == null) island = ground.transform.GetComponentInParent<Island>();
        if (island == null) return;

        // On the ground, not floating above it
        Collider shape = GetComponentInChildren<Collider>();
        float pivotToBottom = shape != null ? transform.position.y - shape.bounds.min.y : 0f;
        transform.position = ground.point + Vector3.up * (pivotToBottom + 0.02f);

        if (transform.parent == null || transform.parent.GetComponentInParent<Island>() != island)
        {
            FloatMotion floating = ground.transform.GetComponentInParent<FloatMotion>();
            transform.SetParent(floating != null ? floating.transform : island.transform, true);
        }

        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezeAll;
        startIsland = island;
        lastIsland = FindParentWithTag(transform, "Isla") ?? island.gameObject;
    }

    // Its island is up and still: physics again (it settles on the ground and freezes there as usual)
    private void ReleaseWhenIslandArrives()
    {
        if (startIsland == null) return;
        if (IsHeld || CurrentPedestal != null) { startIsland = null; return; }   // someone else took over
        if (!startIsland.IsUp || startIsland.IsMoving) return;

        startIsland = null;
        transform.SetParent(null, true);
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.None;
        groundedTime = 0f;
    }

    // The nearest solid thing below that is not this meme
    private bool FindGroundBelow(float distance, out RaycastHit ground)
    {
        ground = default;
        float nearest = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position + Vector3.up * 0.5f, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
            nearest = hit.distance;
            ground = hit;
        }
        return nearest < float.MaxValue;
    }

    protected virtual void Update()
    {
        ReleaseWhenIslandArrives();
        FreezeWhenRestingOnGround();
        CheckFallAndRespawn();
    }

    //---- Called by the environment

    public void PickUp()
    {
        IsHeld = true;
        groundedTime = 0f;
        OnPickedUp();
    }

    public void Drop()
    {
        if (!IsHeld) return;
        IsHeld = false;
        OnDropped();
    }

    // Pedestal: the meme snaps in (if it was in the hand, it counts as dropped first)
    public void PlaceOnPedestal(Pedestal pedestal)
    {
        Drop();
        CurrentPedestal = pedestal;
        OnPlaced(pedestal);
    }

    public void RemoveFromPedestal(Pedestal pedestal)
    {
        if (CurrentPedestal != pedestal) return;
        CurrentPedestal = null;
        OnRemovedFromPedestal(pedestal);
    }

    public virtual void OnPedestalResult(Pedestal pedestal, bool isCorrect) { }
    public virtual void Sweep(float duration) { }
    public virtual void OnBridgeCrossed() { }
    public virtual void OnIslandVisited(GameObject island) { }
    public virtual void OnRespawned() { }

    //---- Overridden by each representation

    protected virtual void OnPickedUp() { }
    protected virtual void OnDropped() { }
    protected virtual void OnPlaced(Pedestal pedestal) { }
    protected virtual void OnRemovedFromPedestal(Pedestal pedestal) { }

    // Fija el respawn a mano. Lo usa el ascensor con los memes que viajan en él.
    public void SetRespawn(Transform point)
    {
        respawnPoint = point;
        lastGroundHit = null;
    }

    //---- Respawn

    private void CheckFallAndRespawn()
    {
        if (IsHeld || LevelManager.Instance == null) return;
        if (body.isKinematic) { lastGroundY = transform.position.y; return; }   // attached (island, pedestal): moves with it, never falls
        if (respawnPoint == null)   // the island it started on (before: always the tutorial)
            respawnPoint = lastIsland != null ? LevelManager.Instance.RespawnPointOf(lastIsland) : LevelManager.Instance.startRespawnPoint;
        if (respawnPoint == null) respawnPoint = LevelManager.Instance.startRespawnPoint;

        if (transform.position.y < lastGroundY - LevelManager.Instance.fallDistance)
        {
            Respawn();
            return;
        }

        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f)) return;
        lastGroundY = hit.point.y;
        if (hit.transform == lastGroundHit) return;
        lastGroundHit = hit.transform;

        GameObject island = FindParentWithTag(hit.transform, "Isla");
        if (island == null || island == lastIsland) return;   // un puente, o la misma isla
        lastIsland = island;

        Transform point = LevelManager.Instance.RespawnPointOf(lastIsland);
        if (point != null) respawnPoint = point;
    }

    private void Respawn()
    {
        if (respawnPoint == null || meteor != null) return;
        meteor = StartCoroutine(MeteorFall());
    }

    //---- Meteor

    IEnumerator MeteorFall()
    {
        transform.SetParent(null, true);
        startIsland = null;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        if (!FindLanding(out Vector3 start, out Vector3 landing)) { start = respawnPoint.position; landing = start; }   // (colliders still on: it measures itself)
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders) c.enabled = false;   // no pedestal or tree catches it on the way

        // Glow and trail while it falls
        Light glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = meteorColor;
        glow.range = 10f;
        glow.intensity = 6f;
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.45f;
        trail.startWidth = 0.7f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = meteorColor;
        trail.endColor = new Color(meteorColor.r, meteorColor.g * 0.5f, 0f, 0f);

        for (float t = 0f; t < meteorDuration; t += Time.deltaTime)
        {
            float p = t / Mathf.Max(meteorDuration, 0.01f);
            transform.position = Vector3.Lerp(start, landing, p * p);   // speeds up like a meteor
            transform.Rotate(Vector3.one * 720f * Time.deltaTime, Space.Self);
            yield return null;
        }
        transform.position = landing;

        // Impact: the ground breaks, then it is a normal meme again
        DebrisBurst.Spawn(impactDebris, impactDebrisCount, landing, Vector3.up, 5f, 1.2f);
        if (impactClip != null) AudioSource.PlayClipAtPoint(impactClip, landing);
        foreach (Collider c in colliders) c.enabled = true;
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.None;
        body.velocity = (landing - start).normalized * 2f;   // a little roll after the hit
        lastGroundY = landing.y;
        groundedTime = 0f;
        trail.emitting = false;
        OnRespawned();

        for (float t = 0f; t < 0.4f; t += Time.deltaTime) { glow.intensity = Mathf.Lerp(6f, 0f, t / 0.4f); yield return null; }
        Destroy(glow);
        Destroy(trail, trail.time);
        meteor = null;
    }

    // Open ground of its island near the middle: not steep (tree tops, rocks), not near a pedestal, and a clear
    // diagonal path from the sky. A few random tries, so it doesn't always land on the same spot
    private bool FindLanding(out Vector3 start, out Vector3 landing)
    {
        start = landing = Vector3.zero;
        Collider shape = GetComponentInChildren<Collider>(true);
        float pivotToBottom = shape != null ? shape.bounds.extents.y : 0.3f;
        Vector3 middle = respawnPoint.position;

        for (int i = 0; i < 20; i++)
        {
            Vector2 offset = Random.insideUnitCircle * landingRadius;
            Vector3 above = new Vector3(middle.x + offset.x, middle.y + 5f, middle.z + offset.y);
            if (!Physics.Raycast(above, Vector3.down, out RaycastHit ground, 200f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (lastIsland != null && FindParentWithTag(ground.transform, "Isla") != lastIsland) continue;
            if (ground.normal.y < 0.85f || NearPedestal(ground.point)) continue;

            Vector2 side = Random.insideUnitCircle.normalized;
            Vector3 land = ground.point + Vector3.up * (pivotToBottom + 0.02f);
            Vector3 from = land + Vector3.up * meteorHeight + new Vector3(side.x, 0f, side.y) * meteorDrift;
            Vector3 path = land - from;
            if (Physics.SphereCast(from, 0.5f, path.normalized, out _, path.magnitude - 0.8f, ~0, QueryTriggerInteraction.Ignore)) continue;   // trees, rocks in the way

            start = from;
            landing = land;
            return true;
        }
        return false;
    }

    private bool NearPedestal(Vector3 point)
    {
        foreach (Pedestal pedestal in FindObjectsOfType<Pedestal>())
        {
            Vector3 offset = pedestal.transform.position - point; offset.y = 0f;
            if (offset.magnitude < pedestalClearance) return true;
        }
        return false;
    }

    private static GameObject FindParentWithTag(Transform child, string tag)
    {
        for (Transform t = child.parent; t != null; t = t.parent)
            if (t.CompareTag(tag)) return t.gameObject;
        return null;
    }

    //---- Rest (before: OrbPosition)

    private void FreezeWhenRestingOnGround()
    {
        bool isResting = !IsHeld && CurrentPedestal == null && Physics.CheckSphere(transform.position, groundCheckRadius, groundMask);
        groundedTime = isResting ? groundedTime + Time.deltaTime : 0f;
        if (groundedTime >= restDelay && body.constraints != RigidbodyConstraints.FreezeAll)
        {
            body.constraints = RigidbodyConstraints.FreezeAll;
            StickToFloatingGround();
        }
    }

    // Resting on a floating island (FloatMotion): it becomes part of it, so it bobs with the ground instead of hovering
    private void StickToFloatingGround()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, groundCheckRadius + 1f, ~0, QueryTriggerInteraction.Ignore)) return;
        FloatMotion floating = ground.transform.GetComponentInParent<FloatMotion>();
        if (floating == null) return;
        transform.SetParent(floating.transform);
        body.isKinematic = true;   // PlayerLook turns it back to physics when picked up
    }
}
