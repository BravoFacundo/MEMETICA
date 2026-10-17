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
[RequireComponent(typeof(AudioSource), typeof(SphereCollider))]
public class Pedestal : MonoBehaviour
{
    [Header("Tipo")]
    [Tooltip("Negro (piedra): acepta cualquier meme. Blanco (mármol): necesita la llave.")]
    public bool KeylessPedestal = false;
    [Tooltip("El meme es correcto si su ID empieza con esto (Meme_3Aa → ID 3Aa)")]
    public string KeyID;
    public string pedestalID;

    [Header("Qué controla")]
    public string[] ControlledIslands;
    public string[] ControlledBridges;

    [Header("Grupo enlazado (opcional)")]
    [Tooltip("Pedestals with the same Group ID complete together when all of them have a meme. Empty = no group")]
    public string groupID;

    [Header("Pista")]
    [Tooltip("The painting (easel) that gives the clue for this pedestal (before: Atril)")]
    public Transform cluePainting;

    [Header("Partes")]
    public Transform socket;
    public GameObject visualKeyed;
    public GameObject visualKeyless;

    [Header("Sonidos")]
    public AudioClip chargeClip;
    public AudioClip correctClip;
    public AudioClip incorrectClip;
    public AudioClip snapClip;

    [Header("Romperse (solo el pedestal del ascensor)")]
    public AudioClip breakClip;       // golpe al tocar el piso
    public float toppleTime = 0.7f;   // cuánto tarda en volcarse

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

    static PlayerLook look;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        trigger = GetComponent<SphereCollider>();
        particles = socket != null ? socket.GetComponent<ParticleSystem>() : null;
        ApplyVisual();
        SyncTrigger();
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Start()
    {
        if (look == null) look = Camera.main.GetComponent<PlayerLook>();
    }

    // The pedestal whose clue is this painting (or one of its parents), or null
    public static Pedestal WithClue(Transform painting)
    {
        foreach (Pedestal p in all)
            if (p.cluePainting != null && (p.cluePainting == painting || p.cluePainting.IsChildOf(painting))) return p;
        return null;
    }

    // ---------- Meme entra / sale ----------
    void OnTriggerEnter(Collider other)
    {
        if (broken || HasMeme || !other.CompareTag("Meme") || other.gameObject == releasedMeme) return;

        GameObject memeObject = other.gameObject;
        look.SnapObject(memeObject);
        memeObject.transform.position = socket.position;
        memeObject.transform.SetParent(socket);   // moves with the pedestal (floating islands, elevators)
        if (memeObject.TryGetComponent(out Rigidbody body)) body.isKinematic = true;
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
        if (releasedMeme != null && !InsideZone(releasedMeme)) releasedMeme = null;   // it really left: it can be placed again
        if (!HasMeme) return;

        bool held = CurrentMeme != null && CurrentMeme.IsHeld;
        bool movedAway = Vector3.Distance(MemeObject.transform.position, socket.position) > 0.5f;
        if (held || movedAway) Release();
    }

    void Release()
    {
        releasedMeme = MemeObject;   // not snapped again until it leaves the zone
        if (evaluating != null) StopCoroutine(evaluating);
        bool lowerBridges = KeylessPedestal || Meme.IDOf(MemeObject).StartsWith(KeyID);

        if (CurrentMeme != null) CurrentMeme.RemoveFromPedestal(this);
        MemeObject = null;
        CurrentMeme = null;
        IsCorrect = false;
        if (!rejecting && !broken) Play(incorrectClip);   // si lo expulsamos nosotros, el "incorrecto" ya sonó
        rejecting = false;

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

        if (Meme.IDOf(MemeObject).StartsWith(KeyID)) SetCorrect();
        else { SetIncorrect(); Reject(); }
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
        Play(correctClip);
        foreach (string id in ControlledIslands) LevelManager.Instance.SubirIsla(id);
        foreach (string id in ControlledBridges) LevelManager.Instance.SubirPuente(id, socket.position);
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

    // ---------- Romperse (pedestal del ascensor) ----------
    // Vuelca el pedestal hacia el lado contrario a 'pushedFrom' y lo deja sin funcionar
    public void Break(Vector3 pushedFrom)
    {
        if (broken) return;
        broken = true;

        if (evaluating != null) StopCoroutine(evaluating);
        trigger.enabled = false;
        if (particles != null) particles.Stop();

        StartCoroutine(Topple(pushedFrom));
    }

    IEnumerator Topple(Vector3 pushedFrom)
    {
        // Tamaño del pedestal (solo mallas, sin partículas) para encontrar el borde de la base sobre el que gira
        MeshRenderer[] rends = GetComponentsInChildren<MeshRenderer>();
        if (rends.Length == 0) yield break;
        Bounds b = rends[0].bounds;
        foreach (MeshRenderer r in rends) b.Encapsulate(r.bounds);

        Vector3 dir = b.center - pushedFrom; dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        dir.Normalize();
        Vector3 axis = Vector3.Cross(Vector3.up, dir);
        float edge = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
        Vector3 pivot = new Vector3(b.center.x, b.min.y, b.center.z) + dir * edge;

        // Cae acelerando, como con gravedad
        float t = 0f, angle = 0f;
        while (t < toppleTime)
        {
            t += Time.deltaTime;
            float target = 90f * Mathf.Pow(Mathf.Clamp01(t / toppleTime), 2f);
            transform.RotateAround(pivot, axis, target - angle);
            angle = target;
            yield return null;
        }

        if (breakClip != null) AudioSource.PlayClipAtPoint(breakClip, pivot);

        // Rebote chico
        yield return RotateBy(pivot, axis, -6f, 0.08f);
        yield return RotateBy(pivot, axis, 6f, 0.10f);
    }

    IEnumerator RotateBy(Vector3 pivot, Vector3 axis, float degrees, float duration)
    {
        float t = 0f, done = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float target = degrees * Mathf.Clamp01(t / duration);
            transform.RotateAround(pivot, axis, target - done);
            done = target;
            yield return null;
        }
    }
}
