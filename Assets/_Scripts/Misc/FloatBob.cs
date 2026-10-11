using UnityEngine;

// Flotación lenta de islas pesadas: sube, baja y se balancea apenas,
// siempre alrededor de su posición y rotación iniciales (nunca se desplaza).
public class FloatBob : MonoBehaviour
{
    [Header("Subida y bajada")]
    public float amplitude = 0.8f;    // metros hacia arriba y hacia abajo
    public float period = 16f;        // segundos por ciclo completo (más alto = más lento)

    [Header("Balanceo")]
    public float tiltDegrees = 1.2f;  // inclinación máxima
    public float tiltPeriod = 22f;    // más lento que la subida, para que no se sincronicen

    private Vector3 startPos;
    private Quaternion startRot;
    private float phase, phaseX, phaseZ;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;

        // Cada isla arranca en un momento distinto y con un ritmo apenas diferente
        phase = Random.value * Mathf.PI * 2f;
        phaseX = Random.value * Mathf.PI * 2f;
        phaseZ = Random.value * Mathf.PI * 2f;
        period *= Random.Range(0.85f, 1.15f);
        tiltPeriod *= Random.Range(0.85f, 1.15f);
    }

    void Update()
    {
        float t = Time.time * 2f * Mathf.PI;

        float y = Mathf.Sin(t / period + phase) * amplitude;
        transform.localPosition = startPos + Vector3.up * y;

        // Dos ejes con ritmos distintos (uno 30 % más lento) dan un balanceo orgánico, no un péndulo
        float tiltX = Mathf.Sin(t / tiltPeriod + phaseX) * tiltDegrees;
        float tiltZ = Mathf.Sin(t / (tiltPeriod * 1.3f) + phaseZ) * tiltDegrees;
        transform.localRotation = startRot * Quaternion.Euler(tiltX, 0f, tiltZ);
    }
}