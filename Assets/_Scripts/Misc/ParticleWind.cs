using UnityEngine;

// Hace que un sistema de partículas (hojas, ráfagas) siga la dirección del WindController.
// Conserva la fuerza horizontal y la parte vertical que ya tenía configuradas; solo cambia hacia dónde van.
// Usa "Force over Lifetime" si está activo; si no, "Velocity over Lifetime".
[RequireComponent(typeof(ParticleSystem))]
public class ParticleWind : MonoBehaviour
{
    [Tooltip("Multiplica la fuerza original del sistema")]
    public float strengthMultiplier = 1f;

    ParticleSystem ps;
    bool useForce, useVelocity;
    float horizontal, vertical;
    Vector3 lastDir;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        var force = ps.forceOverLifetime;
        var velocity = ps.velocityOverLifetime;

        if (force.enabled) { useForce = true; Read(force.x, force.y, force.z, force.space); }
        else if (velocity.enabled) { useVelocity = true; Read(velocity.x, velocity.y, velocity.z, velocity.space); }
        else Debug.LogWarning("[ParticleWind] '" + name + "' no usa Force ni Velocity over Lifetime: no hay nada que orientar.", this);
    }

    void Update()
    {
        if (WindController.Instance == null || (!useForce && !useVelocity)) return;
        Vector3 dir = WindController.Instance.WorldDirection;
        if (dir == lastDir) return;          // solo cuando cambia el viento
        lastDir = dir;
        Apply(dir);
    }

    // Guarda cuánto empuja en horizontal y en vertical (en mundo), sin importar hacia dónde apuntaba
    void Read(ParticleSystem.MinMaxCurve x, ParticleSystem.MinMaxCurve y, ParticleSystem.MinMaxCurve z, ParticleSystemSimulationSpace space)
    {
        if (x.mode != ParticleSystemCurveMode.Constant || z.mode != ParticleSystemCurveMode.Constant)
        {
            Debug.LogWarning("[ParticleWind] '" + name + "' usa curvas en X/Z (no un valor fijo): no lo toco.", this);
            useForce = useVelocity = false;
            return;
        }
        Vector3 v = new Vector3(x.constant, y.mode == ParticleSystemCurveMode.Constant ? y.constant : 0f, z.constant);
        if (space == ParticleSystemSimulationSpace.Local) v = transform.TransformDirection(v);
        horizontal = new Vector2(v.x, v.z).magnitude;
        vertical = v.y;
    }

    void Apply(Vector3 dir)
    {
        Vector3 w = dir * horizontal * strengthMultiplier + Vector3.up * vertical;
        if (useForce)
        {
            var f = ps.forceOverLifetime;
            f.space = ParticleSystemSimulationSpace.World;
            f.x = w.x; f.y = w.y; f.z = w.z;
        }
        else
        {
            var v = ps.velocityOverLifetime;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = w.x; v.y = w.y; v.z = w.z;
        }
    }
}
