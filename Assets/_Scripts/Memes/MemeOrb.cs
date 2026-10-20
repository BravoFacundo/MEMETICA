using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// Meme as an orb (both scenes). Before: OrbMemes. Same behavior as always:
// the image mutates over time while you carry it and move, and returns to the original.
// Mutation timing and sounds: MemeSettings (shared by every orb). Here only what is each orb's own: its images.
public class MemeOrb : Meme
{
    public GameObject Player;

    [Header("Images (0 is the original)")]
    public List<Material> materials = new List<Material>();
    public int matCount;

    private MeshRenderer memeRenderer;
    private AudioSource sfx;
    private MaterialPropertyBlock mpb;
    private PlayerMove playerMove;
    private readonly HashSet<GameObject> visitedIslands = new HashSet<GameObject>();
    private float movingTime;            // time moving with the orb in the hand
    private float nextChangeAt;
    private float lastEventTime = -99f;
    private Vector3 lastPlayerPos;
    private bool wasHolded;
    private Coroutine mutationRoutine;
    private Coroutine sweepRoutine;
    private int lastVariant;

    void Start()
    {
        Player = GameObject.Find("Player");

        // Images are assigned in the Inspector (before: loaded from Resources by the orb's name)
        if (materials.Count == 0) { Debug.LogError("[MemeOrb] '" + name + "' has no images assigned.", this); enabled = false; return; }
        matCount = 0;
        memeRenderer = gameObject.transform.Find("Meme").GetComponent<MeshRenderer>();
        memeRenderer.sharedMaterial = materials[matCount];

        // Its own sound source for the mutation (the orb's one is muted while you carry it)
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 1f; sfx.minDistance = 4f;
        mpb = new MaterialPropertyBlock();
        playerMove = Player.GetComponent<PlayerMove>();
        lastPlayerPos = Player.transform.position;

        ApplyRestingEffects();   // before: the Update polling did it on the first frame
    }

    protected override void Update()
    {
        base.Update();
        UpdateMutation();   // the image mutates over time while you carry it and move
    }

    //---- Meme events

    protected override void OnPickedUp() => ApplyHoldingEffects();
    protected override void OnDropped() => ApplyRestingEffects();
    protected override void OnBridgeCrossed() => MarkEvent();   // a new bridge: don't mutate right there

    // Carried to another island: one more version unlocked
    protected override void OnIslandVisited(GameObject island)
    {
        visitedIslands.Add(island);
        MarkEvent();
    }

    //---- Mutation
    // The image doesn't depend on any single action, only on the time you move carrying the orb.
    // After each variant, and when you drop it, it goes back to the original.
    // Each new island you carry it to unlocks one more version for the random pick.

    void UpdateMutation()
    {
        if (materials.Count < 2) return;

        Vector3 playerPos = Player.transform.position;
        Vector3 delta = playerPos - lastPlayerPos; delta.y = 0f;
        lastPlayerPos = playerPos;

        // Moments the player could take as the cause of the change: no mutating near them
        if (delta.magnitude > 3f) MarkEvent();                          // respawn / teleport
        if (playerMove != null && !playerMove.isGrounded) MarkEvent();  // jumping or falling

        if (IsHeld && !wasHolded) StartMutationTimer();
        if (!IsHeld && wasHolded) StopMutationAndRestore();
        wasHolded = IsHeld;

        if (!IsHeld || mutationRoutine != null) return;

        if (delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f) > 0.5f) movingTime += Time.deltaTime; // only counts while moving
        if (movingTime >= nextChangeAt && !InGrace())
            mutationRoutine = StartCoroutine(Mutate());
    }

    void StartMutationTimer()
    {
        // Taken off the pedestal mid-sweep: the sweep stops and it goes back to the original
        if (sweepRoutine != null) { StopCoroutine(sweepRoutine); sweepRoutine = null; SetImage(0); }
        movingTime = 0f;
        nextChangeAt = Settings.firstChangeAfter;
    }

    void StopMutationAndRestore()
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (matCount != 0) { SetImage(0); Play(Settings.returnClip); StartCoroutine(Flash(0.6f)); }
    }

    void MarkEvent() { lastEventTime = Time.time; }
    bool InGrace() { return Time.time - lastEventTime < Settings.graceAfterEvent; }

    int UnlockedVariants() { return Mathf.Clamp(1 + visitedIslands.Count, 1, materials.Count - 1); }

    IEnumerator Mutate()
    {
        Play(Settings.warnClip);
        yield return new WaitForSeconds(Settings.warnLead);
        while (InGrace()) yield return null;   // you just jumped or stepped on a bridge: wait

        int max = UnlockedVariants();
        int v = Random.Range(1, max + 1);
        if (max > 1 && v == lastVariant) v = (v % max) + 1;   // never the same variant twice in a row
        lastVariant = v;
        SetImage(v); Play(Settings.changeClip); StartCoroutine(Flash(1f));

        yield return new WaitForSeconds(Random.Range(Settings.variantDuration.x, Settings.variantDuration.y));

        SetImage(0); Play(Settings.returnClip); StartCoroutine(Flash(0.6f));
        nextChangeAt = movingTime + Random.Range(Settings.changeInterval.x, Settings.changeInterval.y);
        mutationRoutine = null;
    }

    // Called by the pedestal while it charges: shows every version quickly and ends on the original
    public override void Sweep(float duration)
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (sweepRoutine != null) StopCoroutine(sweepRoutine);
        sweepRoutine = StartCoroutine(SweepRoutine(duration));
    }

    IEnumerator SweepRoutine(float duration)
    {
        // Ends before the pedestal charge and goes back to the original silently,
        // so it doesn't cover the accept or reject sound
        float step = duration * Settings.sweepPortion / materials.Count;
        for (int i = 1; i < materials.Count; i++)
        {
            SetImage(i); Play(Settings.sweepTickClip);
            yield return new WaitForSeconds(step);
        }
        SetImage(0); StartCoroutine(Flash(1f));
        sweepRoutine = null;
    }

    void SetImage(int i)
    {
        matCount = i;
        memeRenderer.sharedMaterial = materials[i];
    }

    void Play(AudioClip clip) { if (clip != null) sfx.PlayOneShot(clip); }

    // Flash: raises the meme's emission and brings it down in 0.3 s
    IEnumerator Flash(float strength)
    {
        Color baseEmission = memeRenderer.sharedMaterial.GetColor("_EmissionColor");
        float t = 0f, dur = 0.3f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - t / dur;
            memeRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", Color.Lerp(baseEmission, Color.white * Settings.flashIntensity, k * k * strength));
            memeRenderer.SetPropertyBlock(mpb);
            yield return null;
        }
        memeRenderer.SetPropertyBlock(null);
    }

    //---- Effects while held / resting (before: Holding / NotHolding coroutines polled every second)

    void ApplyHoldingEffects()
    {
        if (ID == "1Aa") GetComponent<AudioReverbZone>().enabled = true;   // God: reverb effect

        // Unmute the holding sound loop
        transform.Find("Audio").GetComponent<AudioSource>().mute = false;
        GetComponent<AudioSource>().mute = true;
    }

    void ApplyRestingEffects()
    {
        // Everything back to normal when you no longer have the orb
        if (ID == "1Aa") GetComponent<AudioReverbZone>().enabled = false;   // God: reverb off

        transform.Find("Audio").GetComponent<AudioSource>().mute = true;
        GetComponent<AudioSource>().mute = false;

        VideoPlayer video = GetComponentInChildren<VideoPlayer>();
        if (video != null)
        {
            video.frame = 0;
            video.Pause();
        }
    }
}
