using System.Collections;
using UnityEngine;

// A pedestal that is used once and then goes inactive (Game): once it did its job (right) and its meme is taken off,
// it goes into the ground and takes nothing until it comes back up.
//   Swallowed   the earth swallows it, slowly and shaking, for good (the target island's pedestal: its meme was
//               downloaded)
//   Stays       it stays where it is, but takes nothing more (the tutorial's: you see it, it is done)
//   Slammed     it slams into the ground, not all the way (its top still shows), and comes back up when you land
//               near it from a respawn (Rise Radius), or when something calls Restore (the hub's pedestal: ready
//               for the next round). On a randomizer it slams in as soon as the meme is chosen, with the meme on it
//               (RandomizerPedestal), so you see the island on the horizon
// Lower Way (the tutorial's pedestal): taken off, its way goes down too, and your view is held on it a few seconds
// (Watch Time) while the pedestal goes into the ground: you see the bridge fall, and nothing snaps onto it meanwhile.
// Respawn Swap (the tutorial's): two RespawnPoints of its island. Before its meme is taken off, Respawn Before is the
// island's; after, Respawn After (in front of the pedestal) replaces it: any fall brings you back there. And landing on
// ground Catch Depth meters below where you were then (the island's lower floor, with no way back up) does too, until you
// stand on another island.
// Goes on a Pedestal (white, black or randomizer). What moves is the pedestal's Body (the randomizer: the pedestal on its
// platform, not the platform). Without it, the pedestal works as always (OriginalGame).
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
    public enum RiseOn { PortalReturn, AnyRespawn }
    [Tooltip("Portal Return: comes back up when you come back to the hub through a portal. Any Respawn: also when you " +
             "land from a fall's respawn this close (Rise Radius)")]
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
        if (IsUp || mode != Mode.Slammed || moving != null) return;   // (only a slammed one comes back; still going in: not yet)
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
        Play(slamClip);
        Dust();
        Shake(0.25f, slamShake);
        moving = null;
    }

    IEnumerator Rise()
    {
        Play(riseClip);
        Dust();
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
}
