using UnityEngine;

// Wind you can see, on the wind's object in the middle of the islands (with its sound): soft streaks that appear on
// their own at random spots of an area (a box around this object), drift along the WindController's wind with a
// gentle wave, and fade. Where each one starts, how fast it goes and how it waves are random every time.
// Same look as the old fixed emitters (WindParticles under Sky > Wind, Wind.mat): thin in the ends, wider in the middle.
// The particle system is made here when the game starts; the Scene view shows the area (selected).
public class WindEffects : MonoBehaviour
{
    [Section("Area")]
    [Tooltip("Meters: the box around this object where streaks appear")]
    public Vector3 area = new Vector3(120f, 25f, 120f);

    [Section("Streaks")]
    [Tooltip("New streaks per second, somewhere in the area: a few now and then, enough to read the wind")]
    public float perSecond = 0.6f;
    [Tooltip("Seconds each one lasts (random between x and y)")]
    public Vector2 life = new Vector2(3f, 5f);
    [Tooltip("Meters per second along the wind (random between x and y)")]
    public Vector2 speed = new Vector2(2.5f, 4.5f);
    [Tooltip("How much each one waves up and down (sideways a third of it)")]
    public float wander = 0.8f;
    [Tooltip("Seconds of path each streak draws behind it")]
    public float tailTime = 0.6f;
    [Tooltip("Meters wide in the middle")]
    public float width = 0.25f;
    public Color color = Color.white;

    [Section("References")]
    [Tooltip("Material of the streaks (Wind.mat, like before)")]
    public Material material;

    private ParticleSystem ps;
    private Vector3 lastDir;

    private void Start()
    {
        var go = new GameObject("WindStreaks");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = 0f;
        main.startSize = width;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = ps.emission;
        emission.rateOverTime = perSecond;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area;

        var velocity = ps.velocityOverLifetime;   // along the wind (set in Update), each one at its own speed
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        Blow(Wind());

        var noise = ps.noise;   // the wave: a slow field that moves, so it is never the same
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = wander / 3f;
        noise.strengthY = wander;
        noise.strengthZ = wander / 3f;
        noise.frequency = 0.15f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var size = ps.sizeOverLifetime;   // grows in and shrinks away, like before
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, Bump());

        var trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.lifetime = Mathf.Clamp01(tailTime / Mathf.Max((life.x + life.y) * 0.5f, 0.1f));
        trails.minVertexDistance = 0.2f;
        trails.worldSpace = true;
        trails.dieWithParticles = true;
        trails.inheritParticleColor = true;
        trails.sizeAffectsWidth = true;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, Bump());   // thin at both ends

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.None;   // only the streak is drawn
        renderer.trailMaterial = material != null ? material : new Material(Shader.Find("Sprites/Default"));
        if (material == null) Debug.LogWarning($"[WindEffects] {name}: no Material (Wind.mat), using a plain one.", this);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
    }

    private void Update()
    {
        Vector3 dir = Wind();
        if (dir != lastDir) Blow(dir);   // only when the wind changes
    }

    static Vector3 Wind() => WindController.Instance != null ? WindController.Instance.WorldDirection : Vector3.right;

    void Blow(Vector3 dir)
    {
        lastDir = dir;
        var velocity = ps.velocityOverLifetime;
        velocity.x = dir.x;
        velocity.y = 0f;
        velocity.z = dir.z;
    }

    // 0 → 1 in the middle → 0
    static AnimationCurve Bump() =>
        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.6f);
        Gizmos.DrawWireCube(transform.position, area);
    }
}
