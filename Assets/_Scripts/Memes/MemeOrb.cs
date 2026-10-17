using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// Meme as an orb (Game_Old). Before: OrbMemes. Same behavior as always:
// the image mutates over time while you carry it and move, and returns to the original.
public class MemeOrb : Meme
{
    public GameObject Player;

    [Header("Imágenes (la 0 es la original)")]
    public List<Material> materials = new List<Material>();
    public int matCount;

    [Header("Mutación: cambia mientras lo llevás y te movés")]
    public AudioClip warnClip;       // aviso: "está por cambiar"
    public AudioClip changeClip;     // cambió
    public AudioClip returnClip;     // volvió al original
    public AudioClip sweepTickClip;  // cada imagen del barrido en el pedestal
    public float firstChangeAfter = 3f;                        // segundos moviéndote hasta el primer cambio
    public Vector2 changeInterval = new Vector2(4f, 8f);       // segundos moviéndote entre cambios
    public Vector2 variantDuration = new Vector2(1.5f, 2.5f);  // cuánto dura la variante antes de volver al original
    public float warnLead = 0.6f;                              // cuánto antes del cambio suena el aviso
    public float graceAfterEvent = 1f;                         // no muta cerca de saltos, caídas, puentes o islas nuevas
    public float flashIntensity = 2f;
    [Range(0.3f, 1f)] public float sweepPortion = 0.75f;      // qué parte de la carga del pedestal ocupa el barrido

    private MeshRenderer memeRenderer;
    private AudioSource sfx;
    private MaterialPropertyBlock mpb;
    private PlayerMove playerMove;
    private readonly HashSet<GameObject> visitedIslands = new HashSet<GameObject>();
    private float movingTime;            // tiempo acumulado moviéndote con el orbe en la mano
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

        // Las imágenes se asignan en el Inspector (antes se cargaban de Resources según el nombre del orbe)
        if (materials.Count == 0) { Debug.LogError("[MemeOrb] '" + name + "' no tiene imágenes asignadas.", this); enabled = false; return; }
        matCount = 0;
        memeRenderer = gameObject.transform.Find("Meme").GetComponent<MeshRenderer>();
        memeRenderer.sharedMaterial = materials[matCount];

        // Fuente de sonido propia para la mutación (la del orbe se mutea mientras lo llevás)
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
        UpdateMutation();   //Imagen: muta con el tiempo mientras lo llevás y te movés
    }

    //---- Meme events

    protected override void OnPickedUp() => ApplyHoldingEffects();
    protected override void OnDropped() => ApplyRestingEffects();
    public override void OnBridgeCrossed() => MarkEvent();   // pisaste un puente nuevo: no mutar justo ahí

    // Lo llama PlayerMove al pisar una isla nueva con el orbe en la mano
    public override void OnIslandVisited(GameObject island)
    {
        visitedIslands.Add(island);
        MarkEvent();
    }

    // ---------- MUTACIÓN ----------
    // La imagen no depende de ninguna acción puntual, solo del tiempo que llevás el orbe moviéndote.
    // Después de cada variante vuelve al original. Al soltarlo, vuelve al original.
    // Cada isla nueva a la que lo llevás desbloquea una versión más para el azar.

    void UpdateMutation()
    {
        if (materials.Count < 2) return;

        Vector3 playerPos = Player.transform.position;
        Vector3 delta = playerPos - lastPlayerPos; delta.y = 0f;
        lastPlayerPos = playerPos;

        // Momentos que el jugador podría confundir con la causa del cambio: no mutar cerca de ellos
        if (delta.magnitude > 3f) MarkEvent();                          // respawn / teletransporte
        if (playerMove != null && !playerMove.isGrounded) MarkEvent();  // saltando o cayendo

        if (IsHeld && !wasHolded) StartMutationTimer();
        if (!IsHeld && wasHolded) StopMutationAndRestore();
        wasHolded = IsHeld;

        if (!IsHeld || mutationRoutine != null) return;

        if (delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f) > 0.5f) movingTime += Time.deltaTime; // solo cuenta si te movés
        if (movingTime >= nextChangeAt && !InGrace())
            mutationRoutine = StartCoroutine(Mutate());
    }

    void StartMutationTimer()
    {
        // Si lo sacás del pedestal a mitad del barrido, el barrido se corta y vuelve al original
        if (sweepRoutine != null) { StopCoroutine(sweepRoutine); sweepRoutine = null; SetImage(0); }
        movingTime = 0f;
        nextChangeAt = firstChangeAfter;
    }

    void StopMutationAndRestore()
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (matCount != 0) { SetImage(0); Play(returnClip); StartCoroutine(Flash(0.6f)); }
    }

    void MarkEvent() { lastEventTime = Time.time; }
    bool InGrace() { return Time.time - lastEventTime < graceAfterEvent; }

    int UnlockedVariants() { return Mathf.Clamp(1 + visitedIslands.Count, 1, materials.Count - 1); }

    IEnumerator Mutate()
    {
        Play(warnClip);
        yield return new WaitForSeconds(warnLead);
        while (InGrace()) yield return null;   // si justo saltaste o pisaste un puente, espera

        int max = UnlockedVariants();
        int v = Random.Range(1, max + 1);
        if (max > 1 && v == lastVariant) v = (v % max) + 1;   // no repetir la misma variante seguida
        lastVariant = v;
        SetImage(v); Play(changeClip); StartCoroutine(Flash(1f));

        yield return new WaitForSeconds(Random.Range(variantDuration.x, variantDuration.y));

        SetImage(0); Play(returnClip); StartCoroutine(Flash(0.6f));
        nextChangeAt = movingTime + Random.Range(changeInterval.x, changeInterval.y);
        mutationRoutine = null;
    }

    // Lo llama el pedestal mientras carga: muestra todas las versiones rápido y termina en el original
    public override void Sweep(float duration)
    {
        if (mutationRoutine != null) { StopCoroutine(mutationRoutine); mutationRoutine = null; }
        if (sweepRoutine != null) StopCoroutine(sweepRoutine);
        sweepRoutine = StartCoroutine(SweepRoutine(duration));
    }

    IEnumerator SweepRoutine(float duration)
    {
        // Termina antes que la carga del pedestal y vuelve al original en silencio,
        // para no pisar el sonido de aceptación o rechazo
        float step = duration * sweepPortion / materials.Count;
        for (int i = 1; i < materials.Count; i++)
        {
            SetImage(i); Play(sweepTickClip);
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

    // Destello: sube la emisión del meme y la baja en 0.3 s
    IEnumerator Flash(float strength)
    {
        Color baseEmission = memeRenderer.sharedMaterial.GetColor("_EmissionColor");
        float t = 0f, dur = 0.3f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - t / dur;
            memeRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", Color.Lerp(baseEmission, Color.white * flashIntensity, k * k * strength));
            memeRenderer.SetPropertyBlock(mpb);
            yield return null;
        }
        memeRenderer.SetPropertyBlock(null);
    }

    //---- Effects while held / resting (before: Holding / NotHolding coroutines polled every second)

    void ApplyHoldingEffects()
    {
        if (ID == "1Aa") GetComponent<AudioReverbZone>().enabled = true;   //Dios: efecto reverb

        //Desmutear el loop de sonidos de holding
        transform.Find("Audio").GetComponent<AudioSource>().mute = false;
        GetComponent<AudioSource>().mute = true;
    }

    void ApplyRestingEffects()
    {
        //Las modificaciones vuelven a la normalidad cuando ya no tenes el orbe
        if (ID == "1Aa") GetComponent<AudioReverbZone>().enabled = false;   //Dios: se va el efecto reverb

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
