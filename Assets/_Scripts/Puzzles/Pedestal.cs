using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Pedestal: detecta el meme, lo encastra en el Socket, evalúa la llave y sube/baja islas y puentes.
// Works with any Meme (MemeOrb in Game_Old, MemeWindow in Game): it only talks to the Meme base class.
// Estructura del prefab:
//   Pedestal       :: Pedestal + AudioSource + SphereCollider (trigger, centrado en el Socket)
//   ├── Visual_Keyed     pedestal blanco (mármol, con llave)
//   ├── Visual_Keyless   pedestal negro (piedra, sin llave)
//   └── Socket           punto de encastre del meme + partículas
// Group: pedestals with the same Group ID don't evaluate alone; the group completes when all of them
// have a meme, and then each one raises what it controls (before: PedestalGroup).
// Placing: a meme in your hand goes in when you look at the pedestal's zone (the sphere around the Socket, within
// reach), not when it touches it (carried in a corner of the screen, it would get stolen from the edge of the view).
// A meme that falls or is thrown into the zone goes in on its own. The meme window carried in the HUD (Game) comes out
// of its HUD place and flies to the socket (Snap Fly Time), then stops dead there.
// With a MemeRandomizer it doesn't judge: it gives the meme window a meme (the randomizer's raffle) and that raises the way.
// With a PedestalRetire it is used once: right and its meme taken off, it goes into the ground (inactive) until it
// comes back (or for good).
[RequireComponent(typeof(AudioSource), typeof(SphereCollider))]
public class Pedestal : MonoBehaviour
{
    [Section("Key")]
    [Tooltip("Black (stone): takes any meme. White (marble): needs the key")]
    public bool KeylessPedestal = false;
    [Tooltip("The meme is right if its ID starts with this (Meme_3Aa → ID 3Aa)")]
    [ShowIf(nameof(KeylessPedestal), false)] public string KeyID;
    [Tooltip("White: the meme it waits for (one of our MemeData: the meme window has to be showing it). Set, Key ID isn't used")]
    [ShowIf(nameof(KeylessPedestal), false)] public MemeData keyMeme;
    [Tooltip("White, Game: right, the meme window empties (its meme is downloaded)")]
    [ShowIf(nameof(KeylessPedestal), false)] public bool drainMeme = false;

    [Section("What It Raises")]
    public string[] ControlledIslands;
    public string[] ControlledBridges;
    [Tooltip("One after the other, in list order: islands first (each after the previous arrives), then bridges. Off: all at once")]
    public bool inOrder = false;

    [Section("Group & Clue")]
    [Tooltip("Pedestals with the same Group ID complete together when all of them have a meme. Empty = no group")]
    public string groupID;
    [Tooltip("The painting (easel) that gives the clue for this pedestal (before: Atril)")]
    public Transform cluePainting;

    [Section("Sounds", Closed = true)]
    public AudioClip chargeClip;
    public AudioClip correctClip;
    public AudioClip incorrectClip;
    public AudioClip snapClip;

    [Tooltip("Game: seconds the meme window takes to fly from its HUD place to the socket")]
    public float snapFlyTime = 0.2f;

    [Section("Break (elevator pedestal)", Closed = true)]
    [Tooltip("Thud when it hits the ground on its side")]
    public AudioClip breakClip;
    [Tooltip("Seconds it takes to tip over (heavy stone: slow start, then it drops)")]
    public float toppleTime = 1.1f;
    [Tooltip("How fast it scrapes along the ground toward the edge after tipping over (m/s²)")]
    public float slideAcceleration = 2.5f;

    [Section("References", Closed = true)]
    public Transform socket;
    public GameObject visualKeyed;
    public GameObject visualKeyless;

    // Estado (solo lectura desde afuera)
    public GameObject MemeObject { get; private set; }
    public Meme CurrentMeme { get; private set; }   // null if the object has no Meme component
    public bool HasMeme => MemeObject != null;
    public bool IsCorrect { get; private set; }
    public bool IsGrouped => !string.IsNullOrEmpty(groupID);

    static readonly List<Pedestal> all = new List<Pedestal>();

    AudioSource audioSource;
    SphereCollider trigger;
    ParticleSystem particles;
    Coroutine evaluating;
    bool rejecting;
    bool broken;
    bool groupCompleted;
    GameObject releasedMeme;   // just taken off: not snapped again until it leaves the trigger
    MemeRandomizer randomizer;
    PedestalRetire retire;
    bool seating;              // the meme window flying from the HUD to the socket
    RigidbodyInterpolation seatedInterpolation = RigidbodyInterpolation.Interpolate;   // what it had before (given back when let go)

    static PlayerLook playerLook;
    // The player's camera (found by its PlayerLook, not by the MainCamera tag: other cameras may have it)
    static PlayerLook look => playerLook != null ? playerLook : (playerLook = FindObjectOfType<PlayerLook>());

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        trigger = GetComponent<SphereCollider>();
        particles = socket != null ? socket.GetComponent<ParticleSystem>() : null;
        randomizer = GetComponent<MemeRandomizer>();
        retire = GetComponent<PedestalRetire>();
        ApplyVisual();
        SyncTrigger();
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Start()
    {
        WarnIfNoClue();
    }

    // A white pedestal waits for a meme that has a painting (the clue); a meme without one belongs on black ones
    void WarnIfNoClue()
    {
        if (KeylessPedestal || randomizer != null) return;
        if (keyMeme != null) return;   // a MemeData: the meme window shows it when it carries it
        Meme key = Meme.WithKey(KeyID);
        if (key == null) Debug.LogWarning($"[Pedestal] '{name}': no meme with Key ID '{KeyID}' in the scene.", this);
        else if (key.Data != null && !key.Data.HasPainting)
            Debug.LogWarning($"[Pedestal] '{name}' waits for '{key.name}', whose meme '{key.Data.title}' has no painting (MemeData: Hints). " +
                             "Give it one, or make this pedestal black (Keyless).", this);
    }

    // The pedestal whose clue is this painting (or one of its parents), or null
    public static Pedestal WithClue(Transform painting)
    {
        foreach (Pedestal p in all)
            if (p.cluePainting != null && (p.cluePainting == painting || p.cluePainting.IsChildOf(painting))) return p;
        return null;
    }

    // MemeRecall's Snap In Range: carried within reach of a pedestal that waits for it (the randomizer, or a white one
    // whose Key Meme it shows), it goes in without looking. Once per pedestal (a retiring randomizer: each round)
    bool AutoSnap(GameObject inHand)
    {
        MemeRecall assist = MemeRecall.Instance;
        if (assist == null || !assist.AutoSnaps) return false;
        if (everRight && !(randomizer != null && retire != null)) return false;   // (a randomizer that goes down: every round it is back up)
        float radius = trigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        if (Vector3.Distance(look.transform.position, socket.position) > look.pickUpRange + radius) return false;
        if (randomizer != null) return true;
        return !KeylessPedestal && keyMeme != null && Fits(inHand);
    }
    bool everRight;   // it was right once (no more auto snapping)

    // Takes memes: not broken, and not in the ground (PedestalRetire)
    bool Usable => !broken && (retire == null || retire.IsUp);

    // ---------- Meme entra / sale ----------
    // Loose (falling, thrown) into the zone: in. In the hand: only by looking (Update)
    void OnTriggerEnter(Collider other)
    {
        if (!Usable || HasMeme || !other.CompareTag("Meme") || other.gameObject == releasedMeme) return;
        if (look.heldObject == other.gameObject) return;
        Snap(other.gameObject);
    }

    // The camera looks at the zone (the sphere around the Socket), within reach
    bool LookedAt()
    {
        Transform cam = look.transform;
        float radius = trigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        Vector3 toSocket = socket.position - cam.position;
        float along = Vector3.Dot(toSocket, cam.forward);
        if (along < 0f || along > look.pickUpRange + radius) return false;
        return (toSocket - cam.forward * along).sqrMagnitude <= radius * radius;
    }

    void Snap(GameObject memeObject)
    {
        bool fromHud = MemeDock.Instance != null && memeObject.TryGetComponent(out Meme inHud) && MemeDock.Instance.OnScreen(inHud);
        Vector3 size = look.SnapObject(memeObject, fromHud ? snapFlyTime : 0f);
        MemeObject = memeObject;   // taken: nothing else snaps in meanwhile
        if (fromHud) { seating = true; evaluating = StartCoroutine(FlyIn(memeObject, size)); return; }
        Seat(memeObject);
    }

    // From the HUD place to the socket, growing back to its size in the world; it stops dead there
    IEnumerator FlyIn(GameObject memeObject, Vector3 size)
    {
        if (memeObject.TryGetComponent(out Rigidbody flying)) flying.isKinematic = true;
        Transform t = memeObject.transform;
        Vector3 from = t.position, fromSize = t.localScale;
        for (float time = 0f; time < snapFlyTime; time += Time.deltaTime)
        {
            float p = 1f - (1f - time / snapFlyTime) * (1f - time / snapFlyTime);   // fast, then slowing down
            t.position = Vector3.Lerp(from, socket.position, p);
            t.localScale = Vector3.Lerp(fromSize, size, p);
            yield return null;
        }
        t.localScale = size;
        seating = false;
        Seat(memeObject);
    }

    void Seat(GameObject memeObject)
    {
        memeObject.transform.position = socket.position;
        memeObject.transform.SetParent(socket);   // moves with the pedestal (floating islands, elevators, the turning platform)
        if (memeObject.TryGetComponent(out Rigidbody body))
        {
            body.isKinematic = true;
            if (body.interpolation != RigidbodyInterpolation.None) seatedInterpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;   // (interpolated, it lags behind a pedestal that moves)
        }
        if (particles != null) particles.Play();

        MemeObject = memeObject;
        CurrentMeme = memeObject.GetComponent<Meme>();
        if (CurrentMeme != null) CurrentMeme.PlaceOnPedestal(this);
        Play(snapClip);
        evaluating = StartCoroutine(Evaluate());
    }

    // Taken off (picked up, pushed out by a rejection) is checked here, not with OnTriggerExit:
    // turning a meme's physics on or off makes Unity report a fake exit and entry at once
    // (placing it made a fake exit, picking it up made a fake entry that snapped it back)
    void Update()
    {
        if (look == null) return;
        if (releasedMeme != null && !InsideZone(releasedMeme) && !LookedAt()) releasedMeme = null;   // it really left: it can be placed again
        if (!HasMeme)
        {
            // In the hand: in when you look at the zone
            GameObject inHand = look.heldObject;
            if (Usable && inHand != null && inHand != releasedMeme && inHand.CompareTag("Meme") && (LookedAt() || AutoSnap(inHand))) Snap(inHand);
            return;
        }

        if (seating) return;   // still flying in
        bool held = CurrentMeme != null && CurrentMeme.IsHeld;
        bool movedAway = Vector3.Distance(MemeObject.transform.position, socket.position) > 0.5f;
        if (held || movedAway) Release();
    }

    void Release()
    {
        releasedMeme = MemeObject;   // not snapped again until it leaves the zone
        if (evaluating != null) StopCoroutine(evaluating);
        bool lowerBridges = randomizer == null && (KeylessPedestal || Fits(MemeObject));   // the randomizer's way stays
        bool done = IsCorrect;

        if (MemeDock.Instance == null || !MemeDock.Instance.OnScreen(CurrentMeme))   // (carried to the HUD: MemeDock takes care of it)
            if (MemeObject.TryGetComponent(out Rigidbody body)) body.interpolation = seatedInterpolation;
        if (CurrentMeme != null) CurrentMeme.RemoveFromPedestal(this);
        MemeObject = null;
        CurrentMeme = null;
        IsCorrect = false;
        if (!rejecting && !broken && !(done && retire != null)) Play(incorrectClip);   // si lo expulsamos nosotros, el "incorrecto" ya sonó
        rejecting = false;

        if (done && retire != null) { retire.Retire(); return; }   // used: into the ground, its way stays up
        if (lowerBridges && !broken) LowerBridges();
    }

    bool InsideZone(GameObject meme)
    {
        float radius = trigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        return Vector3.Distance(meme.transform.position, transform.TransformPoint(trigger.center)) < radius + 0.2f;
    }

    IEnumerator Evaluate()
    {
        yield return new WaitForSeconds(Length(snapClip));

        // Randomizer: the raffle, then right (its axis' way already rose)
        if (randomizer != null)
        {
            yield return randomizer.Draw(CurrentMeme);
            SetCorrect();
            yield break;
        }

        // Enlazado: carga en loop hasta que el grupo se complete
        if (IsGrouped)
        {
            Play(chargeClip, true);
            yield return new WaitForSeconds(Length(chargeClip));
            CompleteGroupIfFull();
            yield break;
        }

        Play(chargeClip);
        if (KeylessPedestal) { SetCorrect(); yield break; }

        // Blanco: barrido de imágenes mientras carga, después decide
        if (CurrentMeme != null) CurrentMeme.Sweep(Length(chargeClip));
        yield return new WaitForSeconds(Length(chargeClip));

        if (Fits(MemeObject)) SetCorrect();
        else { SetIncorrect(); Reject(); }
    }

    // The one it waits for: showing its Key Meme (if set), else its ID starts with Key ID
    bool Fits(GameObject memeObject)
    {
        if (keyMeme != null) return memeObject.TryGetComponent(out Meme meme) && meme.Data == keyMeme;
        return Meme.IDOf(memeObject).StartsWith(KeyID);
    }

    // ---------- Grupo ----------
    void CompleteGroupIfFull()
    {
        if (groupCompleted) return;
        List<Pedestal> members = all.FindAll(p => p.groupID == groupID);
        if (members.Exists(p => !p.HasMeme)) return;

        foreach (Pedestal member in members)
        {
            member.groupCompleted = true;
            member.SetCorrect();
        }
    }

    // ---------- Resultados ----------
    void SetCorrect()
    {
        IsCorrect = true;
        everRight = true;
        Play(correctClip);
        if (!KeylessPedestal && randomizer == null && CurrentMeme != null && CurrentMeme.Data != null)
            MemeDex.Report(CurrentMeme.Data, CurrentMeme.Data.baseImage);   // a white one: its Base into the MemeDex
        if (drainMeme && !KeylessPedestal && CurrentMeme is MemeChanger window) window.SetMeme(null);   // downloaded
        if (retire != null && retire.when == PedestalRetire.When.Result && randomizer == null) retire.Retire();
        if (inOrder) LevelManager.Instance.RaiseInOrder(ControlledIslands, ControlledBridges);
        else
        {
            foreach (string id in ControlledIslands) LevelManager.Instance.SubirIsla(id);
            foreach (string id in ControlledBridges) LevelManager.Instance.SubirPuente(id);
        }
        if (CurrentMeme != null) CurrentMeme.OnPedestalResult(this, true);
    }

    void SetIncorrect()
    {
        Play(incorrectClip);
        LowerBridges();
        if (CurrentMeme != null) CurrentMeme.OnPedestalResult(this, false);
    }

    void LowerBridges()
    {
        foreach (string id in ControlledBridges) LevelManager.Instance.BajarPuente(id);
    }

    // Lets its meme go with a little hop, no "wrong" sound (a pedestal going into the ground: PedestalRetire)
    public void DropMeme()
    {
        if (MemeObject == null || seating) return;
        GameObject meme = MemeObject;
        rejecting = true;   // (no "incorrect")
        meme.transform.SetParent(null, true);
        Release();
        if (meme.TryGetComponent(out Rigidbody rb))
        {
            rb.constraints = RigidbodyConstraints.None;
            rb.isKinematic = false;
            rb.AddForce(transform.forward * 60f + transform.up * 220f);
        }
    }

    void Reject()
    {
        if (MemeObject == null) return;
        rejecting = true;
        MemeObject.transform.SetParent(null);
        Rigidbody rb = MemeObject.GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.None;
        rb.isKinematic = false;
        rb.AddForce(transform.forward * 100f + transform.up * 300f);
    }

    void Play(AudioClip clip, bool loop = false)
    {
        audioSource.loop = loop;
        audioSource.clip = clip;
        if (clip != null) audioSource.Play();
    }

    static float Length(AudioClip clip) => clip != null ? clip.length : 0f;

    // ---------- Visual y trigger según el tipo ----------
    public void ApplyVisual()
    {
        SetActive(visualKeyed, !KeylessPedestal);
        SetActive(visualKeyless, KeylessPedestal);
    }

    public void SyncTrigger()
    {
        if (trigger == null) trigger = GetComponent<SphereCollider>();
        if (trigger == null || socket == null) return;
        Vector3 center = transform.InverseTransformPoint(socket.position);
        if (trigger.isTrigger && trigger.center == center) return;
        Record(trigger);
        trigger.isTrigger = true;
        trigger.center = center;
    }

    void SetActive(GameObject go, bool on)
    {
        if (go == null || go.activeSelf == on) return;
        Record(go);
        go.SetActive(on);
    }

    // En el editor registra el cambio (Ctrl+Z y overrides del prefab). En el juego no hace nada.
    static void Record(Object obj)
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        UnityEditor.Undo.RecordObject(obj, "Pedestal");
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (obj != null) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        };
#endif
    }

#if UNITY_EDITOR
    // Al tildar/destildar KeylessPedestal o mover el Socket, el modelo y el trigger se actualizan solos
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || UnityEditor.EditorUtility.IsPersistent(this)) return;
            ApplyVisual();
            SyncTrigger();
        };
    }

    void OnDrawGizmos()
    {
        if (socket == null) return;
        SphereCollider col = GetComponent<SphereCollider>();
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        float radius = col != null ? col.radius * scale : 0.2f;

        Gizmos.color = KeylessPedestal ? new Color(0.2f, 0.2f, 0.2f) : Color.white;
        Gizmos.DrawWireSphere(socket.position, radius);
        Gizmos.DrawSphere(socket.position, 0.03f);
    }
#endif

    // ---------- Break (elevator pedestal) ----------
    // A heavy stone giving way: it always tips over on its side toward the nearest edge, scrapes along the ground until
    // it is over the void, and falls off with gravity, slowly rolling over the edge, until it is gone (Drop Distance
    // below). It touches nothing: nobody gets pushed.
    public void Break()
    {
        if (broken) return;
        broken = true;

        if (evaluating != null) StopCoroutine(evaluating);
        seating = false;
        trigger.enabled = false;
        if (particles != null) particles.Stop();
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        transform.SetParent(null, true);   // not riding the elevator any more

        Vector3 start = Center();
        StartCoroutine(FallOff(NearestEdge(start), start.y));
    }

    IEnumerator FallOff(Vector3 dir, float startY)
    {
        // 1. Tips over its base edge on that side, speeding up like a falling weight
        MeshRenderer[] rends = GetComponentsInChildren<MeshRenderer>();
        Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(transform.position, Vector3.one);
        foreach (MeshRenderer r in rends) b.Encapsulate(r.bounds);
        float edge = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
        Vector3 pivot = new Vector3(b.center.x, b.min.y, b.center.z) + dir * edge;
        Vector3 axis = Vector3.Cross(Vector3.up, dir);

        float angle = 0f;
        for (float t = 0f; t < toppleTime; t += Time.deltaTime)
        {
            float target = 90f * Mathf.Pow(t / toppleTime, 2.5f);
            transform.RotateAround(pivot, axis, target - angle);
            angle = target;
            yield return null;
        }
        transform.RotateAround(pivot, axis, 90f - angle);
        if (breakClip != null) AudioSource.PlayClipAtPoint(breakClip, pivot);

        // 2. Scrapes toward the edge until its middle is over the void
        float speed = 0f;
        for (float t = 0f; t < 15f && HasGround(Center()); t += Time.deltaTime)
        {
            speed = Mathf.Min(speed + slideAcceleration * Time.deltaTime, 3f);
            transform.position += dir * speed * Time.deltaTime;
            yield return null;
        }

        // 3. Off the edge: gravity, rolling slowly over it
        if (!TryGetComponent(out Rigidbody body)) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.velocity = dir * Mathf.Max(speed, 0.5f);
        body.angularVelocity = axis * 1.2f;

        float bottom = startY - LevelManager.Instance.dropDistance;
        while (transform.position.y > bottom) yield return null;
        gameObject.SetActive(false);
    }

    // Middle of its meshes (no particles)
    Vector3 Center()
    {
        MeshRenderer[] rends = GetComponentsInChildren<MeshRenderer>();
        if (rends.Length == 0) return transform.position;
        Bounds b = rends[0].bounds;
        foreach (MeshRenderer r in rends) b.Encapsulate(r.bounds);
        return b.center;
    }

    // Of 16 directions, the one with the void closest (a gap at least 2 m long)
    Vector3 NearestEdge(Vector3 from)
    {
        Vector3 best = Vector3.forward;
        float nearest = float.MaxValue;
        for (int k = 0; k < 16; k++)
        {
            Vector3 dir = Quaternion.AngleAxis(k * 22.5f, Vector3.up) * Vector3.forward;
            float d = VoidDistance(from, dir);
            if (d < nearest) { nearest = d; best = dir; }
        }
        return best;
    }

    // How far along dir there's nothing under (2 m of void in a row), up to 40 m
    float VoidDistance(Vector3 from, Vector3 dir)
    {
        const float step = 0.5f;
        float gapStart = -1f;
        for (float d = 0f; d <= 40f; d += step)
        {
            if (HasGround(from + dir * d)) { gapStart = -1f; continue; }
            if (gapStart < 0f) gapStart = d;
            if (d - gapStart >= 2f) return gapStart;
        }
        return 40f;
    }

    bool HasGround(Vector3 point)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider is CharacterController || hit.transform.IsChildOf(transform)) continue;   // the player, itself
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;                           // memes
            return true;
        }
        return false;
    }
}
