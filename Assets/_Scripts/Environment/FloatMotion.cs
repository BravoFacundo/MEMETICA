using UnityEngine;
using UnityEngine.Serialization;

// Slow idle motion around the start position and rotation (it never wanders off).
// Before: FloatBob (islands) and CloudDrift (clouds). Each part is optional: 0 turns it off.
//   Bob    up and down
//   Drift  back and forth along the wind (or a fixed direction)
//   Tilt   sway on X and Z, two rhythms so it is organic and not a pendulum
//   Spin   turn a little to each side on Y
// Every object starts at a different moment with a slightly different rhythm.
// It can go on the same object as an Island: the island moves where it floats around (SetRestHeight), so
// pedestals and everything interactive can sit right in Island_NN and still float and rise with it.
[DefaultExecutionOrder(-10)]   // moves before bridges bend to it and the player follows it
public class FloatMotion : MonoBehaviour
{
    [Header("Bob")]
    [Tooltip("Meters up and down")]
    [FormerlySerializedAs("amplitude")] public float bobAmplitude = 0.8f;
    [Tooltip("Seconds per full cycle (higher = slower)")]
    [FormerlySerializedAs("period")] public float bobPeriod = 16f;

    [Header("Drift")]
    [Tooltip("Meters to each side of the start position. 0 = no drift")]
    public float driftRange = 0f;
    [Tooltip("Seconds per back and forth")]
    public float driftPeriod = 60f;
    [Tooltip("Uses the WindController direction")]
    public bool followWind = true;
    [Tooltip("Direction when it doesn't follow the wind")]
    public Vector3 driftDirection = Vector3.right;

    [Header("Tilt")]
    [Tooltip("Max tilt on X and Z. 0 = no tilt")]
    public float tiltDegrees = 1.2f;
    [Tooltip("Slower than the bob so they don't sync")]
    public float tiltPeriod = 22f;

    [Header("Spin")]
    [Tooltip("Max turn to each side on Y. 0 = no spin")]
    public float spinDegrees = 0f;
    public float spinPeriod = 80f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private float bobPhase, driftPhase, tiltPhaseX, tiltPhaseZ, spinPhase;
    private Vector3 offset;

    // Awake, so an Island on the same object can move its rest height in its Start
    void Awake()
    {
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
    }

    void Start()
    {

        bobPhase = RandomPhase();
        driftPhase = RandomPhase();
        tiltPhaseX = RandomPhase();
        tiltPhaseZ = RandomPhase();
        spinPhase = RandomPhase();
        bobPeriod *= Random.Range(0.85f, 1.15f);
        driftPeriod *= Random.Range(0.85f, 1.15f);
        tiltPeriod *= Random.Range(0.85f, 1.15f);
        spinPeriod *= Random.Range(0.85f, 1.15f);
    }

    void Update()
    {
        float t = Time.time * 2f * Mathf.PI;

        offset = Vector3.up * Wave(t, bobPeriod, bobPhase) * bobAmplitude;
        if (driftRange != 0f) offset += WorldToLocalVector(GetDriftDirection() * Wave(t, driftPeriod, driftPhase) * driftRange);
        transform.localPosition = startPosition + offset;

        float tiltX = Wave(t, tiltPeriod, tiltPhaseX) * tiltDegrees;
        float tiltZ = Wave(t, tiltPeriod * 1.3f, tiltPhaseZ) * tiltDegrees;
        float spinY = Wave(t, spinPeriod, spinPhase) * spinDegrees;
        transform.localRotation = startRotation * Quaternion.Euler(tiltX, spinY, tiltZ);
    }

    //---- Used by an Island (Lift) on the same object

    // Height (world) of the place it floats around
    public float RestHeight => transform.parent != null ? transform.parent.TransformPoint(startPosition).y : startPosition.y;

    // Moves the place it floats around (rising, falling), keeping the floating on top
    public void SetRestHeight(float worldY)
    {
        Vector3 rest = transform.parent != null ? transform.parent.TransformPoint(startPosition) : startPosition;
        rest.y = worldY;
        startPosition = transform.parent != null ? transform.parent.InverseTransformPoint(rest) : rest;
        transform.localPosition = startPosition + offset;
    }

    //----

    static float RandomPhase() => Random.value * Mathf.PI * 2f;
    static float Wave(float t, float period, float phase) => Mathf.Sin(t / Mathf.Max(period, 0.01f) + phase);

    Vector3 WorldToLocalVector(Vector3 world) => transform.parent != null ? transform.parent.InverseTransformVector(world) : world;

    Vector3 GetDriftDirection()
    {
        Vector3 direction = followWind && WindController.Instance != null ? WindController.Instance.WorldDirection : driftDirection;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
    }

#if UNITY_EDITOR
    // Selected: how far it drifts
    void OnDrawGizmosSelected()
    {
        if (driftRange == 0f) return;
        Vector3 center = transform.position;
        Vector3 reach = GetDriftDirection() * driftRange;
        Gizmos.color = new Color(0.6f, 0.8f, 1f);
        Gizmos.DrawLine(center - reach, center + reach);
        Gizmos.DrawWireSphere(center - reach, 0.5f);
        Gizmos.DrawWireSphere(center + reach, 0.5f);
    }
#endif
}
