using System.Collections.Generic;
using UnityEngine;

// Leaves falling from a tree (its particle system): low poly like the world, a flat-shaded leaf tumbling down.
// Green: a pointed leaf folded along its middle. Autumn: an angular maple leaf in warm colors.
// They land flat on the ground and shrink away in Fade Time; the ones blown off the island shrink away at the end
// of their life. Before: the Collision module left them standing on their edge on the ground for good.
// Everything is set on the particle system when the game starts (its Collision module is turned off).
// WindParticles, on the same object, still turns them with the wind.
// The leaves are the particle system's own: Unity moves them whether this looks at them or not. This only lands them:
// each falling leaf measures once how far the ground below is (one ray), and measures again only if the wind carried it
// more than a step sideways; a last short ray when it reaches that height lands it exactly (the island floats). Before:
// one ray per falling leaf every frame. Not seen (off screen or farther than Check Distance) nothing is worked out: they
// keep falling, and the ones that went under the ground meanwhile land as soon as you see them again.
[RequireComponent(typeof(ParticleSystem))]
public class FallingLeaves : MonoBehaviour
{
    public enum Kind { Green, Autumn }

    [Section("Leaves")]
    public Kind kind = Kind.Green;
    [Tooltip("Multiplies the leaf colors (white = as they are)")]
    public Color tint = Color.white;
    [Tooltip("Seconds a leaf takes to shrink away once it lands (or at the end of its life)")]
    public float fadeTime = 1.5f;
    [Tooltip("How fast they tumble while falling (turns per second, about)")]
    public float tumble = 0.5f;
    [Tooltip("Meters from the camera: farther, the leaves aren't landed (too far to tell). They land once you come near")]
    public float checkDistance = 60f;

    [Section("References")]
    [Tooltip("Memetica/LowPolySmoke (LowPolySmoke.mat): lit by the sun, colored by each leaf")]
    public Material material;

    private const float LandLift = 0.02f;   // above the ground, so it doesn't flicker into it
    private const float Remeasure = 0.75f;  // meters a falling leaf drifts sideways before it measures the ground again
    private const float NoGround = -100000f;

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;
    private readonly List<Vector4> landed = new List<Vector4>();   // x: 0 = new, 0.5 = ground measured, 1 = landed | y: its yaw
    private readonly List<Vector4> spots = new List<Vector4>();    // landed: where | falling: x, z where it measured, y the ground's height
    private ParticleSystemRenderer leafRenderer;
    private Transform tree;

    private void Start()
    {
        ps = GetComponent<ParticleSystem>();
        leafRenderer = GetComponent<ParticleSystemRenderer>();
        tree = transform.parent != null ? transform.parent : transform;
        SetUp();
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    private void SetUp()
    {
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = Colors();
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        float spin = tumble * Mathf.PI * 2f;
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-spin, spin);
        rotation.y = new ParticleSystem.MinMaxCurve(-spin * 0.5f, spin * 0.5f);
        rotation.z = new ParticleSystem.MinMaxCurve(-spin, spin);

        var collision = ps.collision;   // landing is done here, flat
        collision.enabled = false;
        var size = ps.sizeOverLifetime;   // shrinking too (from when each one lands)
        size.enabled = false;
        var color = ps.colorOverLifetime;   // solid leaves: no fading
        color.enabled = false;

        var data = ps.customData;
        data.enabled = true;
        data.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        data.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 2);
        data.SetMode(ParticleSystemCustomData.Custom2, ParticleSystemCustomDataMode.Vector);
        data.SetVectorComponentCount(ParticleSystemCustomData.Custom2, 3);

        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = kind == Kind.Autumn ? MapleMesh() : LeafMesh();
        renderer.alignment = ParticleSystemRenderSpace.World;
        if (material != null) renderer.sharedMaterial = material;
        else Debug.LogWarning($"[FallingLeaves] {name}: no Material (LowPolySmoke.mat), the leaves keep the old one.", this);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    ParticleSystem.MinMaxGradient Colors()
    {
        Color[] c = kind == Kind.Autumn
            ? new[] { new Color(0.95f, 0.62f, 0.2f), new Color(0.86f, 0.3f, 0.15f), new Color(0.96f, 0.8f, 0.3f) }
            : new[] { new Color(0.38f, 0.62f, 0.26f), new Color(0.5f, 0.72f, 0.3f), new Color(0.3f, 0.52f, 0.24f) };
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(c[0] * tint, 0f), new GradientColorKey(c[1] * tint, 0.5f), new GradientColorKey(c[2] * tint, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
    }

    // Lands the ones reaching the ground (flat, still) and shrinks the ones that are going away
    private void LateUpdate()
    {
        if (particles == null) return;   // before Start (e.g. right after a script reload)
        if (!leafRenderer.isVisible) return;   // not seen: they fall on their own; the late ones land once seen
        Camera cam = Camera.main;
        if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > checkDistance * checkDistance) return;

        int count = ps.GetParticles(particles);
        if (count == 0) return;
        ps.GetCustomParticleData(landed, ParticleSystemCustomData.Custom1);
        ps.GetCustomParticleData(spots, ParticleSystemCustomData.Custom2);
        float dt = Time.deltaTime;

        for (int i = 0; i < count; i++)
        {
            ParticleSystem.Particle p = particles[i];
            if (landed[i].x >= 1f)
            {
                p.position = spots[i];
                p.velocity = Vector3.zero;
                p.rotation3D = new Vector3(0f, landed[i].y, 0f);
            }
            else
            {
                // The ground below it: measured once, again only if it drifted a step sideways
                Vector3 pos = p.position;
                Vector4 spot = spots[i];
                float driftX = pos.x - spot.x, driftZ = pos.z - spot.z;
                if (landed[i].x <= 0f || driftX * driftX + driftZ * driftZ > Remeasure * Remeasure)
                {
                    spot = new Vector4(pos.x, GroundBelow(pos), pos.z, 0f);
                    spots[i] = spot;
                    landed[i] = new Vector4(0.5f, 0f, 0f, 0f);
                }

                // Near that height (or already under it, while nobody looked): the exact spot, and it lands
                float fall = Mathf.Max(-p.velocity.y, 0f) * dt;
                if (spot.y > NoGround && pos.y - fall < spot.y + 0.3f)
                {
                    bool lands = Lands(p, dt, out Vector3 at);
                    if (!lands && pos.y < spot.y) { at = new Vector3(pos.x, spot.y + LandLift, pos.z); lands = true; }   // went through it
                    if (lands)
                    {
                        landed[i] = new Vector4(1f, p.rotation3D.y, 0f, 0f);
                        spots[i] = at;
                        p.position = at;
                        p.velocity = Vector3.zero;
                        p.rotation3D = new Vector3(0f, p.rotation3D.y, 0f);
                        p.remainingLifetime = Mathf.Min(p.remainingLifetime, fadeTime);
                    }
                }
            }

            // The last Fade Time of its life: down to nothing (each frame the part of the time left it still has)
            if (p.remainingLifetime < fadeTime && dt > 0f)
                p.startSize *= p.remainingLifetime / (p.remainingLifetime + dt);
            particles[i] = p;
        }

        // Custom data first: SetParticles removes the ones with no life left, and then the lists would be too long
        ps.SetCustomParticleData(landed, ParticleSystemCustomData.Custom1);
        ps.SetCustomParticleData(spots, ParticleSystemCustomData.Custom2);
        ps.SetParticles(particles, count);
    }

    // Height of the ground a leaf here would land on (not its own tree, the player or a meme), or No Ground (the void)
    float GroundBelow(Vector3 from)
    {
        for (int tries = 0; tries < 3; tries++)
        {
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return NoGround;
            if (CanLandOn(hit)) return hit.point.y;
            from = hit.point + Vector3.down * 0.05f;   // (through its tree's branches, or a meme, to what is under them)
        }
        return NoGround;
    }

    bool CanLandOn(RaycastHit hit) =>
        hit.normal.y >= 0.5f && !hit.transform.IsChildOf(tree) &&
        !(hit.collider is CharacterController) && (hit.rigidbody == null || hit.rigidbody.isKinematic);   // not on the player or a meme

    bool Lands(ParticleSystem.Particle p, float dt, out Vector3 spot)
    {
        spot = default;
        float fall = Mathf.Max(-p.velocity.y, 0f) * dt;
        const float above = 0.25f;
        if (!Physics.Raycast(p.position + Vector3.up * above, Vector3.down, out RaycastHit hit, above + fall + LandLift,
                             Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        if (!CanLandOn(hit)) return false;
        spot = hit.point + hit.normal * LandLift;
        return true;
    }

    //---- Leaf meshes: 1 m long along Z, lying flat (facing up), both sides, a flat normal per face

    static Mesh leafMesh, mapleMesh;

    // Pointed leaf, the halves folded up a bit along the middle so each one catches the light differently
    static Mesh LeafMesh()
    {
        if (leafMesh != null) return leafMesh;
        Vector3 tip = new Vector3(0f, 0f, 0.5f), stem = new Vector3(0f, 0f, -0.4f);
        Vector3 r1 = new Vector3(0.22f, 0.06f, 0.14f), r2 = new Vector3(0.17f, 0.05f, -0.18f);
        Vector3 l1 = new Vector3(-r1.x, r1.y, r1.z), l2 = new Vector3(-r2.x, r2.y, r2.z);
        leafMesh = Build("LowPolyLeaf", new[]
        {
            tip, r1, r2,   tip, r2, stem,
            tip, stem, l2, tip, l2, l1,
            new Vector3(0.02f, 0f, -0.38f), new Vector3(0f, 0f, -0.56f), new Vector3(-0.02f, 0f, -0.38f),   // the stalk
        });
        return leafMesh;
    }

    // Maple: five sharp lobes around a raised middle (faceted like a gem)
    static Mesh MapleMesh()
    {
        if (mapleMesh != null) return mapleMesh;
        Vector3[] edge =
        {
            new Vector3(0f, 0f, 0.5f), new Vector3(0.1f, 0f, 0.22f), new Vector3(0.42f, 0f, 0.26f),
            new Vector3(0.2f, 0f, 0.02f), new Vector3(0.36f, 0f, -0.22f), new Vector3(0.06f, 0f, -0.12f),
            new Vector3(0f, 0f, -0.2f),
            new Vector3(-0.06f, 0f, -0.12f), new Vector3(-0.36f, 0f, -0.22f), new Vector3(-0.2f, 0f, 0.02f),
            new Vector3(-0.42f, 0f, 0.26f), new Vector3(-0.1f, 0f, 0.22f),
        };
        Vector3 middle = new Vector3(0f, 0.05f, 0.04f);
        var corners = new List<Vector3>();
        for (int i = 0; i < edge.Length; i++) corners.AddRange(new[] { middle, edge[i], edge[(i + 1) % edge.Length] });
        corners.AddRange(new[] { new Vector3(0.02f, 0f, -0.18f), new Vector3(0f, 0f, -0.4f), new Vector3(-0.02f, 0f, -0.18f) });
        mapleMesh = Build("LowPolyMapleLeaf", corners.ToArray());
        return mapleMesh;
    }

    // Each triangle on its own (flat shading), and again turned around for the back side
    static Mesh Build(string meshName, Vector3[] corners)
    {
        int n = corners.Length;
        var vertices = new Vector3[n * 2];
        var triangles = new int[n * 2];
        for (int i = 0; i < n; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                vertices[i + k] = corners[i + k];
                vertices[n + i + k] = corners[i + 2 - k];
                triangles[i + k] = i + k;
                triangles[n + i + k] = n + i + k;
            }
        }
        var mesh = new Mesh { name = meshName, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
