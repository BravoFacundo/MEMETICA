using UnityEngine;

// Makes a particle system (leaves, gusts) follow the WindController direction.
// Keeps the horizontal strength and vertical part it already had; only changes where it points.
// Uses "Force over Lifetime" if enabled, otherwise "Velocity over Lifetime".
//   - Fixed values (Constant): the X/Z values are rewritten in world space.
//   - Random values (Random Between Two Constants) in Local space: the whole emitter is rotated
//     around Y instead, so the randomness and the emitter shape stay as designed.
[RequireComponent(typeof(ParticleSystem))]
public class ParticleWind : MonoBehaviour
{
    [Tooltip("Multiplies the system's original strength (fixed values only)")]
    public float strengthMultiplier = 1f;

    enum Mode { None, SetForce, SetVelocity, RotateEmitter }

    ParticleSystem ps;
    Mode mode;
    float horizontal, vertical;
    Vector3 localFlowDir;   // RotateEmitter: horizontal direction of the flow, in local space
    Quaternion baseRotation;
    Vector3 lastDir;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        baseRotation = transform.rotation;
        var force = ps.forceOverLifetime;
        var velocity = ps.velocityOverLifetime;

        if (force.enabled) Read(force.x, force.y, force.z, force.space, Mode.SetForce);
        else if (velocity.enabled) Read(velocity.x, velocity.y, velocity.z, velocity.space, Mode.SetVelocity);
        else Debug.LogWarning("[ParticleWind] '" + name + "' uses neither Force nor Velocity over Lifetime: nothing to orient.", this);
    }

    void Update()
    {
        if (WindController.Instance == null || mode == Mode.None) return;
        Vector3 dir = WindController.Instance.WorldDirection;
        if (dir == lastDir) return;          // only when the wind changes
        lastDir = dir;
        Apply(dir);
    }

    void Read(ParticleSystem.MinMaxCurve x, ParticleSystem.MinMaxCurve y, ParticleSystem.MinMaxCurve z,
              ParticleSystemSimulationSpace space, Mode fixedMode)
    {
        bool constant = x.mode == ParticleSystemCurveMode.Constant && z.mode == ParticleSystemCurveMode.Constant;
        bool twoConstants = x.mode == ParticleSystemCurveMode.TwoConstants || z.mode == ParticleSystemCurveMode.TwoConstants;

        if (constant)
        {
            // Store how much it pushes horizontally and vertically (world space), regardless of direction
            Vector3 v = new Vector3(x.constant, y.mode == ParticleSystemCurveMode.Constant ? y.constant : 0f, z.constant);
            if (space == ParticleSystemSimulationSpace.Local) v = transform.TransformDirection(v);
            horizontal = new Vector2(v.x, v.z).magnitude;
            vertical = v.y;
            mode = fixedMode;
        }
        else if (twoConstants && space == ParticleSystemSimulationSpace.Local)
        {
            // Average direction of the random range, rotated as a whole
            Vector3 mid = new Vector3(Mid(x), 0f, Mid(z));
            if (mid.sqrMagnitude < 0.0001f) { Warn("its random range averages to zero"); return; }
            localFlowDir = mid.normalized;
            mode = Mode.RotateEmitter;
        }
        else Warn("uses curves or world-space random values");
    }

    void Apply(Vector3 dir)
    {
        if (mode == Mode.RotateEmitter)
        {
            // Rotate around Y so the emitter's flow points where the wind blows
            Vector3 current = baseRotation * localFlowDir; current.y = 0f;
            transform.rotation = Quaternion.FromToRotation(current.normalized, dir) * baseRotation;
            return;
        }

        Vector3 w = dir * horizontal * strengthMultiplier + Vector3.up * vertical;
        if (mode == Mode.SetForce)
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

    static float Mid(ParticleSystem.MinMaxCurve c) =>
        c.mode == ParticleSystemCurveMode.TwoConstants ? (c.constantMin + c.constantMax) * 0.5f : c.constant;

    void Warn(string reason) =>
        Debug.LogWarning("[ParticleWind] '" + name + "' " + reason + ": left as is.", this);
}
