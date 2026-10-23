using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// Meme as an orb: a glass bubble (this object: Memetica/MemeOrb) with the meme inside as a sticker (child Sticker:
// Memetica/MemeSticker, one shared material; each orb shows its images on it through a property block). An image
// can be a video: a VideoPlayer made here plays it on the sticker (its picture, if any, until the first frame).
// Its meme (images, sounds) is a MemeData (Meme: Data, or a spare from a MemeGroup). Meme (the base) detects what
// happens; this class decides how each change looks and when it shows:
//   Move     carried onto an island other than the last one: it changes, to the next variant of its list (1, 2…; the
//            tutorial doesn't count), or to one of them once all were shown. Moving around, now and then too
//   Fall     in your hand: as soon as nothing is below you (you will reappear) it turns into its Fall change, and
//            when you land after reappearing it goes back.
//            Alone (meteor): it changes in front of you as you come close
//   Leave    you leave it (not on a pedestal): once you have been on another island and come back, it changes in
//            front of you as you come close
//   Show     on a white pedestal: every image quickly, ending on the base one (the sweep). Fall and Leave only once
//            you have seen them change
// Whenever you find it again after losing sight of it (meteor, left behind) it is its base image: a change only
// happens once you see it, and every change goes back to the base image.
// Every change happens inside a mist: a fog swirls and multiplies inside the bubble while the glass clouds over
// (_Frost); at its thickest the image swaps unseen, then both clear. Back to the base image the same way.
// Picking it up plays its Grab Sound. Its change sound, the first that exists: the image's own (each Move its own) → the meme's → MemeSettings' fallback. A looping one plays
// while the change shows. Timing and transition sounds: MemeSettings (shared by every orb).
public class MemeOrb : Meme
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly int FrostId = Shader.PropertyToID("_Frost");

    [Section("References")]
    [Tooltip("The image inside (child Sticker, with the shared MemeSticker material)")]
    public Renderer sticker;

    // Before MemeData: one material per image. Read once by Memetica > Memes: migrate orbs (temporary), then removed
    [HideInInspector] public List<Material> materials = new List<Material>();

    private readonly List<MemeImage> images = new List<MemeImage>();   // 0 the base image, then its Move variants
    private MemeImage shown;                // the image on the sticker now
    private VideoPlayer videoPlayer;        // plays the shown image's video (made on the first one)
    private bool showingVideoFrame;         // the sticker shows the video's frames (not its picture)
    private bool hasMeme;                   // off: no MemeData, a plain carryable orb
    private AudioSource sfx;                // transition sounds (warning, change, return, sweep ticks, grab)
    private AudioSource memeSource;         // the meme's sound of the change showing: a new change cuts it
    private MaterialPropertyBlock stickerBlock;
    private Color baseEmission;
    private float movingTime;               // time moving with the orb in the hand
    private float nextChangeAt;
    private float lastEventTime = -99f;
    private Vector3 lastPlayerPos;
    private bool wasHolded;
    private Coroutine mutationRoutine;
    private Coroutine sweepRoutine;
    private Coroutine changeRoutine;   // a change showing (island change, fall, leave)
    private Coroutine revealRoutine;   // a pending change happening in front of the player
    private int lastVariant;
    private int shownUnlocked;         // variants already shown on arriving to an island

    // A change waiting for the player to see it (fall alone, leave)
    private MemeImage pendingImage;
    private bool pendingNeedsAway, playerWentAway;
    private bool fallingWithPlayer;
    private bool fallSeen, leaveSeen;   // their change happened once: they join the sweep

    private Renderer shell;            // the bubble (its mist)
    private MaterialPropertyBlock shellBlock;
    private float mist;                // 0 clear, 1 the image is hidden
    private bool fogGathering;         // rising toward the change (wisps keep coming); off: they fade out fast
    private Coroutine mistClearing;
    private ParticleSystem innerFog;   // the fog inside the bubble
    private static Material fogMaterial;   // copy of MemeSettings' Fog Material, pulled toward the camera like the sticker

    private MemeImage BaseImage => images.Count > 0 ? images[0] : null;

    private void Start()
    {
        shell = GetComponent<Renderer>();
        shellBlock = new MaterialPropertyBlock();
        stickerBlock = new MaterialPropertyBlock();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 1f; sfx.minDistance = 4f;
        memeSource = gameObject.AddComponent<AudioSource>();
        memeSource.playOnAwake = false; memeSource.spatialBlend = 1f; memeSource.minDistance = 4f;
        if (CurrentPlayer != null) lastPlayerPos = CurrentPlayer.transform.position;

        if (sticker == null) { Debug.LogError($"[MemeOrb] '{name}' has no Sticker reference.", this); return; }
        sticker.transform.localScale = Vector3.one * Settings.stickerSize;
        baseEmission = sticker.sharedMaterial != null && sticker.sharedMaterial.HasProperty(EmissionId)
            ? sticker.sharedMaterial.GetColor(EmissionId) : Color.black;
        if (Data == null || !MemeImage.Exists(Data.baseImage)) { Debug.LogWarning($"[MemeOrb] '{name}' has no meme (MemeData with a base image).", this); return; }

        images.Add(Data.baseImage);
        foreach (MemeImage variant in Data.variants) if (MemeImage.Exists(variant)) images.Add(variant);
        hasMeme = true;
        SetImage(BaseImage);
    }

    protected override void Update()
    {
        base.Update();
        if (!hasMeme) return;
        UpdateVideoFrame();
        UpdateMutation();   // the image mutates over time while you carry it and move
        UpdatePending();    // fall and leave changes wait until you see them
        if (fallingWithPlayer && CurrentPlayer != null && CurrentPlayer.isGrounded && !CurrentPlayer.FallingIntoVoid)
            OnFallLanded();   // it wasn't the void after all (it landed on something): back too
    }

    //---- Meme events

    protected override void OnBridgeCrossed() => MarkEvent();   // a new bridge: don't mutate right there

    // Picked up: the meme's own sound (Base.mp3)
    protected override void OnPickedUp()
    {
        if (hasMeme) Play(Data.grabSound);
    }

    // Fell into the void. With you: it turns into its Fall change as soon as nothing is below you, and stays so the
    // whole way down. Alone (meteor, just landed): its change waits until you come close
    protected override void OnFell(bool withPlayer)
    {
        if (!hasMeme) return;
        MarkEvent();
        if (!Data.HasFall) return;
        if (!withPlayer) { SetPending(Data.fall, false); return; }

        StopChanges();
        fallingWithPlayer = true;
        changeRoutine = StartCoroutine(MistSwap(() =>
        {
            SetImage(Data.fall); Play(Settings.changeClip); PlayChange(Data.fall);
            fallSeen = true;
        }, Settings.mistIn));
    }

    // In your hand, the fall is over (you reappeared and touched the ground): back to its base image
    protected override void OnFallLanded()
    {
        if (!fallingWithPlayer) return;
        fallingWithPlayer = false;
        StopChanges();
        changeRoutine = StartCoroutine(BackAfterFall());
    }

    IEnumerator BackAfterFall()
    {
        yield return MistSwap(() => { ShowBase(); Play(Settings.returnClip); }, Settings.mistIn);
        nextChangeAt = movingTime + Random.Range(Settings.changeInterval.x, Settings.changeInterval.y);
        changeRoutine = null;
    }

    void StopChanges()
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (changeRoutine != null) { StopCoroutine(changeRoutine); changeRoutine = null; }
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
    }

    // Carried onto an island other than the last one: it changes. To the next variant of its list, or, once all were
    // shown, to one of them (so every island change shows one, however many variants it has)
    protected override void OnIslandVisited(GameObject island, bool counts)
    {
        MarkEvent();
        if (!hasMeme || !counts || revealRoutine != null || fallingWithPlayer) return;
        int unlocked = UnlockedVariants();
        if (unlocked < 1) return;
        int v = unlocked > shownUnlocked ? unlocked : RandomVariant(unlocked);
        shownUnlocked = unlocked;
        lastVariant = v;
        ShowChange(images[v]);
    }

    //---- Mutation
    // The image doesn't depend on any single action, only on the time you move carrying the orb.
    // After each variant, and when you let it go, it goes back to the base image.
    // Each island change you carry it through unlocks one more variant for the random pick.

    void UpdateMutation()
    {
        Vector3 playerPos = CurrentPlayer != null ? CurrentPlayer.transform.position : lastPlayerPos;
        Vector3 delta = playerPos - lastPlayerPos; delta.y = 0f;
        lastPlayerPos = playerPos;

        // Moments the player could take as the cause of the change: no mutating near them
        if (delta.magnitude > 3f) MarkEvent();                                    // respawn / teleport
        if (CurrentPlayer != null && !CurrentPlayer.isGrounded) MarkEvent();      // jumping or falling

        if (IsHeld && !wasHolded) StartMutationTimer();
        if (!IsHeld && wasHolded) OnLetGo();
        wasHolded = IsHeld;

        if (!IsHeld || mutationRoutine != null || changeRoutine != null || revealRoutine != null || fallingWithPlayer || UnlockedVariants() < 1) return;

        if (delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f) > 0.5f) movingTime += Time.deltaTime; // only counts while moving
        if (movingTime >= nextChangeAt && !InGrace())
            mutationRoutine = StartCoroutine(Mutate());
    }

    void StartMutationTimer()
    {
        // Taken off the pedestal mid-sweep: back to the base image. A change happening goes on: you see it end in your hand
        if (sweepRoutine != null) { StopCoroutine(sweepRoutine); sweepRoutine = null; ShowBase(); }
        movingTime = 0f;
        nextChangeAt = Settings.firstChangeAfter;
    }

    // Let go: back to the base image now; its Leave change waits until you go to another island and come back
    // (on a pedestal the sweep takes over instead)
    void OnLetGo()
    {
        StopMutationAndRestore();
        if (Data.HasLeave && CurrentPedestal == null) SetPending(Data.leave, true);
    }

    void StopMutationAndRestore()
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (shown != BaseImage) { ShowBase(); Play(Settings.returnClip); StartCoroutine(Flash(0.6f)); }
        ClearMist();
    }

    void MarkEvent() { lastEventTime = Time.time; }
    bool InGrace() { return Time.time - lastEventTime < Settings.graceAfterEvent; }

    // How many Move variants it has unlocked: one per island change it was carried through, as many as it has
    int UnlockedVariants() => Mathf.Min(IslandChanges, images.Count - 1);

    // One of the first 'max' variants, never the last one shown again
    int RandomVariant(int max)
    {
        int v = Random.Range(1, max + 1);
        if (max > 1 && v == lastVariant) v = (v % max) + 1;
        return v;
    }

    IEnumerator Mutate()
    {
        while (InGrace()) yield return null;   // you just jumped or stepped on a bridge: wait
        Play(Settings.warnClip);   // the fog gathering is the warning

        int v = RandomVariant(UnlockedVariants());
        lastVariant = v;
        yield return MistSwap(() => { SetImage(images[v]); Play(Settings.changeClip); PlayChange(images[v]); }, Settings.mistIn);

        yield return new WaitForSeconds(Random.Range(Settings.variantDuration.x, Settings.variantDuration.y));

        yield return MistSwap(() => { ShowBase(); Play(Settings.returnClip); }, Settings.mistIn);
        nextChangeAt = movingTime + Random.Range(Settings.changeInterval.x, Settings.changeInterval.y);
        mutationRoutine = null;
    }

    //---- Changes that wait to be seen (fall alone, leave)

    void SetPending(MemeImage image, bool needsAway)
    {
        pendingImage = image;
        pendingNeedsAway = needsAway;
        playerWentAway = false;
    }

    void UpdatePending()
    {
        if (pendingImage == null || revealRoutine != null) return;
        if (CurrentPedestal != null) { pendingImage = null; return; }   // placed (black pedestals don't sweep): no change there

        if (pendingNeedsAway && !playerWentAway)
        {
            if (IsHeld) { pendingImage = null; return; }   // picked up again before going away: no leave change
            GameObject playerIsland = PlayerIsland;
            if (playerIsland != null && MemeIsland != null && playerIsland != MemeIsland) playerWentAway = true;
            return;
        }

        // In front of you as you come close, or in your hand if you grabbed it without looking
        if (IsHeld || PlayerSees(Settings.revealDistance))
        {
            revealRoutine = StartCoroutine(RevealRoutine(pendingImage));
            pendingImage = null;
        }
    }

    // The change happening: the new image flickers in faster and faster, then shows as any change does
    IEnumerator RevealRoutine(MemeImage image)
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
            SetImage(BaseImage); yield return new WaitForSeconds(wait);
            t += wait * (1f + Mathf.Lerp(0.3f, 0.8f, p));
        }
        yield return ChangeRoutine(image, true);
        revealRoutine = null;
    }

    //---- Showing a change

    // Shows a change for a moment (stopping any other), then back to the base image
    void ShowChange(MemeImage image)
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (changeRoutine != null) StopCoroutine(changeRoutine);
        changeRoutine = StartCoroutine(ChangeRoutine(image));
    }

    IEnumerator ChangeRoutine(MemeImage image, bool mistAlreadyUp = false)
    {
        yield return MistSwap(() =>
        {
            SetImage(image); Play(Settings.changeClip); PlayChange(image);
            if (image == Data.fall) fallSeen = true;
            if (image == Data.leave) leaveSeen = true;
        }, mistAlreadyUp ? 0f : Settings.mistIn);
        yield return new WaitForSeconds(Random.Range(Settings.variantDuration.x, Settings.variantDuration.y));
        yield return MistSwap(() => { ShowBase(); Play(Settings.returnClip); }, Settings.mistIn);
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
    float StickerDepthPull => sticker != null && sticker.sharedMaterial != null && sticker.sharedMaterial.HasProperty("_DepthPull")
        ? sticker.sharedMaterial.GetFloat("_DepthPull") : 0.2f;

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

    // A puff pops in small, swells, and shrinks away at the end (no fading, like the clouds)
    static AnimationCurve Puffy(float start, float swell, float peak) =>
        new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.25f, swell), new Keyframe(0.75f, peak), new Keyframe(1f, 0f));

    //---- Sounds

    // The sound of the image it just turned into: that Move's own (move_3.png → move_3.mp3), or one of the Fall /
    // Leave ones; else one of the meme's; else MemeSettings' fallback
    void PlayChange(MemeImage image)
    {
        MemeSound sound = image == Data.fall ? Pick(Data.fallSounds)
                        : image == Data.leave ? Pick(Data.leaveSounds)
                        : MoveSound(images.IndexOf(image));
        sound = sound ?? Pick(Data.sounds);
        if (sound == null)
        {
            AudioClip fallback = PickClip(Settings.fallbackSounds);
            if (fallback != null) sound = new MemeSound { clip = fallback };
        }
        if (sound == null) return;
        // One at a time: the previous change's sound stops (hopping islands fast doesn't pile them up)
        memeSource.Stop();
        memeSource.clip = sound.clip;
        memeSource.loop = sound.loop;   // a loop lasts until it goes back (ShowBase)
        memeSource.Play();
    }

    // Move variant v (1, 2…) has the sound at v - 1 of Move Sounds, if any
    MemeSound MoveSound(int v)
    {
        MemeSound[] list = Data.moveSounds;
        return list != null && v >= 1 && v <= list.Length && list[v - 1] != null && list[v - 1].clip != null ? list[v - 1] : null;
    }

    static MemeSound Pick(MemeSound[] sounds)
    {
        if (sounds == null) return null;
        var valid = System.Array.FindAll(sounds, s => s != null && s.clip != null);
        return valid.Length > 0 ? valid[Random.Range(0, valid.Length)] : null;
    }

    static AudioClip PickClip(AudioClip[] clips)
    {
        if (clips == null) return null;
        var valid = System.Array.FindAll(clips, c => c != null);
        return valid.Length > 0 ? valid[Random.Range(0, valid.Length)] : null;
    }

    void Play(AudioClip clip) { if (clip != null) sfx.PlayOneShot(clip); }

    //---- Show (pedestal)

    // Called by the pedestal while it charges: shows every version quickly and ends on the base image
    public override void Sweep(float duration)
    {
        if (!hasMeme) return;
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (changeRoutine != null) { StopCoroutine(changeRoutine); changeRoutine = null; }
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
        pendingImage = null;
        memeSource.Stop();
        ClearMist();
        if (sweepRoutine != null) StopCoroutine(sweepRoutine);
        sweepRoutine = StartCoroutine(SweepRoutine(duration));
    }

    IEnumerator SweepRoutine(float duration)
    {
        // Ends before the pedestal charge and goes back to the base image silently,
        // so it doesn't cover the accept or reject sound
        var shownInSweep = new List<MemeImage>(images.GetRange(1, images.Count - 1));   // its variants, and fall / leave once seen
        if (Data.HasFall && fallSeen) shownInSweep.Add(Data.fall);
        if (Data.HasLeave && leaveSeen) shownInSweep.Add(Data.leave);

        float step = duration * Settings.sweepPortion / Mathf.Max(shownInSweep.Count + 1, 1);
        foreach (MemeImage image in shownInSweep)
        {
            SetImage(image); Play(Settings.sweepTickClip);
            yield return new WaitForSeconds(step);
        }
        ShowBase(); StartCoroutine(Flash(1f));
        sweepRoutine = null;
    }

    //---- The sticker

#if UNITY_EDITOR
    // Scene view: the sticker shows its meme's base image, to tell the orbs apart
    private void OnValidate() => UnityEditor.EditorApplication.delayCall += ShowBaseInEditor;

    void ShowBaseInEditor()
    {
        if (this == null || Application.isPlaying || sticker == null) return;
        if (sticker.transform.localScale != Vector3.one * Settings.stickerSize)   // its size inside the bubble
        {
            UnityEditor.Undo.RecordObject(sticker.transform, "Sticker size");
            sticker.transform.localScale = Vector3.one * Settings.stickerSize;
        }
        MemeData meme = data;
        if (meme == null && reserveGroup != null && reserveSlot >= 1 && reserveSlot <= reserveGroup.reserve.Count)
            meme = reserveGroup.reserve[reserveSlot - 1];
        if (meme == null || meme.baseImage == null || meme.baseImage.picture == null) return;   // (a video-only base: no preview)
        var block = new MaterialPropertyBlock();
        sticker.GetPropertyBlock(block);
        block.SetTexture(MainTexId, meme.baseImage.picture);
        sticker.SetPropertyBlock(block);
    }
#endif

    // Its picture now; its video (if any) plays and takes over the sticker from its first frame
    void SetImage(MemeImage image)
    {
        if (!MemeImage.Exists(image)) return;
        shown = image;
        showingVideoFrame = false;
        if (image.picture != null) SetStickerTexture(image.picture);

        if (image.video == null) { if (videoPlayer != null) videoPlayer.Stop(); return; }
        if (videoPlayer == null) videoPlayer = CreateVideoPlayer();
        if (videoPlayer.clip != image.video) videoPlayer.clip = image.video;
        videoPlayer.Play();
    }

    // The video's frames on the sticker once it has one (until then, its picture)
    void UpdateVideoFrame()
    {
        if (showingVideoFrame || shown == null || shown.video == null || videoPlayer == null) return;
        if (!videoPlayer.isPlaying || videoPlayer.frame < 1 || videoPlayer.texture == null) return;
        SetStickerTexture(videoPlayer.texture);
        showingVideoFrame = true;
    }

    // Plays to a texture we read (no render texture), looping and silent (the change sounds are its sound)
    VideoPlayer CreateVideoPlayer()
    {
        VideoPlayer player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.skipOnDrop = true;
        return player;
    }

    void SetStickerTexture(Texture texture)
    {
        sticker.GetPropertyBlock(stickerBlock);
        stickerBlock.SetTexture(MainTexId, texture);
        sticker.SetPropertyBlock(stickerBlock);
    }

    // Back to the base image: a looping change sound stops with it
    void ShowBase()
    {
        SetImage(BaseImage);
        if (memeSource != null && memeSource.loop && memeSource.isPlaying) memeSource.Stop();
    }

    // Flash: raises the meme's emission and brings it down in 0.3 s
    IEnumerator Flash(float strength)
    {
        float t = 0f, dur = 0.3f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - t / dur;
            SetEmission(Color.Lerp(baseEmission, Color.white * Settings.flashIntensity, k * k * strength));
            yield return null;
        }
        SetEmission(baseEmission);
    }

    void SetEmission(Color color)
    {
        sticker.GetPropertyBlock(stickerBlock);
        stickerBlock.SetColor(EmissionId, color);
        sticker.SetPropertyBlock(stickerBlock);
    }
}
