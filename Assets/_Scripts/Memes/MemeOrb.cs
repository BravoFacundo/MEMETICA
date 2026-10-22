using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// Meme as an orb (both scenes). Before: OrbMemes. Its changes (a test of what MemeData will hold).
// Meme (the base) detects what happens; this class decides how each change looks and when it shows:
//   Move     carried to a new island: shows the variant it just unlocked. Moving around known islands, now and then
//            it turns into one of the unlocked variants (as always)
//   Fall     in your hand: it changes on the way down (starts when you brake, done when you land).
//            Alone (meteor, falling inside a MeteorRock that smokes where it lands): it changes in front of you as you
//            come close
//   Drop     you let it go (not on a pedestal): once you have been on another island and come back, it changes in
//            front of you as you come close
//   Show     on the pedestal: every image quickly, ending on the original (the sweep). Fall and drop only once
//            you have seen them change
// Every change happens inside a mist: a fog swirls and multiplies inside the bubble while the glass clouds over
// (shader Memetica/MemeOrb, _Frost); at its thickest the image swaps unseen, then both clear. Back to the original
// the same way.
// Its sound, the first that exists:
//   its own sounds → this meme's sounds → MemeSettings' fallback sounds (any meme can use them).
// Timing and transition sounds: MemeSettings (shared by every orb). Here only what is each orb's own.
public class MemeOrb : Meme
{
    public GameObject Player;

    [Section("Move")]
    [Tooltip("0 is the original; the rest are the variants it turns into while you move (one more per island you carry it to)")]
    public List<Material> materials = new List<Material>();
    public int matCount;
    [Tooltip("Sounds of its variants (empty: this meme's sounds, then the fallback)")]
    public AudioClip[] moveSounds;

    [Section("Fall")]
    [Tooltip("Its change after falling into the void. Empty: nothing happens")]
    public Material fallImage;
    public AudioClip[] fallSounds;

    [Section("Drop")]
    [Tooltip("Its change after you let it go and come back from another island. Empty: nothing happens")]
    public Material dropImage;
    public AudioClip[] dropSounds;

    [Section("Meme Sounds")]
    [Tooltip("This meme's sounds: for any change that has none of its own")]
    public AudioClip[] memeSounds;

    private MeshRenderer memeRenderer;
    private AudioSource sfx;
    private MaterialPropertyBlock mpb;
    private PlayerMove playerMove;
    private float movingTime;            // time moving with the orb in the hand
    private float nextChangeAt;
    private float lastEventTime = -99f;
    private Vector3 lastPlayerPos;
    private bool wasHolded;
    private Coroutine mutationRoutine;
    private Coroutine sweepRoutine;
    private Coroutine changeRoutine;   // a change showing (new island, fall, drop)
    private Coroutine revealRoutine;   // a pending change happening in front of the player
    private int lastVariant;
    private int shownUnlocked = 1;     // variants already shown (1 is unlocked from the start)

    // A change waiting for the player to see it (fall alone, drop)
    private Material pendingImage;
    private AudioClip[] pendingSounds;
    private bool pendingNeedsAway, playerWentAway;
    private bool fallingWithPlayer;
    private Renderer shell;             // the bubble (its mist)
    private MaterialPropertyBlock shellBlock;
    private float mist;                 // 0 clear, 1 the image is hidden
    private bool fogGathering;          // rising toward the change (wisps keep coming); off: they fade out fast
    private Coroutine mistClearing;
    private ParticleSystem innerFog;    // the fog inside the bubble
    private static Material fogMaterial;   // copy of MemeSettings' Fog Material, pulled toward the camera like the sticker
    private static readonly int FrostId = Shader.PropertyToID("_Frost");
    private bool fallSeen, dropSeen;   // their change happened once: they join the sweep

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
        shell = GetComponent<MeshRenderer>();
        shellBlock = new MaterialPropertyBlock();
        playerMove = Player.GetComponent<PlayerMove>();
        lastPlayerPos = Player.transform.position;

        ApplyRestingEffects();   // before: the Update polling did it on the first frame
    }

    protected override void Update()
    {
        base.Update();
        UpdateMutation();   // the image mutates over time while you carry it and move
        UpdatePending();    // fall and drop changes wait until you see them
    }

    //---- Meme events

    protected override void OnPickedUp() => ApplyHoldingEffects();
    protected override void OnDropped() => ApplyRestingEffects();
    protected override void OnBridgeCrossed() => MarkEvent();   // a new bridge: don't mutate right there

    // Fell into the void. With you: it changes on your way down. Alone (meteor, just landed by its rock): its change
    // waits until you come close
    protected override void OnFell(bool withPlayer)
    {
        MarkEvent();
        if (withPlayer) { fallingWithPlayer = fallImage != null; return; }
        if (fallImage != null) SetPending(fallImage, fallSounds, false);
    }

    // In your hand after a fall: braking starts the change, touching the ground makes sure it shows
    protected override void OnFallLanding(bool touchedGround)
    {
        if (!fallingWithPlayer) return;
        if (!touchedGround) { if (revealRoutine == null) revealRoutine = StartCoroutine(RevealRoutine(fallImage, fallSounds)); return; }
        fallingWithPlayer = false;
        if (revealRoutine == null && changeRoutine == null) ShowChange(fallImage, fallSounds);   // no braking: at once
    }

    // Carried to a new island: shows the variant it just unlocked. A known one: only a pause in the mutation
    protected override void OnIslandVisited(GameObject island, bool firstTime)
    {
        MarkEvent();
        if (!firstTime || materials.Count < 2 || revealRoutine != null) return;
        int unlocked = UnlockedVariants();
        if (unlocked <= shownUnlocked) return;
        shownUnlocked = unlocked;
        ShowChange(materials[unlocked], moveSounds);
    }

    //---- Mutation
    // The image doesn't depend on any single action, only on the time you move carrying the orb.
    // After each variant, and when you drop it, it goes back to the original.
    // Each new island you carry it to unlocks one more version for the random pick.

    void UpdateMutation()
    {
        Vector3 playerPos = Player.transform.position;
        Vector3 delta = playerPos - lastPlayerPos; delta.y = 0f;
        lastPlayerPos = playerPos;

        // Moments the player could take as the cause of the change: no mutating near them
        if (delta.magnitude > 3f) MarkEvent();                          // respawn / teleport
        if (playerMove != null && !playerMove.isGrounded) MarkEvent();  // jumping or falling

        if (IsHeld && !wasHolded) StartMutationTimer();
        if (!IsHeld && wasHolded) OnLetGo();
        wasHolded = IsHeld;

        if (!IsHeld || mutationRoutine != null || changeRoutine != null || revealRoutine != null || fallingWithPlayer || materials.Count < 2) return;

        if (delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f) > 0.5f) movingTime += Time.deltaTime; // only counts while moving
        if (movingTime >= nextChangeAt && !InGrace())
            mutationRoutine = StartCoroutine(Mutate());
    }

    void StartMutationTimer()
    {
        // Taken off the pedestal mid-sweep: back to the original. A change happening goes on: you see it end in your hand
        if (sweepRoutine != null) { StopCoroutine(sweepRoutine); sweepRoutine = null; SetImage(0); }
        movingTime = 0f;
        nextChangeAt = Settings.firstChangeAfter;
    }

    // Let go: back to the original now; its Drop change waits until you go to another island and come back
    // (on a pedestal the sweep takes over instead)
    void OnLetGo()
    {
        StopMutationAndRestore();
        if (dropImage != null && CurrentPedestal == null) SetPending(dropImage, dropSounds, true);
    }

    void StopMutationAndRestore()
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (matCount != 0) { SetImage(0); Play(Settings.returnClip); StartCoroutine(Flash(0.6f)); }
        ClearMist();
    }

    void MarkEvent() { lastEventTime = Time.time; }
    bool InGrace() { return Time.time - lastEventTime < Settings.graceAfterEvent; }

    int UnlockedVariants() { return Mathf.Clamp(1 + IslandsDiscovered, 1, materials.Count - 1); }

    IEnumerator Mutate()
    {
        while (InGrace()) yield return null;   // you just jumped or stepped on a bridge: wait
        Play(Settings.warnClip);   // the fog gathering is the warning

        int max = UnlockedVariants();
        int v = Random.Range(1, max + 1);
        if (max > 1 && v == lastVariant) v = (v % max) + 1;   // never the same variant twice in a row
        lastVariant = v;
        yield return MistSwap(() => { SetImage(v); Play(Settings.changeClip); Play(SoundFor(moveSounds)); }, Settings.mistIn);

        yield return new WaitForSeconds(Random.Range(Settings.variantDuration.x, Settings.variantDuration.y));

        yield return MistSwap(() => { SetImage(0); Play(Settings.returnClip); }, Settings.mistIn);
        nextChangeAt = movingTime + Random.Range(Settings.changeInterval.x, Settings.changeInterval.y);
        mutationRoutine = null;
    }

    //---- Changes that wait to be seen (fall alone, drop)

    void SetPending(Material image, AudioClip[] sounds, bool needsAway)
    {
        pendingImage = image;
        pendingSounds = sounds;
        pendingNeedsAway = needsAway;
        playerWentAway = false;
    }

    void UpdatePending()
    {
        if (pendingImage == null || revealRoutine != null) return;
        if (CurrentPedestal != null) { pendingImage = null; return; }   // placed (black pedestals don't sweep): no change there

        if (pendingNeedsAway && !playerWentAway)
        {
            if (IsHeld) { pendingImage = null; return; }   // picked up again before leaving: no drop change
            GameObject playerIsland = PlayerIsland;
            if (playerIsland != null && MemeIsland != null && playerIsland != MemeIsland) playerWentAway = true;
            return;
        }

        // In front of you as you come close, or in your hand if you grabbed it without looking
        if (IsHeld || PlayerSees(Settings.revealDistance))
        {
            revealRoutine = StartCoroutine(RevealRoutine(pendingImage, pendingSounds));
            pendingImage = null;
        }
    }

    // The change happening: the new image flickers in faster and faster, then shows as any change does
    IEnumerator RevealRoutine(Material image, AudioClip[] sounds)
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (changeRoutine != null) { StopCoroutine(changeRoutine); changeRoutine = null; }
        // The mist gathers slowly while the new image shows through now and then, faster and faster
        Play(Settings.warnClip);
        for (float t = 0f; t < Settings.revealTime; )
        {
            float p = t / Mathf.Max(Settings.revealTime, 0.01f);
            float wait = Mathf.Lerp(0.3f, 0.05f, p);
            fogGathering = true;
            SetMist(Mathf.SmoothStep(0f, 1f, p));
            SetImage(image); yield return new WaitForSeconds(wait * Mathf.Lerp(0.3f, 0.8f, p));
            SetImage(0); yield return new WaitForSeconds(wait);
            t += wait * (1f + Mathf.Lerp(0.3f, 0.8f, p));
        }
        yield return ChangeRoutine(image, sounds, true);
        revealRoutine = null;
    }

    // Low poly smoke: a solid puff pops in small, swells, and shrinks away at the end (no fading, like the clouds)
    static AnimationCurve Puffy(float start, float swell, float peak) =>
        new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.25f, swell), new Keyframe(0.75f, peak), new Keyframe(1f, 0f));

    //---- Showing a change

    // Shows a change for a moment (stopping any other), then back to the original
    void ShowChange(Material image, AudioClip[] sounds)
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (changeRoutine != null) StopCoroutine(changeRoutine);
        changeRoutine = StartCoroutine(ChangeRoutine(image, sounds));
    }

    IEnumerator ChangeRoutine(Material image, AudioClip[] sounds, bool mistAlreadyUp = false)
    {
        yield return MistSwap(() =>
        {
            SetImage(image); Play(Settings.changeClip); Play(SoundFor(sounds));
            if (image == fallImage) fallSeen = true;
            if (image == dropImage) dropSeen = true;
        }, mistAlreadyUp ? 0f : Settings.mistIn);
        yield return new WaitForSeconds(Random.Range(Settings.variantDuration.x, Settings.variantDuration.y));
        yield return MistSwap(() => { SetImage(0); Play(Settings.returnClip); }, Settings.mistIn);
        nextChangeAt = movingTime + Random.Range(Settings.changeInterval.x, Settings.changeInterval.y);
        changeRoutine = null;
    }

    //---- Mist (the bubble's shader)

    // The mist swirls up and hides the image, the swap happens at its thickest (with a flash), and it clears
    IEnumerator MistSwap(System.Action swap, float inTime)
    {
        if (mistClearing != null) { StopCoroutine(mistClearing); mistClearing = null; }
        fogGathering = true;
        float from = mist;
        for (float t = 0f; t < inTime; t += Time.deltaTime) { SetMist(Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, t / inTime))); yield return null; }
        SetMist(1f);
        swap();
        fogGathering = false;   // the wisps stop and fade out fast while the glass clears
        StartCoroutine(Flash(0.6f));
        yield return MistTo(0f, Settings.mistOut);
    }

    IEnumerator MistTo(float target, float time)
    {
        float from = mist;
        for (float t = 0f; t < time; t += Time.deltaTime) { SetMist(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / time))); yield return null; }
        SetMist(target);
    }

    // A change was cut halfway (picked up, placed, swept): the mist doesn't stay
    void ClearMist()
    {
        fogGathering = false;
        if (mist <= 0f) return;
        if (mistClearing != null) StopCoroutine(mistClearing);
        mistClearing = StartCoroutine(MistTo(0f, Settings.mistOut));
    }

    // How much the sticker is drawn toward the camera (MemeSticker's Depth Pull): the fog in front matches it
    float StickerDepthPull => memeRenderer != null && memeRenderer.sharedMaterial != null && memeRenderer.sharedMaterial.HasProperty("_DepthPull")
        ? memeRenderer.sharedMaterial.GetFloat("_DepthPull") : 0.2f;

    // Gathering: the wisps come from the start (with the warning) and multiply, the glass clouds over with them:
    // both are at their thickest right at the swap. After it: no new wisps, the ones there fade out fast, the glass clears
    void SetMist(float value)
    {
        mist = value;
        if (shell != null)
        {
            shell.GetPropertyBlock(shellBlock);
            shellBlock.SetFloat(FrostId, value * Mathf.Sqrt(value));
            shell.SetPropertyBlock(shellBlock);
        }

        if (value > 0f && innerFog == null) innerFog = CreateInnerFog();
        if (innerFog == null) return;
        var emission = innerFog.emission;
        emission.rateOverTime = fogGathering ? Settings.fogRate * Mathf.Sqrt(value) : 0f;
        var main = innerFog.main;
        main.simulationSpeed = fogGathering ? 1f : Settings.fogFadeSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.15f, 0.2f, value), Mathf.Lerp(0.22f, 0.32f, value));
        if (fogGathering && !innerFog.isPlaying) innerFog.Play();
    }

    // Flat cartoon puffs all through the bubble (front, back, sides; few in the middle, where the image is), swirling
    // around it in every direction. Its local space: they travel and turn with it. Drawn behind the glass
    ParticleSystem CreateInnerFog()
    {
        var go = new GameObject("InnerFog");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Settings.fogColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // sizes in the orb's units (its sphere is 1 wide)
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.38f;           // the orb's sphere is 0.5: with their size they stay inside the glass
        shape.radiusThickness = 0.45f;  // born in the outer part of that sphere, not crossing the image in the middle

        var velocity = ps.velocityOverLifetime;   // a slow whirl around the image
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalX = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);   // around every axis: a 3D swirl, not rings
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        velocity.radial = new ParticleSystem.MinMaxCurve(0f, 0f);

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        var color = ps.colorOverLifetime;   // a quick fade at both ends, solid in between
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, Puffy(0.3f, 1f, 1.1f));

        // Flat cartoon puffs (the orb is a flat drawing). Pulled toward the camera as much as the sticker is,
        // so the puffs in front of the image cover it
        if (fogMaterial == null && Settings.fogMaterial != null)
        {
            fogMaterial = new Material(Settings.fogMaterial) { name = "InnerFog" };
            fogMaterial.SetFloat("_DepthPull", StickerDepthPull);
        }
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = fogMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // The sound of a change: its own, else this meme's, else the fallback (random in each list)
    AudioClip SoundFor(AudioClip[] own) => Pick(own) ?? Pick(memeSounds) ?? Pick(Settings.fallbackSounds);

    static AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null) return null;
        int count = 0;
        foreach (AudioClip c in clips) if (c != null) count++;
        if (count == 0) return null;
        int pick = Random.Range(0, count);
        foreach (AudioClip c in clips) if (c != null && pick-- == 0) return c;
        return null;
    }

    //---- Show (pedestal)

    // Called by the pedestal while it charges: shows every version quickly and ends on the original
    public override void Sweep(float duration)
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (changeRoutine != null) { StopCoroutine(changeRoutine); changeRoutine = null; }
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
        pendingImage = null;
        ClearMist();
        if (sweepRoutine != null) StopCoroutine(sweepRoutine);
        sweepRoutine = StartCoroutine(SweepRoutine(duration));
    }

    IEnumerator SweepRoutine(float duration)
    {
        // Ends before the pedestal charge and goes back to the original silently,
        // so it doesn't cover the accept or reject sound
        var images = new List<Material>();   // its variants, and fall / drop once seen
        for (int i = 1; i < materials.Count; i++) images.Add(materials[i]);
        if (fallImage != null && fallSeen) images.Add(fallImage);
        if (dropImage != null && dropSeen) images.Add(dropImage);

        float step = duration * Settings.sweepPortion / Mathf.Max(images.Count + 1, 1);
        foreach (Material image in images)
        {
            SetImage(image); Play(Settings.sweepTickClip);
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

    // Any image (fall, drop): Mat Count is -1 when it isn't one of the Move list
    void SetImage(Material image)
    {
        matCount = materials.IndexOf(image);
        memeRenderer.sharedMaterial = image;
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
        // Unmute the holding sound loop
        transform.Find("Audio").GetComponent<AudioSource>().mute = false;
        GetComponent<AudioSource>().mute = true;
    }

    void ApplyRestingEffects()
    {
        // Everything back to normal when you no longer have the orb
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
