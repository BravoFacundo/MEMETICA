using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// A meme that changes: the rules every representation shares (MemeOrb, MemeWindow). Meme (the base) detects what
// happens; this class decides which change shows and when; each representation only draws it (the abstract methods).
// Its meme (images, sounds) is a MemeData (Meme: Data).
//   Move     carried onto an island other than the last one: it changes, to the next variant of its list (1, 2…; the
//            tutorial doesn't count), or to one of them once all were shown. Moving around, now and then too
//   Fall     in your hand: as soon as nothing is below you (you will reappear) it turns into its Fall change, and
//            when you land after reappearing it goes back. Alone (meteor): it changes in front of you as you come close
//   Leave    you leave it (not on a pedestal): once you have been on another island and come back, it changes in
//            front of you as you come close
//   Show     on a white pedestal: every image quickly, ending on the base one (the sweep). Fall and Leave only once
//            you have seen them change
// Whenever you find it again after losing sight of it (meteor, left behind) it is its base image: a change only
// happens once you see it, and every change goes back to the base image.
// Every change happens under a cover (the orb's mist, the window's loading): it comes up and hides the image, the
// image swaps at its thickest, and it clears. An image can be a video: a VideoPlayer made here plays it.
// Empty (Can Be Empty, the window): no meme until SetMeme gives it one (RandomizerPedestal's raffle); until then it is
// only carried. SetMeme starts the new meme from scratch (its Move list from 1).
// Picking it up plays its Grab Sound. Its change sound, the first that exists: the image's own (each Move its own) →
// the meme's → MemeSettings' fallback; a looping one plays while the change shows, and a new change cuts it.
// Timing and transition sounds: MemeSettings (shared by every meme).
public abstract class MemeChanger : Meme
{
    private readonly List<MemeImage> images = new List<MemeImage>();   // 0 the base image, then its Move variants
    private MemeImage shown;                // the image showing now
    private VideoPlayer videoPlayer;        // plays the shown image's video (made on the first one)
    private bool showingVideoFrame;         // showing the video's frames (not its picture)
    private Texture videoTexture;           // the video texture it shows
    private bool videoStarted;              // picked up once (MemeData Video Start = On Pick Up: till then, still)
    private bool VideoWaits => !videoStarted && Data != null && Data.videoStart == MemeData.VideoStart.OnPickUp;
    private bool hasMeme;                   // off: no MemeData, a plain carryable meme
    private AudioSource sfx;                // transition sounds (warning, change, return, sweep ticks, grab)
    private AudioSource memeSource;         // the meme's sound of the change showing: a new change cuts it
    private float movingTime;               // time moving with the meme in the hand
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
    private bool raffling;             // RandomizerPedestal is drawing its meme (on it)

    // A change waiting for the player to see it (fall alone, leave)
    private MemeImage pendingImage;
    private bool pendingNeedsAway, playerWentAway;
    private bool fallingWithPlayer;
    private bool fallSeen, leaveSeen;
    private readonly HashSet<int> movesSeen = new HashSet<int>();   // Changes Once: the Moves already shown
    private GameObject lastFallIsland;   // the island you fell from the last time its Fall change showed   // their change happened once: they join the sweep

    private bool showingStill;          // a video's kept frame (the sweep), its player stopped
    private float changeShownAt = -99f;
    private float fallEndsAt;           // when its Fall change's sound ends (landing doesn't cut it) // when the last change came up (the next one waits Min Change Show)
    private MemeImage sweepFinale;      // the sweep ends on this one, played (the target island's last Move)
    private readonly Dictionary<UnityEngine.Video.VideoClip, RenderTexture> stills =
        new Dictionary<UnityEngine.Video.VideoClip, RenderTexture>();   // a frame of each video already shown (the sweep shows it)

    private float cover;                // 0 clear, 1 the image is hidden
    private Coroutine coverClearing;

    private MemeImage BaseImage => images.Count > 0 ? images[0] : null;

    //---- What each representation draws

    // Its parts are there and ready (false: it logs why and stays a plain carryable meme)
    protected abstract bool SetUpLook();
    // A picture, or the video's current frame
    protected abstract void ShowTexture(Texture texture);
    // The cover over the image: 0 clear, 1 hidden. Rising: coming up toward a change; off: clearing after it
    protected abstract void SetCover(float amount, bool rising);
    // The flash after a change: 0 none, 1 full
    protected abstract void SetGlow(float amount);
    // An image was just shown (its title, for the window)
    protected virtual void OnImageShown(MemeImage image) { }
    // Whose image is showing during a raffle (another meme's: its Fill, its title), else null (this one's own)
    protected MemeData ImageOwner { get; private set; }
    // It may start with no meme (no warning): it waits for SetMeme
    protected virtual bool CanBeEmpty => false;
    // No meme: how it looks empty
    protected virtual void ShowEmpty() { }
    // Scene view: its meme's base image, to tell them apart
    protected abstract void PreviewInEditor(MemeData meme);

    private void Start()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 1f; sfx.minDistance = 4f;
        memeSource = gameObject.AddComponent<AudioSource>();
        memeSource.playOnAwake = false; memeSource.spatialBlend = 1f; memeSource.minDistance = 4f;
        fadingSource = gameObject.AddComponent<AudioSource>();   // (the cut one fades out here)
        fadingSource.playOnAwake = false; fadingSource.spatialBlend = 1f; fadingSource.minDistance = 4f;
        if (CurrentPlayer != null) lastPlayerPos = CurrentPlayer.transform.position;

        if (!SetUpLook()) return;
        if (Data == null && CanBeEmpty) { ShowEmpty(); return; }
        if (!LoadImages()) { Debug.LogWarning($"[{GetType().Name}] '{name}' has no meme (MemeData with a base image).", this); return; }
        SetImage(BaseImage);
    }

    // Its images from Data: the base one, then its Move variants. False: no meme to show
    bool LoadImages()
    {
        images.Clear();
        hasMeme = Data != null && MemeImage.Exists(Data.baseImage);
        if (!hasMeme) return false;
        images.Add(Data.baseImage);
        foreach (MemeImage variant in Data.variants) if (MemeImage.Exists(variant)) images.Add(variant);
        return true;
    }

    // No meme on it (empty window)
    public bool IsEmpty => !hasMeme;
    // A change, the sweep or the fall back still going on (PortalWindow waits for it before emptying it)
    public bool IsChanging => changeRoutine != null || revealRoutine != null || sweepRoutine != null;

    // Its Leave change (you forgot it and it came to you: PlayerGrab's Recall), right now, if it hasn't shown it yet. False: it
    // has none left to show
    public bool PendLeave()
    {
        if (!hasMeme || !Data.HasLeave || leaveSeen) return false;
        ShowNow(Data.leave);
        return true;
    }

    // Its Fall change right now, if it hasn't shown it yet (the recall)
    public void PendFall()
    {
        if (hasMeme && Data.HasFall && !fallSeen) ShowNow(Data.fall);
    }

    // A change at once: no teasing, no mist in (it comes flying to you already changed), back to the base after its sound
    void ShowNow(MemeImage image)
    {
        StopChanges();
        pendingImage = null;
        changeRoutine = StartCoroutine(InstantChange(image));
    }

    IEnumerator InstantChange(MemeImage image)
    {
        yield return ShownLongEnough();
        ClearCover();
        changeShownAt = Time.time;
        SetImage(image);
        Play(Settings.changeClip);
        float soundLength = PlayChange(image);
        if (image == Data.fall) fallSeen = true;
        if (image == Data.leave) leaveSeen = true;
        MemeDex.Report(Data, image);
        yield return new WaitForSeconds(soundLength > 0f ? Mathf.Max(0f, soundLength - Settings.mistOut)
                                                       : Random.Range(Settings.variantDuration.x, Settings.variantDuration.y));
        yield return CoverSwap(() => { ShowBase(); Play(Settings.returnClip); }, Settings.mistIn);
        nextChangeAt = movingTime + Random.Range(Settings.changeInterval.x, Settings.changeInterval.y);
        changeRoutine = null;
    }

    //---- Another meme

    // From now on it is this meme, from scratch: its base image, its Move list from 1, nothing pending.
    // Null: empty again
    public void SetMeme(MemeData meme)
    {
        StopChanges();
        if (sweepRoutine != null) { StopCoroutine(sweepRoutine); sweepRoutine = null; }
        if (videoPlayer != null) videoPlayer.Stop();
        if (memeSource != null) memeSource.Stop();
        ClearCover();
        pendingImage = null;
        fallingWithPlayer = fallSeen = leaveSeen = false;
        shownUnlocked = lastVariant = 0;
        IslandChanges = 0;
        visitedIslands.Clear();
        ClearReached();
        movesSeen.Clear();
        videoStarted = false;
        movingTime = 0f;
        nextChangeAt = Settings.firstChangeAfter;

        Data = meme;
        if (LoadImages()) SetImage(BaseImage);
        else ShowEmpty();
    }

    // The raffle (RandomizerPedestal): the candidates' base images go by, fast and then slower and slower,
    // a tick each, and it stops on the winner, which becomes its meme (SetMeme, its own sound and a flash).
    // Taken off the pedestal halfway: it stays as it was
    public IEnumerator Raffle(IList<MemeData> candidates, MemeData winner, float duration, AudioClip tick, AudioClip win)
    {
        StopChanges();
        if (sweepRoutine != null) { StopCoroutine(sweepRoutine); sweepRoutine = null; }
        memeSource.Stop();
        ClearCover();
        raffling = true;

        // How long each image stays: from 0.05 s to 0.45 s, easing out, filling the duration
        var steps = new List<float>();
        for (float total = 0f; total < duration; )
        {
            float step = Mathf.Lerp(0.05f, 0.45f, Mathf.Pow(total / Mathf.Max(duration, 0.01f), 2f));
            steps.Add(step); total += step;
        }

        int last = -1;
        for (int k = 0; k < steps.Count; k++)
        {
            if (!raffling) { ImageOwner = null; yield break; }
            bool final = k == steps.Count - 1;
            if (final || candidates.Count > 1)
            {
                int i = final ? -1 : NextCandidate(candidates, last, winner, k == steps.Count - 2);
                last = i;
                ImageOwner = final ? winner : candidates[i];
                SetImage(final ? winner.baseImage : candidates[i].baseImage);
            }
            else if (k % 2 == 0) ShowEmpty();   // only one left: it blinks in and out
            else { ImageOwner = winner; SetImage(winner.baseImage); }
            if (!final) Play(tick != null ? tick : Settings.sweepTickClip);   // (not ??: an empty Inspector field isn't a real null)
            yield return new WaitForSeconds(steps[k]);
        }

        raffling = false;
        ImageOwner = null;
        SetMeme(winner);
        Play(win != null ? win : Settings.changeClip);
        PlayGrab(winner.BaseSound);
        grabHeard = true;   // picking it up from the pedestal next: no sound again
        StartCoroutine(Flash(1f));
    }

    // A candidate other than the last one shown (and not the winner just before it lands, so it doesn't stop twice)
    static int NextCandidate(IList<MemeData> candidates, int last, MemeData winner, bool beforeFinal)
    {
        for (int tries = 0; tries < 8; tries++)
        {
            int i = Random.Range(0, candidates.Count);
            if (i == last) continue;
            if (beforeFinal && candidates[i] == winner && candidates.Count > 2) continue;
            return i;
        }
        return (last + 1) % candidates.Count;
    }

    // Taken off the pedestal mid-raffle: back to what it was (its meme, or empty)
    protected override void OnRemovedFromPedestal(Pedestal pedestal)
    {
        if (!raffling) return;
        raffling = false;
        if (hasMeme) ShowBase(); else ShowEmpty();
    }

    protected override void Update()
    {
        PauseSounds(UIManager.GameIsPaused);
        if (memeSource != null && memeSource.isPlaying) AudioManager.MemeSounding();   // the world goes quieter
        UpdateVideoSound();
        base.Update();
        if (!hasMeme) return;
        UpdateVideoFrame();
        UpdateMutation();   // the image mutates over time while you carry it and move
        UpdatePending();    // fall and leave changes wait until you see them
        if (fallingWithPlayer && CurrentPlayer != null && CurrentPlayer.isGrounded && !PlayerRespawn.Instance.FallingIntoVoid && !PlayerRespawn.Instance.Respawning)
            OnFallLanded();   // it wasn't the void after all (it landed on something): back too
    }

    //---- Meme events

    protected override void OnBridgeCrossed() => MarkEvent();   // a new bridge: don't mutate right there

    // Picked up: the meme's own sound (Base.mp3). Not over a change showing (its sound goes on to the end)
    protected override void OnPickedUp()
    {
        StartVideo();
        if (grabHeard) { grabHeard = false; return; }   // just won in the raffle: its sound was already heard
        if (pendingImage != null && (!pendingNeedsAway || playerWentAway)) return;   // a change about to show (its own sound): not both at once
        if (hasMeme && changeRoutine == null && revealRoutine == null)
        {
            if (BaseImage != null && BaseImage.videoSound && BaseImage.video != null) RestartVideo();   // its video's sound, from the start
            else PlayGrab(Data.BaseSound);
        }
    }

    // Fell into the void. With you: it turns into its Fall change as soon as nothing is below you, and stays so the
    // whole way down. Alone (meteor, just landed): its change waits until you come close
    protected override void OnFell(bool withPlayer)
    {
        if (!hasMeme) return;
        MarkEvent();
        if (!Data.HasFall) return;
        if (ChangesOnce && fallSeen) return;   // (once)
        if (!withPlayer) { SetPending(Data.fall, false); return; }

        // Falling again from the same island: no Fall change (only the landing sound differs). From another one: yes
        GameObject island = CurrentPlayer != null ? PlayerRespawn.Instance.CurrentIsland : null;
        if (island != null && island == lastFallIsland) return;
        lastFallIsland = island;

        StopChanges();
        fallingWithPlayer = true;
        changeRoutine = StartCoroutine(FallChange());
    }

    IEnumerator FallChange()
    {
        yield return ShownLongEnough();   // (a Leave just before it: seen and heard first)
        yield return CoverSwap(() =>
        {
            SetImage(Data.fall); Play(Settings.changeClip);
            float soundLength = PlayChange(Data.fall);
            changeShownAt = Time.time;
            fallEndsAt = Time.time + (soundLength > 0f ? soundLength : Settings.minChangeShow);
            fallSeen = true;
            MemeDex.Report(Data, Data.fall);
        }, Settings.fallSwapTime);
        changeRoutine = null;
    }

    // The change showing now has been up Min Change Show seconds (else: wait the rest; it goes on meanwhile)
    IEnumerator ShownLongEnough()
    {
        while (Time.time - changeShownAt < Settings.minChangeShow) yield return null;
    }

    // In your hand, the fall is over (you reappeared, landed and got up): back to its base image
    protected override void OnFallLanded()
    {
        if (!fallingWithPlayer) return;
        fallingWithPlayer = false;
        StopChanges();
        changeRoutine = StartCoroutine(BackAfterFall());
    }

    // Landed before its Fall played out (a short fall, the portal): it plays to the end first
    IEnumerator BackAfterFall()
    {
        while (Time.time < fallEndsAt - Settings.mistOut) yield return null;
        yield return CoverSwap(() => { ShowBase(); Play(Settings.returnClip); }, Settings.mistIn);
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
        if (!hasMeme || revealRoutine != null || fallingWithPlayer) return;
        if (ChangesOnce) { ShowMoveOf(island); return; }
        if (!counts) return;
        int unlocked = UnlockedVariants();
        if (unlocked < 1) return;
        int v = unlocked > shownUnlocked ? unlocked : RandomVariant(unlocked);
        shownUnlocked = unlocked;
        lastVariant = v;
        ShowChange(images[v]);
    }

    // Changes Once (Game): every change shows only once per meme (Fall, Leave, each Move), and no random mutations.
    // The Move an island gives comes from its number: Island_11 → Move 1, Island_12 → 2, Island_13 → 3 (its last digit).
    // The last Move isn't an island's: it comes out at the end of the sweep, placed on the white pedestal of the target
    // island (named as the meme's axis: MemeData > Meme > Title: TrollFace → Island_TrollFace; case and spaces don't
    // matter): End Sweep On Last Move. Any other name (Island_HUB, Island_Tutorial, Island_1A optional) → none.
    // The hub, the tutorial and optional islands (Lift: Role) never give one. Going back: nothing again.
    // OriginalGame (MemeOrb): as before (one more for the random pick on each new island)
    protected virtual bool ChangesOnce => false;

    void ShowMoveOf(GameObject island)
    {
        if (island == null || !Meme.Unlocks(island)) return;
        int moves = Mathf.Min(Data.variants.Count, MemeDex.Instance != null ? MemeDex.Instance.movesPerAxis : 4);
        int move = MoveOf(island.name);   // (the target island: none here, its pedestal gives the last one)
        if (move < 1 || move >= moves || !MemeImage.Exists(Data.variants[move - 1]) || !movesSeen.Add(move)) return;
        ShowChange(Data.variants[move - 1]);
    }

    // The last Move: on the target island's white pedestal (KeyPedestal, its Key) the sweep ends on it instead of the
    // base image, and it plays there (its video, its sound, into the MemeDex) until its sound ends, then back to the
    // base. Call it before Sweep. False: anywhere else, or already seen (the sweep ends on the base as always)
    public bool EndSweepOnLastMove(GameObject island)
    {
        if (!ChangesOnce || !hasMeme || Data == null || island == null || !IsTarget(island.name)) return false;
        int moves = Mathf.Min(Data.variants.Count, MemeDex.Instance != null ? MemeDex.Instance.movesPerAxis : 4);
        if (moves < 1 || !MemeImage.Exists(Data.variants[moves - 1]) || !movesSeen.Add(moves)) return false;
        sweepFinale = Data.variants[moves - 1];
        return true;
    }

    // Until the sweep is over (its last Move goes on playing)
    public IEnumerator WaitForSweepEnd()
    {
        while (sweepRoutine != null) yield return null;
    }

    // Named as its axis: Island_<Title>
    bool IsTarget(string islandName)
    {
        if (Data == null || string.IsNullOrEmpty(Data.title)) return false;
        string id = islandName.Substring(islandName.LastIndexOf('_') + 1);
        return string.Equals(id.Replace(" ", ""), Data.title.Replace(" ", ""), System.StringComparison.OrdinalIgnoreCase);
    }

    // From its name, after the last '_': only digits → the last one; anything else → 0 (none)
    public static int MoveOf(string islandName)
    {
        string id = islandName.Substring(islandName.LastIndexOf('_') + 1).Trim();
        if (id.Length == 0) return 0;
        foreach (char c in id) if (!char.IsDigit(c)) return 0;
        return id[id.Length - 1] - '0';
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

        if (ChangesOnce || !IsHeld || mutationRoutine != null || changeRoutine != null || revealRoutine != null || fallingWithPlayer || UnlockedVariants() < 1) return;

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
        // Game: a change showing goes on to its end (its sound too) and then back to the base, wherever you left it
        // (a black pedestal…). OriginalGame: back to the base at once
        if (!ChangesOnce || (changeRoutine == null && revealRoutine == null)) StopMutationAndRestore();
        bool optional = MemeIsland != null && Lift.RoleOf(MemeIsland.transform) == Lift.Role.Optional;
        if (Data.HasLeave && CurrentPedestal == null && !optional && !(ChangesOnce && leaveSeen)) SetPending(Data.leave, true);   // (not in an optional course)
    }

    void StopMutationAndRestore()
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (shown != BaseImage) { ShowBase(); Play(Settings.returnClip); StartCoroutine(Flash(0.6f)); }
        ClearCover();
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
        Play(Settings.warnClip);   // the cover coming up is the warning

        int v = RandomVariant(UnlockedVariants());
        lastVariant = v;
        yield return CoverSwap(() => { SetImage(images[v]); Play(Settings.changeClip); PlayChange(images[v]); }, Settings.mistIn);

        yield return new WaitForSeconds(Random.Range(Settings.variantDuration.x, Settings.variantDuration.y));

        yield return CoverSwap(() => { ShowBase(); Play(Settings.returnClip); }, Settings.mistIn);
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
        yield return ShownLongEnough();
        // The cover comes up slowly while the new image shows through now and then, faster and faster
        Play(Settings.warnClip);
        for (float t = 0f; t < Settings.revealTime; )
        {
            float p = t / Mathf.Max(Settings.revealTime, 0.01f);
            float wait = Mathf.Lerp(0.3f, 0.05f, p);
            SetCover(cover = Mathf.SmoothStep(0f, 1f, p), true);
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

    IEnumerator ChangeRoutine(MemeImage image, bool coverAlreadyUp = false)
    {
        if (!coverAlreadyUp) yield return ShownLongEnough();
        float soundLength = 0f;
        yield return CoverSwap(() =>
        {
            SetImage(image); Play(Settings.changeClip); soundLength = PlayChange(image);
            changeShownAt = Time.time;
            if (image == Data.fall) fallSeen = true;
            if (image == Data.leave) leaveSeen = true;
            MemeDex.Report(Data, image);   // Fall, Leave or a Move: into the MemeDex
        }, coverAlreadyUp ? 0f : Settings.mistIn);
        // Fall and Leave last as long as their sound (picked up meanwhile, it goes on to the end); the rest, Variant Duration
        bool untilSoundEnds = soundLength > 0f && (ChangesOnce || image == Data.fall || image == Data.leave);   // (Game: every change)
        yield return new WaitForSeconds(untilSoundEnds ? Mathf.Max(0f, soundLength - Settings.mistOut)
                                                       : Random.Range(Settings.variantDuration.x, Settings.variantDuration.y));
        yield return CoverSwap(() => { ShowBase(); Play(Settings.returnClip); }, Settings.mistIn);
        nextChangeAt = movingTime + Random.Range(Settings.changeInterval.x, Settings.changeInterval.y);
        changeRoutine = null;
    }

    //---- The cover

    // The cover comes up and hides the image, the swap happens at its thickest (with a flash), and it clears
    IEnumerator CoverSwap(System.Action swap, float inTime)
    {
        if (coverClearing != null) { StopCoroutine(coverClearing); coverClearing = null; }
        float from = cover;
        for (float t = 0f; t < inTime; t += Time.deltaTime) { SetCover(cover = Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, t / inTime)), true); yield return null; }
        SetCover(cover = 1f, true);
        swap();
        StartCoroutine(Flash(0.6f));
        yield return CoverTo(0f, Settings.mistOut);
    }

    IEnumerator CoverTo(float target, float time)
    {
        float from = cover;
        for (float t = 0f; t < time; t += Time.deltaTime) { SetCover(cover = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / time)), false); yield return null; }
        SetCover(cover = target, false);
    }

    // A change was cut halfway (picked up, placed, swept): the cover doesn't stay
    void ClearCover()
    {
        if (cover <= 0f) { SetCover(0f, false); return; }
        if (coverClearing != null) StopCoroutine(coverClearing);
        coverClearing = StartCoroutine(CoverTo(0f, Settings.mistOut));
    }

    //---- Sounds

    // The sound of the image it just turned into: its own (next to it in the MemeData); else one of its Fallback Sounds,
    // else its Fallback Sound; else MemeSettings' fallback. Returns its length (0: none, or a loop)
    float PlayChange(MemeImage image)
    {
        if (image.video != null && image.videoSound) { FadeOutMemeSound(); return (float)image.video.length; }   // its video's own sound
        MemeSound sound = image.sound != null ? new MemeSound { clip = image.sound, loop = image.loop }   // its own, next to it
                        : Pick(Data.fallbackSounds)
                        ?? (Data.fallbackSound != null ? new MemeSound { clip = Data.fallbackSound } : null);
        if (sound == null)
        {
            AudioClip fallback = PickClip(Settings.fallbackSounds);
            if (fallback != null) sound = new MemeSound { clip = fallback };
        }
        if (sound == null) return 0f;
        // One at a time: the previous change's sound fades out quickly (hopping islands fast doesn't pile them up)
        FadeOutMemeSound();
        memeSource.clip = sound.clip;
        memeSource.loop = sound.loop;   // a loop lasts until it goes back (ShowBase)
        memeSource.Play();
        if (DebugTools.LogMemeSounds) Debug.Log($"[Meme sound] {name}: {sound.clip.name} (change: {(image == Data.fall ? "Fall" : image == Data.leave ? "Leave" : "Move")}{(sound.loop ? ", loop" : "")}) frame {Time.frameCount}", this);
        return sound.loop ? 0f : sound.clip.length;
    }

    // The sound playing now goes on in the other source, fading out in Sound Cut Fade (not cut dry)
    private AudioSource fadingSource;
    private Coroutine fading;
    void FadeOutMemeSound()
    {
        if (fadingSource == null || !memeSource.isPlaying || Settings.soundCutFade <= 0f) { memeSource.Stop(); return; }
        if (fading != null) StopCoroutine(fading);
        fadingSource.Stop();
        fadingSource.clip = memeSource.clip;
        fadingSource.loop = false;
        fadingSource.volume = memeSource.volume;
        fadingSource.Play();
        fadingSource.timeSamples = Mathf.Min(memeSource.timeSamples, memeSource.clip.samples - 1);
        memeSource.Stop();
        fading = StartCoroutine(FadeOut(fadingSource, Settings.soundCutFade));
    }

    IEnumerator FadeOut(AudioSource source, float seconds)
    {
        float from = source.volume;
        for (float t = 0f; t < seconds; t += Time.deltaTime) { source.volume = Mathf.Lerp(from, 0f, t / seconds); yield return null; }
        source.Stop();
        source.volume = from;
        fading = null;
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

    // The meme's own sound (picked up, won in the raffle): not again while it is still sounding (picked up right after
    // the raffle, or picked up and dropped quickly)
    private float grabSoundEnds;
    private bool grabHeard;   // won in the raffle: the next pick up is silent
    void PlayGrab(AudioClip clip, [System.Runtime.CompilerServices.CallerMemberName] string why = "")
    {
        if (clip == null || Time.time < grabSoundEnds) return;
        grabSoundEnds = Time.time + clip.length;
        AudioManager.MemeSounding(clip.length);
        Play(clip, why);
    }

    void Play(AudioClip clip, [System.Runtime.CompilerServices.CallerMemberName] string why = "")
    {
        if (clip == null) return;
        sfx.PlayOneShot(clip);
        if (DebugTools.LogMemeSounds) Debug.Log($"[Meme sound] {name}: {clip.name} ({why}) frame {Time.frameCount}", this);
    }

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
        ClearCover();
        if (sweepRoutine != null) StopCoroutine(sweepRoutine);
        sweepRoutine = StartCoroutine(SweepRoutine(duration));
    }

    IEnumerator SweepRoutine(float duration)
    {
        // Ends before the pedestal charge and goes back to the base image silently,
        // so it doesn't cover the accept or reject sound
        // Its variants (Game: only the Moves already seen: the last one comes out at the end), and fall / leave once seen
        var shownInSweep = new List<MemeImage>();
        if (!ChangesOnce) shownInSweep.AddRange(images.GetRange(1, images.Count - 1));
        else for (int i = 0; i < Data.variants.Count; i++) if (movesSeen.Contains(i + 1) && MemeImage.Exists(Data.variants[i])) shownInSweep.Add(Data.variants[i]);
        if (Data.HasFall && fallSeen) shownInSweep.Add(Data.fall);
        if (Data.HasLeave && leaveSeen) shownInSweep.Add(Data.leave);

        float step = duration * Settings.sweepPortion / Mathf.Max(shownInSweep.Count + 1, 1);
        foreach (MemeImage image in shownInSweep)
        {
            SetImage(image, true); Play(Settings.sweepTickClip);   // (a video: a frame of it, at once)
            yield return new WaitForSeconds(step);
        }
        MemeImage finale = sweepFinale;
        sweepFinale = null;
        if (finale != null) changeRoutine = StartCoroutine(Finale(finale));   // (set before the sweep is over: no gap)
        else { ShowBase(); StartCoroutine(Flash(1f)); }
        sweepRoutine = null;
    }

    // The sweep ends on it: no cover, it just stays and plays, then back to the base
    IEnumerator Finale(MemeImage image)
    {
        SetImage(image); Play(Settings.changeClip);
        float soundLength = PlayChange(image);
        changeShownAt = Time.time;
        MemeDex.Report(Data, image);
        StartCoroutine(Flash(1f));
        yield return new WaitForSeconds(soundLength > 0f ? Mathf.Max(0f, soundLength - Settings.mistOut)
                                                       : Random.Range(Settings.variantDuration.x, Settings.variantDuration.y));
        yield return CoverSwap(() => { ShowBase(); Play(Settings.returnClip); }, Settings.mistIn);
        changeRoutine = null;
    }

    //---- The image

#if UNITY_EDITOR
    private void OnValidate() => UnityEditor.EditorApplication.delayCall += ShowBaseInEditor;

    void ShowBaseInEditor()
    {
        if (this == null || Application.isPlaying) return;
        PreviewInEditor(data);
    }
#endif

    // The pause stops its video and its sounds where they are; going back, they go on from there
    private bool soundsPaused, videoWasPlaying;
    void PauseSounds(bool paused)
    {
        if (paused == soundsPaused) return;
        soundsPaused = paused;
        if (paused)
        {
            videoWasPlaying = videoPlayer != null && videoPlayer.isPlaying;
            if (videoWasPlaying) videoPlayer.Pause();
            if (memeSource != null) memeSource.Pause();
            if (fadingSource != null) fadingSource.Pause();
            if (sfx != null) sfx.Pause();
        }
        else
        {
            if (videoWasPlaying && videoPlayer != null) videoPlayer.Play();
            if (memeSource != null) memeSource.UnPause();
            if (fadingSource != null) fadingSource.UnPause();
            if (sfx != null) sfx.UnPause();
        }
    }

    // Picked up (or collected: DexCollectible): a video still on its first frame plays from there
    public void StartVideo()
    {
        videoStarted = true;
        if (videoPlayer != null && videoPlayer.isPaused && shown != null && shown.video != null) videoPlayer.Play();
    }

    // Its picture now; its video (if any) plays and takes over from its first frame. Still (the sweep): a video shows a
    // frame of it already seen at once, and doesn't play (each image there lasts a blink)
    void SetImage(MemeImage image, bool still = false)
    {
        if (!MemeImage.Exists(image)) return;
        shown = image;
        showingVideoFrame = false;
        RenderTexture frame = image.video != null && image.picture == null && stills.TryGetValue(image.video, out RenderTexture f) ? f : null;
        if (image.picture == null && frame == null && videoTexture != null && shownTexture == videoTexture) HoldFrame();   // (not black meanwhile)
        if (image.picture != null) Display(image.picture);
        else if (frame != null) Display(frame);
        OnImageShown(image);
        showingStill = still && frame != null;
        if (showingStill) { if (videoPlayer != null) videoPlayer.Stop(); return; }

        if (image.video == null) { if (videoPlayer != null) videoPlayer.Stop(); return; }
        if (videoPlayer == null) videoPlayer = CreateVideoPlayer();
        VideoAudioOutputMode audio = image.videoSound ? VideoAudioOutputMode.AudioSource : VideoAudioOutputMode.None;
        if (videoPlayer.clip != image.video || videoPlayer.audioOutputMode != audio)
        {
            videoPlayer.Stop();
            videoPlayer.clip = image.video;
            videoPlayer.audioOutputMode = audio;
            if (image.videoSound) { videoPlayer.EnableAudioTrack(0, true); videoPlayer.SetTargetAudioSource(0, videoAudio); }
        }
        videoPlayer.Play();
    }

    // The video's frames once it has one (until then, its picture)
    void UpdateVideoFrame()
    {
        if (shown == null || shown.video == null || videoPlayer == null || showingStill) return;
        // Showing it: the build's player may hand a new texture later (the old one would stay black): follow it
        if (showingVideoFrame)
        {
            if (videoPlayer.texture != null && videoPlayer.texture != videoTexture) Display(videoTexture = videoPlayer.texture);
            if (videoPlayer.frame >= StillFrame) KeepStill(shown.video, videoPlayer.texture);   // (well into it: not a black frame)
            return;
        }
        // Its first frames come black (the decoder starting): until then the picture before stays, never black
        if (!videoPlayer.isPlaying || videoPlayer.frame < FirstShownFrame || videoPlayer.texture == null) return;
        Display(videoTexture = videoPlayer.texture);
        showingVideoFrame = true;
        if (VideoWaits) videoPlayer.Pause();   // its first frame, until picked up
    }

    // The video frame showing now, copied: switching clips blanks the player's own texture, and it is still on screen
    private RenderTexture held;
    private Texture shownTexture;
    void HoldFrame()
    {
        if (held == null || held.width != videoTexture.width || held.height != videoTexture.height)
        {
            if (held != null) held.Release();
            held = new RenderTexture(videoTexture.width, videoTexture.height, 0) { name = name + " (held frame)" };
        }
        Graphics.Blit(videoTexture, held);
        Display(held);
    }

    // Every texture shown goes through here (so it knows what is on screen)
    void Display(Texture texture)
    {
        shownTexture = texture;
        ShowTexture(texture);
    }

    const int FirstShownFrame = 3;   // the first video frame it shows (before: black while it starts)
    const int StillFrame = 10;       // the frame it keeps for the sweep

    // A frame of this video, kept (the sweep shows it without playing it)
    void KeepStill(UnityEngine.Video.VideoClip clip, Texture texture)
    {
        if (clip == null || texture == null || stills.ContainsKey(clip)) return;
        var copy = new RenderTexture(texture.width, texture.height, 0) { name = clip.name + " (still)" };
        Graphics.Blit(texture, copy);
        stills[clip] = copy;
    }

    void OnDestroy()
    {
        foreach (RenderTexture rt in stills.Values) if (rt != null) rt.Release();
        stills.Clear();
        if (held != null) held.Release();
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
        videoAudio = gameObject.AddComponent<AudioSource>();   // its own sound (MemeImage Video Sound)
        videoAudio.playOnAwake = false; videoAudio.spatialBlend = 1f; videoAudio.minDistance = 4f;
        return player;
    }

    // Video Sound: the video's own sound. Heard during a change, or the base while you carry it (or ForceVideoSound:
    // DexCollectible); else muted (a meme lying around doesn't play its video's sound all the time)
    private AudioSource videoAudio;
    public bool ForceVideoSound { get; set; }
    bool VideoHeard => shown != BaseImage || IsHeld || ForceVideoSound;

    void UpdateVideoSound()
    {
        if (videoAudio == null) return;
        videoAudio.mute = !VideoHeard;
        if (!videoAudio.mute && videoAudio.isPlaying) AudioManager.MemeSounding();   // the world goes quieter
    }

    // From its start (the video's own sound with it): picked up with a Video Sound base, collected (DexCollectible)
    public void RestartVideo()
    {
        if (videoPlayer == null || shown == null || shown.video == null) return;
        videoPlayer.time = 0;
        videoPlayer.Play();
    }

    // Back to the base image: a looping change sound stops with it
    void ShowBase()
    {
        SetImage(BaseImage);
        if (memeSource != null && memeSource.loop && memeSource.isPlaying) memeSource.Stop();
    }

    // Flash: up at once, down in 0.3 s
    IEnumerator Flash(float strength)
    {
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.3f;
            SetGlow(k * k * strength);
            yield return null;
        }
        SetGlow(0f);
    }
}
