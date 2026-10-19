using System.Collections;
using UnityEngine;

// Falling platform: when the player steps on it, it warns (shake + crack sound + small camera shake),
// then drops speeding up like a real fall, waits below and rises back slowly.
// While falling and down it has no collision: the player falls through to the void instead of riding it down.
// Landing on it while it rises stops the rise and starts the warning again.
// Pivots: PlatformPivotSuperior (rest height) and PlatformPivotInferior (lowest point), anywhere inside its bridge.
// PlayerMove calls StepOn() while the player stands on it.
// It moves in its parent's space as if the bridge were straight; with a BridgeBend it is then placed on the bent bridge.
// Its values come from its Bridge (Falling Platforms section) unless Override Bridge is on.
[DefaultExecutionOrder(-1)]   // placed before the player follows it (PlayerMove.LateUpdate)
public class FallingPlatform : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Off: uses the settings of its Bridge. On: its own settings below (also used if it's not inside a Bridge)")]
    public bool overrideBridge = false;
    public FallingPlatformSettings settings = new FallingPlatformSettings();

    [Header("Debug")]
    [SerializeField] private State state = State.Resting;

    [Header("References")]
    public Transform pivotSuperior;
    public Transform pivotInferior;

    enum State { Resting, Warning, Falling, Down, Rising }

    private AudioSource audioSource;
    private Bridge bridge;
    private BridgeBend bend;
    private Collider[] solidColliders;
    private Vector3 restLocalPosition;   // its place in the straight bridge
    private Quaternion restLocalRotation;
    private Vector3 localPosition;       // where it is now in the straight bridge (shake, fall, rise)
    private float fallSpeed;

    void Start()
    {
        bridge = GetComponentInParent<Bridge>();
        bend = GetComponentInParent<BridgeBend>();
        Transform searchRoot = bridge != null ? bridge.transform : transform.parent;
        if (pivotSuperior == null) pivotSuperior = FindChildByName(searchRoot, "PlatformPivotSuperior");
        if (pivotInferior == null) pivotInferior = FindChildByName(searchRoot, "PlatformPivotInferior");

        localPosition = transform.localPosition;
        restLocalRotation = transform.localRotation;
        if (pivotSuperior != null) SetLocalHeight(LocalHeightOf(pivotSuperior));   // rests at the upper pivot
        restLocalPosition = localPosition;
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
        Play(Settings.crackClip);
        ShakeCamera();

        for (float t = 0f; t < Settings.warningTime; t += Time.deltaTime)
        {
            Vector2 jitter = Random.insideUnitCircle * Settings.shakeAmount;
            localPosition = new Vector3(restLocalPosition.x + jitter.x, localPosition.y, restLocalPosition.z + jitter.y);
            yield return null;
        }
        localPosition = new Vector3(restLocalPosition.x, localPosition.y, restLocalPosition.z);

        // Fall: speeds up until the lowest point. No collision: whoever is on it falls through
        state = State.Falling;
        SetSolid(false);
        Play(Settings.fallClip);
        fallSpeed = 0f;
        float lowest = LocalHeightOf(pivotInferior);
        while (localPosition.y > lowest)
        {
            fallSpeed = Mathf.Min(fallSpeed + Settings.fallAcceleration * Time.deltaTime, Settings.maxFallSpeed);
            SetLocalHeight(Mathf.Max(localPosition.y - fallSpeed * Time.deltaTime, lowest));
            yield return null;
        }

        state = State.Down;
        yield return new WaitForSeconds(Settings.returnDelay);

        // Rise back, slow and smooth (solid again, so you can land on it)
        state = State.Rising;
        SetSolid(true);
        Play(Settings.riseClip);
        while (localPosition.y < restLocalPosition.y)
        {
            SetLocalHeight(Mathf.MoveTowards(localPosition.y, restLocalPosition.y, Settings.riseSpeed * Time.deltaTime));
            yield return null;
        }
        localPosition = restLocalPosition;
        state = State.Resting;
    }

    // Every frame, after the shake/fall/rise of this frame: on the straight bridge, or on its point of the bent one
    void LateUpdate()
    {
        // Laid out on the curve: its slot, plus how far it shook or fell from its rest (in the bridge's space)
        if (bend != null && transform.parent != null && bend.PlatformSlot(this, out Vector3 slot, out Quaternion frame))
        {
            Transform bridgeRoot = bend.transform;
            Vector3 moved = bridgeRoot.InverseTransformVector(transform.parent.TransformVector(localPosition - restLocalPosition));
            Quaternion designed = Quaternion.Inverse(bridgeRoot.rotation) * transform.parent.rotation * restLocalRotation;
            transform.SetPositionAndRotation(bridgeRoot.TransformPoint(slot + moved), bridgeRoot.rotation * (frame * designed));
            return;
        }

        if (bend == null || !bend.IsBending || transform.parent == null)
        {
            transform.localPosition = localPosition;
            transform.localRotation = restLocalRotation;
            return;
        }

        Transform root = bend.transform;
        Vector3 position = root.InverseTransformPoint(transform.parent.TransformPoint(localPosition));
        Quaternion rotation = Quaternion.Inverse(root.rotation) * transform.parent.rotation * restLocalRotation;
        bend.Deform(ref position, ref rotation);
        transform.SetPositionAndRotation(root.TransformPoint(position), root.rotation * rotation);
    }

    FallingPlatformSettings Settings => overrideBridge || bridge == null ? settings : bridge.platformSettings;

    void SetSolid(bool solid)
    {
        foreach (Collider c in solidColliders) c.enabled = solid;
    }

    // Heights in the parent's space (the straight bridge), so the bridge moving or bending doesn't matter
    void SetLocalHeight(float y) => localPosition = new Vector3(localPosition.x, y, localPosition.z);
    float LocalHeightOf(Transform pivot) => transform.parent != null ? transform.parent.InverseTransformPoint(pivot.position).y : pivot.position.y;

    void ShakeCamera()
    {
        if (Settings.cameraShakeAmount <= 0f || Camera.main == null) return;
        if (Camera.main.TryGetComponent(out PlayerLook look)) look.StartCoroutine(look.ShakeTheCamera(0.15f, Settings.cameraShakeAmount));
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

// Values of a falling platform. The Bridge holds one for all its platforms; a platform can use its own.
[System.Serializable]
public class FallingPlatformSettings
{
    [Header("Warning")]
    [Tooltip("Seconds between stepping on it and the drop. Enough to run across, short enough to feel the urgency")]
    public float warningTime = 0.2f;
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
}
