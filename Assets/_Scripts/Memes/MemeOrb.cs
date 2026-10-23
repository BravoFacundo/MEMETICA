using UnityEngine;

// Meme as an orb (Game_Old): a glass bubble (this object: Memetica/MemeOrb) with the meme inside as a sticker (child
// Sticker: Memetica/MemeSticker, one shared material; each orb shows its images on it through a property block).
// When and which change shows: MemeChanger. Here only how it looks: every change happens inside a mist, a fog that
// swirls and multiplies inside the bubble while the glass clouds over (_Frost); at its thickest the image swaps
// unseen, then both clear. The sticker's size: MemeSettings' Sticker Size (every orb, in the editor too).
public class MemeOrb : MemeChanger
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly int FrostId = Shader.PropertyToID("_Frost");

    [Section("References")]
    [Tooltip("The image inside (child Sticker, with the shared MemeSticker material)")]
    public Renderer sticker;

    private MaterialPropertyBlock stickerBlock;
    private Color baseEmission;
    private Renderer shell;            // the bubble (its mist)
    private MaterialPropertyBlock shellBlock;
    private bool fogGathering;         // rising toward the change (wisps keep coming); off: they fade out fast
    private ParticleSystem innerFog;   // the fog inside the bubble
    private static Material fogMaterial;   // copy of MemeSettings' Fog Material, pulled toward the camera like the sticker

    protected override bool SetUpLook()
    {
        shell = GetComponent<Renderer>();
        shellBlock = new MaterialPropertyBlock();
        stickerBlock = new MaterialPropertyBlock();
        if (sticker == null) { Debug.LogError($"[MemeOrb] '{name}' has no Sticker reference.", this); return false; }
        sticker.transform.localScale = Vector3.one * Settings.stickerSize;
        baseEmission = sticker.sharedMaterial != null && sticker.sharedMaterial.HasProperty(EmissionId)
            ? sticker.sharedMaterial.GetColor(EmissionId) : Color.black;
        return true;
    }

    protected override void ShowTexture(Texture texture)
    {
        sticker.GetPropertyBlock(stickerBlock);
        stickerBlock.SetTexture(MainTexId, texture);
        sticker.SetPropertyBlock(stickerBlock);
    }

    // The flash: the sticker's emission up and back
    protected override void SetGlow(float amount)
    {
        sticker.GetPropertyBlock(stickerBlock);
        stickerBlock.SetColor(EmissionId, Color.Lerp(baseEmission, Color.white * Settings.flashIntensity, amount));
        sticker.SetPropertyBlock(stickerBlock);
    }

    protected override void SetCover(float amount, bool rising)
    {
        fogGathering = rising;
        SetMist(amount);
    }

    protected override void PreviewInEditor(MemeData meme)
    {
#if UNITY_EDITOR
        if (sticker == null) return;
        if (sticker.transform.localScale != Vector3.one * Settings.stickerSize)   // its size inside the bubble
        {
            UnityEditor.Undo.RecordObject(sticker.transform, "Sticker size");
            sticker.transform.localScale = Vector3.one * Settings.stickerSize;
        }
        if (meme == null || meme.baseImage == null || meme.baseImage.picture == null) return;   // (a video-only base: no preview)
        var block = new MaterialPropertyBlock();
        sticker.GetPropertyBlock(block);
        block.SetTexture(MainTexId, meme.baseImage.picture);
        sticker.SetPropertyBlock(block);
#endif
    }

    //---- Mist (the bubble's shader)

    // How much the sticker is drawn toward the camera (MemeSticker's Depth Pull): the fog in front matches it
    float StickerDepthPull => sticker != null && sticker.sharedMaterial != null && sticker.sharedMaterial.HasProperty("_DepthPull")
        ? sticker.sharedMaterial.GetFloat("_DepthPull") : 0.2f;

    // Gathering: the wisps come from the start (with the warning) and multiply, the glass clouds over with them:
    // both are at their thickest right at the swap. After it: no new wisps, the ones there fade out fast, the glass clears
    void SetMist(float value)
    {
        if (shell != null)
        {
            shell.GetPropertyBlock(shellBlock);
            shellBlock.SetFloat(FrostId, value * Mathf.Sqrt(value));
            shell.SetPropertyBlock(shellBlock);
        }

        if (value > 0f && innerFog == null) innerFog = CreateInnerFog();
        if (innerFog == null) return;
        var emission = innerFog.emission;
        emission.rateOverTime = fogGathering ? Settings.fogRate * Mathf.Sqrt(value) : 0f;
        var main = innerFog.main;
        main.simulationSpeed = fogGathering ? 1f : Settings.fogFadeSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.15f, 0.2f, value), Mathf.Lerp(0.22f, 0.32f, value));
        if (fogGathering && !innerFog.isPlaying) innerFog.Play();
    }

    // Flat cartoon puffs all through the bubble (front, back, sides; few in the middle, where the image is), swirling
    // around it in every direction. Its local space: they travel and turn with it. Drawn behind the glass
    ParticleSystem CreateInnerFog()
    {
        var go = new GameObject("InnerFog");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Settings.fogColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // sizes in the orb's units (its sphere is 1 wide)
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.38f;           // the orb's sphere is 0.5: with their size they stay inside the glass
        shape.radiusThickness = 0.45f;  // born in the outer part of that sphere, not crossing the image in the middle

        var velocity = ps.velocityOverLifetime;   // a slow whirl around the image
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalX = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);   // around every axis: a 3D swirl, not rings
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        velocity.radial = new ParticleSystem.MinMaxCurve(0f, 0f);

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        var color = ps.colorOverLifetime;   // a quick fade at both ends, solid in between
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, Puffy(0.3f, 1f, 1.1f));

        // Flat cartoon puffs (the orb is a flat drawing). Pulled toward the camera as much as the sticker is,
        // so the puffs in front of the image cover it
        if (fogMaterial == null && Settings.fogMaterial != null)
        {
            fogMaterial = new Material(Settings.fogMaterial) { name = "InnerFog" };
            fogMaterial.SetFloat("_DepthPull", StickerDepthPull);
        }
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = fogMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // A puff pops in small, swells, and shrinks away at the end (no fading, like the clouds)
    static AnimationCurve Puffy(float start, float swell, float peak) =>
        new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.25f, swell), new Keyframe(0.75f, peak), new Keyframe(1f, 0f));

}
