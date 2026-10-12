using UnityEngine;

// Nube que va y viene con el viento alrededor de su posición inicial (nunca se aleja más de "range"),
// flota arriba y abajo y gira un poco hacia un lado y hacia el otro.
// Se puede poner en varias nubes a la vez (seleccionarlas todas y Add Component).
public class CloudDrift : MonoBehaviour
{
    [Header("Vaivén con el viento")]
    public float range = 1.5f;                   // metros hacia cada lado de su posición inicial
    public float period = 60f;                 // segundos por ida y vuelta
    public bool followWind = true;             // usa la dirección del WindController
    public Vector3 direction = Vector3.right;  // si no sigue el viento

    [Header("Flotación")]
    public float bobAmplitude = 0.15f;          // metros
    public float bobPeriod = 20f;              // segundos

    [Header("Giro")]
    public float spinDegrees = 1f;             // giro máximo hacia cada lado
    public float spinPeriod = 80f;             // segundos

    Vector3 startPos;
    Quaternion startRot;
    float phase, bobPhase, spinPhase;

    void Start()
    {
        startPos = transform.position;
        startRot = transform.rotation;

        // Cada nube con su ritmo
        phase = Random.value * Mathf.PI * 2f;
        bobPhase = Random.value * Mathf.PI * 2f;
        spinPhase = Random.value * Mathf.PI * 2f;
        period *= Random.Range(0.85f, 1.15f);
        bobPeriod *= Random.Range(0.85f, 1.15f);
        spinPeriod *= Random.Range(0.85f, 1.15f);
    }

    void Update()
    {
        float t = Time.time * 2f * Mathf.PI;

        transform.position = startPos
            + Direction() * Mathf.Sin(t / period + phase) * range
            + Vector3.up * Mathf.Sin(t / bobPeriod + bobPhase) * bobAmplitude;

        transform.rotation = startRot * Quaternion.Euler(0f, Mathf.Sin(t / spinPeriod + spinPhase) * spinDegrees, 0f);
    }

    Vector3 Direction()
    {
        Vector3 dir = followWind && WindController.Instance != null ? WindController.Instance.WorldDirection : direction;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;
    }

#if UNITY_EDITOR
    // Seleccionada: muestra hasta dónde va y viene
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? startPos : transform.position;
        Vector3 d = Direction() * range;
        Gizmos.color = new Color(0.6f, 0.8f, 1f);
        Gizmos.DrawLine(center - d, center + d);
        Gizmos.DrawWireSphere(center - d, 0.5f);
        Gizmos.DrawWireSphere(center + d, 0.5f);
    }
#endif
}
