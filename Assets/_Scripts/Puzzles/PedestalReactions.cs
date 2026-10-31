using UnityEngine;

// The pedestal's reactions, as icons that pop out above it and float up (like the orb's puffs):
//   Question   question marks while it thinks: a white pedestal's sweep, the raffle, a group waiting for the rest
//   Like       it takes the meme (a black one: right away)
//   Dislike    it doesn't
// Your cursor pictures (sprites). Goes on a Pedestal, which calls it. With it, the old sparks on the Socket don't play.
[RequireComponent(typeof(Pedestal))]
public class PedestalReactions : MonoBehaviour
{
    [Header("Icons")]
    public Sprite like;
    public Sprite dislike;
    public Sprite question;

    [Header("Look")]
    [Tooltip("Meters above the socket where they come out (over the meme)")]
    public float height = 1.2f;
    [Tooltip("Meters wide each icon")]
    public float size = 0.45f;
    [Tooltip("Meters around that point they come out from")]
    public float spread = 0.5f;
    [Tooltip("Meters per second they rise")]
    public float riseSpeed = 1f;
    [Tooltip("Seconds each one lasts")]
    public float lifetime = 1.3f;
    [Tooltip("Like / Dislike: how many at once")]
    public int burst = 5;
    [Tooltip("Question: how many per second while it thinks")]
    public float questionRate = 3f;
    [Tooltip("Order in Layer: over the meme window (its frame and title have their own order, 10 the title: under it " +
             "the icons looked as if inside it)")]
    public int sortingOrder = 20;

    private ParticleSystem likes, dislikes, questions;

    void Awake()
    {
        Pedestal pedestal = GetComponent<Pedestal>();
        Transform at = pedestal.socket != null ? pedestal.socket : transform;
        likes = Create("Likes", like, at);
        dislikes = Create("Dislikes", dislike, at);
        questions = Create("Questions", question, at);
    }

    public void Like() { Think(false); Burst(likes); }
    public void Dislike() { Think(false); Burst(dislikes); }

    public void Think(bool on)
    {
        if (questions == null) return;
        var emission = questions.emission;
        emission.rateOverTime = on ? questionRate : 0f;
        if (on && !questions.isPlaying) questions.Play();
    }

    void Burst(ParticleSystem ps)
    {
        if (ps == null) return;
        if (!ps.isPlaying) ps.Play();
        ps.Emit(burst);
    }

    // A system of flat icons facing the camera, floating up and away (world space: they stay as the pedestal moves)
    ParticleSystem Create(string systemName, Sprite sprite, Transform at)
    {
        if (sprite == null) return null;
        var go = new GameObject(systemName);
        go.transform.SetParent(at, false);
        go.transform.localPosition = at.InverseTransformVector(Vector3.up * height);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // its circle lies flat
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);   // a little tilted, each its own way
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;   // sizes in meters, whatever the pedestal's scale
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = spread;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(riseSpeed * 0.7f, riseSpeed);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        var sizeOver = ps.sizeOverLifetime;   // pops in small, swells, shrinks away (as the orb's puffs)
        sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1.15f), new Keyframe(0.35f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

        var sheet = ps.textureSheetAnimation;   // the sprite itself (its part of an atlas too)
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        sheet.AddSprite(sprite);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Sprites/Default")) { name = systemName, mainTexture = sprite.texture };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = sortingOrder;
        return ps;
    }
}
