using System.Collections;
using UnityEngine;

// Pedestal: detecta el orbe, lo encastra en el Socket, evalúa la llave y sube/baja islas y puentes.
// Estructura del prefab:
//   Pedestal       :: Pedestal + AudioSource + SphereCollider (trigger, centrado en el Socket)
//   ├── Visual_Keyed     pedestal blanco (mármol, con llave)
//   ├── Visual_Keyless   pedestal negro (piedra, sin llave)
//   └── Socket           punto de encastre del orbe + partículas
[RequireComponent(typeof(AudioSource), typeof(SphereCollider))]
public class Pedestal : MonoBehaviour
{
    [Header("Tipo")]
    [Tooltip("Negro (piedra): acepta cualquier orbe. Blanco (mármol): necesita la llave.")]
    public bool KeylessPedestal = false;
    [Tooltip("El orbe es correcto si su nombre empieza con esto")]
    public string KeyID;
    public string pedestalID;

    [Header("Qué controla")]
    public string[] ControlledIslands;
    public string[] ControlledBridges;

    [Header("Grupo enlazado (opcional)")]
    [Tooltip("Si pertenece a un grupo, no evalúa solo: el grupo se completa cuando todos tienen orbe")]
    public PedestalGroup group;

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

    // TEMPORAL: datos viejos del pedestal 51, los lee "Memetica > Migrar pedestales" para armar el PedestalGroup
    [SerializeField, HideInInspector] GameObject[] LinkedPedestals;

    // Estado (solo lectura desde afuera)
    public GameObject Orb { get; private set; }
    public bool HasOrb => Orb != null;
    public bool IsCorrect { get; private set; }

    AudioSource audioSource;
    SphereCollider trigger;
    ParticleSystem particles;
    Coroutine evaluating;
    bool rejecting;
    bool broken;

    static PlayerHoldObjects hold;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        trigger = GetComponent<SphereCollider>();
        particles = socket != null ? socket.GetComponent<ParticleSystem>() : null;
        ApplyVisual();
        SyncTrigger();
    }

    void Start()
    {
        if (hold == null) hold = Camera.main.GetComponent<PlayerHoldObjects>();
    }

    // ---------- Orbe entra / sale ----------
    void OnTriggerEnter(Collider other)
    {
        if (broken || HasOrb || !other.CompareTag("Meme")) return;

        GameObject orb = other.gameObject;
        hold.SnapObject(orb);
        orb.transform.position = socket.position;
        if (particles != null) particles.Play();

        Orb = orb;
        Play(snapClip);
        evaluating = StartCoroutine(Evaluate(orb));
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Meme") || other.gameObject != Orb) return; // solo importa el orbe encastrado

        if (evaluating != null) StopCoroutine(evaluating);
        bool lowerBridges = KeylessPedestal || Orb.name.StartsWith(KeyID);

        Orb = null;
        IsCorrect = false;
        if (!rejecting) Play(incorrectClip);   // si lo expulsamos nosotros, el "incorrecto" ya sonó
        rejecting = false;

        if (lowerBridges) LowerBridges();
        if (group != null) group.Refresh();
    }

    IEnumerator Evaluate(GameObject orb)
    {
        yield return new WaitForSeconds(Length(snapClip));

        // Enlazado: carga en loop hasta que el grupo se complete
        if (group != null)
        {
            Play(chargeClip, true);
            yield return new WaitForSeconds(Length(chargeClip));
            group.Refresh();
            yield break;
        }

        Play(chargeClip);
        if (KeylessPedestal) { SetCorrect(); yield break; }

        // Blanco: barrido de imágenes mientras carga, después decide
        if (orb.TryGetComponent(out OrbMemes memes)) memes.Sweep(Length(chargeClip));
        yield return new WaitForSeconds(Length(chargeClip));

        if (orb.name.StartsWith(KeyID)) SetCorrect();
        else { SetIncorrect(); Reject(); }
    }

    // ---------- Resultados ----------
    void SetCorrect()
    {
        IsCorrect = true;
        Play(correctClip);
        foreach (string id in ControlledIslands) LevelManager.Instance.SubirIsla(id);
        foreach (string id in ControlledBridges) LevelManager.Instance.SubirPuente(id);
    }

    void SetIncorrect()
    {
        Play(incorrectClip);
        LowerBridges();
    }

    void LowerBridges()
    {
        foreach (string id in ControlledBridges) LevelManager.Instance.BajarPuente(id);
    }

    void Reject()
    {
        if (Orb == null) return;
        rejecting = true;
        Rigidbody rb = Orb.GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.None;
        rb.isKinematic = false;
        rb.AddForce(transform.forward * 100f + transform.up * 300f);
    }

    public void PlayCorrect() => Play(correctClip);   // lo usa el PedestalGroup al completarse

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
