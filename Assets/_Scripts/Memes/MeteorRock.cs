using System.Collections;
using UnityEngine;

// The meteor a meme comes back as when it falls into the void (all set in Assets/Resources/MemeSettings, Meteor sections).
// Falling: the meme is hidden inside a rock (Meteor Rock, any rock model; empty = the meme falls as itself),
// tumbling with the glow and trail. Impact: the rock bursts into small chunks thrown up inside a cloud of dark smoke,
// and the meme comes out of the cloud: no rock is left. Then a thin, lighter smoke trail rises from the meme itself
// until it is picked up or placed, so you spot it from anywhere. All low poly puffs, like the world.
public class MeteorRock : MonoBehaviour
{
    private static MemeSettings Settings => MemeSettings.Instance;
    private static Material smokeMaterial;   // copy of MemeSettings' Smoke Material

    private Meme meme;
    private GameObject model;
    private ParticleSystem smoke;
    private float size;

    // The meme hides its own parts inside it (false: no rock assigned, the meme falls as itself)
    public bool HasModel => model != null;

    // The meteor around the meme, riding with it while it falls
    public static MeteorRock Spawn(Meme meme)
    {
        var go = new GameObject("Meteor");
        go.transform.position = meme.transform.position;
        var meteor = go.AddComponent<MeteorRock>();
        meteor.meme = meme;
        meteor.size = Settings.meteorRockSize;

        if (Settings.meteorRock != null)
        {
            meteor.model = Instantiate(Settings.meteorRock, go.transform.position, Random.rotation, go.transform);
            foreach (Collider c in meteor.model.GetComponentsInChildren<Collider>()) Destroy(c);   // the meme is what you touch
            Renderer[] parts = meteor.model.GetComponentsInChildren<Renderer>();   // scaled to Meteor Rock Size, whatever the model
            if (parts.Length > 0)
            {
                Bounds b = parts[0].bounds;
                foreach (Renderer r in parts) b.Encapsulate(r.bounds);
                float biggest = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (biggest > 0.0001f) meteor.model.transform.localScale *= Settings.meteorRockSize / biggest;
            }
        }

        go.transform.SetParent(meme.transform, true);
        return meteor;
    }

    // Hit the ground at point: the rock bursts (chunks flying up, hidden by the dark cloud), the meme starts smoking
    public void Land(Vector3 point)
    {
        transform.SetParent(null, true);
        if (model != null)
        {
            Destroy(model);   // gone: the chunks take its place
            DebrisBurst.Spawn(new[] { Settings.meteorRock }, Settings.meteorChunks, point + Vector3.up * size * 0.4f,
                              Vector3.up, 7f, 0.7f, size * 0.3f);
        }
        ImpactCloud(point);
        ImpactRing(point);

        smoke = CreateSmoke();
        smoke.transform.position = SmokeSpot();
        smoke.Play();
        StartCoroutine(WaitAndGo());
    }

    // Until the meme is picked up or placed the smoke follows it (rising from just above it, wherever it rolls);
    // then no more smoke, the last puffs rise and go away
    IEnumerator WaitAndGo()
    {
        while (meme != null && !meme.IsHeld && meme.CurrentPedestal == null)
        {
            smoke.transform.position = SmokeSpot();
            yield return null;
        }
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(smoke.gameObject, smoke.main.startLifetime.constantMax + 1f);
        Destroy(gameObject);
    }

    // Just above the orb, so the puffs don't cover it
    Vector3 SmokeSpot() => meme.transform.position + Vector3.up * size * 0.6f;

    // Thin and broken, thicker up high, rising straight like a campfire: tall, so you spot it from anywhere (zooming if needed)
    ParticleSystem CreateSmoke()
    {
        var go = new GameObject("MeteorSmoke");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 5f;
        float life = Settings.smokeHeight / Mathf.Max(Settings.smokeRiseSpeed, 0.1f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.85f, life * 1.15f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(Settings.smokeSize * 0.35f, Settings.smokeSize * 0.6f);   // a thin trail
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Settings.smokeColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;   // the pillar keeps growing while you look elsewhere

        var emission = ps.emission;   // small groups now and then: a broken trail, not a solid column
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1, (short)3, 0, Settings.smokeGap) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var velocity = ps.velocityOverLifetime;   // straight up, a column, whatever way the orb is turned
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(Settings.smokeRiseSpeed * 0.8f, Settings.smokeRiseSpeed * 1.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, Puffy(0.4f, 0.8f, 1.9f));   // thin low, thicker up high, then shrinks away

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.25f;

        LowPoly(ps, ref smokeMaterial, "Smoke", 0f);
        return ps;
    }

    // The cloud the rock breaks in: dark puffs, big from the first frame, over the meme while it comes out of the rock
    static void ImpactCloud(Vector3 point)
    {
        var go = new GameObject("ImpactCloud");
        go.transform.position = point + Vector3.up * 0.5f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(Settings.smokeSize * 0.35f, Settings.smokeSize * 0.6f);
        main.startColor = Settings.impactSmokeColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;   // a ball around the meme, puffs going out
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var velocity = ps.velocityOverLifetime;   // and drifting up
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var slow = ps.limitVelocityOverLifetime;   // it hangs there a moment
        slow.enabled = true;
        slow.limit = 0.6f;
        slow.dampen = 0.25f;

        var size = ps.sizeOverLifetime;   // already big when it appears: it hides the swap
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.8f), new Keyframe(0.1f, 1.1f), new Keyframe(0.6f, 0.9f), new Keyframe(1f, 0f)));

        LowPoly(ps, ref smokeMaterial, "Smoke", 0f);
        ps.Play();
        Destroy(go, 2.5f);
    }

    // A ring of dark puffs bursting along the ground and slowing down (on its own, it cleans itself up)
    static void ImpactRing(Vector3 point)
    {
        var go = new GameObject("ImpactSmoke");
        go.transform.position = point;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(Settings.smokeSize * 0.4f, Settings.smokeSize * 0.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Settings.impactSmokeColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        var shape = ps.shape;   // a flat ring on the ground, puffs going outward
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.4f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var velocity = ps.velocityOverLifetime;   // and a little up
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var slow = ps.limitVelocityOverLifetime;
        slow.enabled = true;
        slow.limit = 0.5f;
        slow.dampen = 0.15f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, Puffy(0.6f, 1.2f, 1.3f));

        LowPoly(ps, ref smokeMaterial, "Smoke", 0f);
        ps.Play();
        Destroy(go, 3f);
    }


    // A solid puff pops in small, swells, and shrinks away at the end (no fading, like the clouds)
    static AnimationCurve Puffy(float start, float swell, float peak) =>
        new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.25f, swell), new Keyframe(0.75f, peak), new Keyframe(1f, 0f));

    // Low poly puffs: every particle is a small flat-shaded icosahedron (solid grey, like the clouds), lit by the sun,
    // turning on every axis. Their material is a copy of MemeSettings' Smoke Material (Memetica/LowPolySmoke)
    static void LowPoly(ParticleSystem ps, ref Material copy, string name, float depthPull)
    {
        if (copy == null && Settings.smokeMaterial != null)
        {
            copy = new Material(Settings.smokeMaterial) { name = name };
            if (copy.HasProperty("_DepthPull")) copy.SetFloat("_DepthPull", depthPull);
        }
        var main = ps.main;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = PuffMesh();
        renderer.alignment = ParticleSystemRenderSpace.World;   // each puff turns on its own, whatever its holder does
        renderer.material = copy;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // An icosahedron 1 m wide with a flat normal per face (vertices not shared): the low poly look
    static Mesh puffMesh;
    static Mesh PuffMesh()
    {
        if (puffMesh != null) return puffMesh;
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] corners =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        int[] faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        var vertices = new Vector3[faces.Length];
        var triangles = new int[faces.Length];
        for (int i = 0; i < faces.Length; i++) { vertices[i] = corners[faces[i]].normalized * 0.5f; triangles[i] = i; }
        puffMesh = new Mesh { name = "LowPolyPuff", vertices = vertices, triangles = triangles };
        puffMesh.RecalculateNormals();
        puffMesh.RecalculateBounds();
        return puffMesh;
    }
}
