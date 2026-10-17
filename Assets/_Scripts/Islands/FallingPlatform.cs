using System.Collections;
using UnityEngine;

// Falling platform: when the player steps on it, it warns (shake + crack sound + small camera shake),
// then drops speeding up like a real fall, waits below and rises back slowly.
// While falling and down it has no collision: the player falls through to the void instead of riding it down.
// Landing on it while it rises stops the rise and starts the warning again.
// Pivots: PlatformPivotSuperior (rest height) and PlatformPivotInferior (lowest point), anywhere inside its bridge.
// PlayerMove calls StepOn() while the player stands on it.
public class FallingPlatform : MonoBehaviour
{
    [Header("Warning")]
    [Tooltip("Seconds between stepping on it and the drop. Enough to run across, short enough to feel the urgency")]
    public float warningTime = 0.3f;
    [Tooltip("How much it shakes while warning (meters)")]
    public float shakeAmount = 0.03f;
    [Tooltip("Small camera shake when stepping on it. 0 = off")]
    public float cameraShakeAmount = 0.03f;

    [Header("Fall")]
    [Tooltip("Speeds up like a falling object (m/s²)")]
    public float fallAcceleration = 25f;
    public float maxFallSpeed = 20f;
    [Tooltip("Seconds it stays down before rising back")]
    public float returnDelay = 2.5f;
    [Tooltip("Speed it rises back at (m/s)")]
    public float riseSpeed = 3f;

    [Header("Sounds")]
    public AudioClip crackClip;   // stepped on: it's about to fall
    public AudioClip fallClip;    // drops
    public AudioClip riseClip;    // comes back

    [Header("Debug")]
    [SerializeField] private State state = State.Resting;

    [Header("References")]
    public Transform pivotSuperior;
    public Transform pivotInferior;

    enum State { Resting, Warning, Falling, Down, Rising }

    private AudioSource audioSource;
    private Collider[] solidColliders;
    private Vector3 restPosition;
    private float fallSpeed;

    void Start()
    {
        Bridge bridge = GetComponentInParent<Bridge>();
        Transform searchRoot = bridge != null ? bridge.transform : transform.parent;
        if (pivotSuperior == null) pivotSuperior = FindChildByName(searchRoot, "PlatformPivotSuperior");
        if (pivotInferior == null) pivotInferior = FindChildByName(searchRoot, "PlatformPivotInferior");

        if (pivotSuperior != null) MoveToHeight(pivotSuperior.position.y);   // rests at the upper pivot
        solidColliders = System.Array.FindAll(GetComponentsInChildren<Collider>(), c => !c.isTrigger);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 3f;
        }
    }

    // The player is standing on it. While rising, the rise stops and it falls again
    public void StepOn()
    {
        if (state != State.Resting && state != State.Rising) return;
        StopAllCoroutines();
        StartCoroutine(FallSequence());
    }

    IEnumerator FallSequence()
    {
        // Warning: shakes around its rest position
        state = State.Warning;
        restPosition = transform.position;
        Play(crackClip);
        ShakeCamera();

        for (float t = 0f; t < warningTime; t += Time.deltaTime)
        {
            Vector2 jitter = Random.insideUnitCircle * shakeAmount;
            transform.position = restPosition + new Vector3(jitter.x, 0f, jitter.y);
            yield return null;
        }
        transform.position = restPosition;

        // Fall: speeds up until the lowest point. No collision: whoever is on it falls through
        state = State.Falling;
        SetSolid(false);
        Play(fallClip);
        fallSpeed = 0f;
        while (transform.position.y > pivotInferior.position.y)
        {
            fallSpeed = Mathf.Min(fallSpeed + fallAcceleration * Time.deltaTime, maxFallSpeed);
            MoveToHeight(Mathf.Max(transform.position.y - fallSpeed * Time.deltaTime, pivotInferior.position.y));
            yield return null;
        }

        state = State.Down;
        yield return new WaitForSeconds(returnDelay);

        // Rise back, slow and smooth (solid again, so you can land on it)
        state = State.Rising;
        SetSolid(true);
        Play(riseClip);
        while (transform.position.y < pivotSuperior.position.y)
        {
            MoveToHeight(Mathf.MoveTowards(transform.position.y, pivotSuperior.position.y, riseSpeed * Time.deltaTime));
            yield return null;
        }
        state = State.Resting;
    }

    void SetSolid(bool solid)
    {
        foreach (Collider c in solidColliders) c.enabled = solid;
    }

    void MoveToHeight(float y) => transform.position = new Vector3(transform.position.x, y, transform.position.z);

    void ShakeCamera()
    {
        if (cameraShakeAmount <= 0f || Camera.main == null) return;
        if (Camera.main.TryGetComponent(out PlayerLook look)) look.StartCoroutine(look.ShakeTheCamera(0.15f, cameraShakeAmount));
    }

    void Play(AudioClip clip)
    {
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    // Pivots can be anywhere inside the bridge, not only next to the platform
    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == childName) return child;
        return null;
    }
}
