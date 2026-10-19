using UnityEngine;

// Anything the player carries that holds a meme: MemeOrb (Game_Old) or MemeWindow (Game).
// The environment (hold, pedestals, respawn, bridges and islands) only talks to this class,
// so it works the same with both. Each representation decides what to do in the On... methods.
// On its own (no MemeOrb / MemeWindow) it is a plain carryable meme with no visual logic, e.g. the tutorial orb.
// Falling into the void: it reappears on the last island it touched (before: PlayerRespawn on the orbs).
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

    public string ID => IDOf(gameObject);
    public bool IsHeld { get; private set; }
    public Pedestal CurrentPedestal { get; private set; }

    protected Rigidbody body;
    private LayerMask groundMask;
    private float groundedTime;

    private Transform respawnPoint;
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
        AttachToIslandBelow();
    }

    // Placed on an island in the scene: it becomes part of it before the islands move (Awake runs before their Start),
    // so it goes down and comes up with its island. Before: islands that start below left it hanging in the sky,
    // it fell into the void and reappeared in the tutorial
    private void AttachToIslandBelow()
    {
        if (transform.parent != null && transform.parent.GetComponentInParent<Island>() != null) return;   // already inside one
        if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 5f, ~0, QueryTriggerInteraction.Ignore)) return;
        Island island = ground.transform.GetComponentInParent<Island>();
        if (island == null) return;

        FloatMotion floating = ground.transform.GetComponentInParent<FloatMotion>();
        transform.SetParent(floating != null ? floating.transform : island.transform, true);
        body.isKinematic = true;   // PlayerLook turns it back to physics when picked up
        body.constraints = RigidbodyConstraints.FreezeAll;
        lastIsland = FindParentWithTag(ground.transform, "Isla") ?? island.gameObject;
    }

    protected virtual void Update()
    {
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
        if (respawnPoint == null) return;
        transform.position = respawnPoint.position;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        OnRespawned();
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
