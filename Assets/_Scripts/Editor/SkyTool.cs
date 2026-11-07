using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Memetica > Sky > Use Sky Gradient As Skybox (open scene): the sky stops being two domes (objects) and becomes the
// scene's skybox, drawn from the same picture (rpgpp_lt_sky_a) by Memetica/Sky Gradient: always behind everything,
// whatever the camera's far plane, and blue below the horizon too.
//   Assets/_Rendering/Sky/Sky_Gradient.mat   the skybox (one for every scene: tune it there, Curve, Exposure…)
// The light doesn't change: the ambient light came from the old skybox (Sky_Ambient, procedural, not seen); it is kept
// as three colors (Lighting > Environment > Source: Gradient) taken from that same light before the change.
// The domes (whatever shows rpgpp_lt_sky_a) are turned off, not deleted: check it, then delete them. The clouds stay.
// Ctrl+Z turns the domes back on (not the Lighting values: the log says what they were).
public static class SkyTool
{
    const string MaterialPath = "Assets/_Rendering/Sky/Sky_Gradient.mat";
    const string TexturePath = "Assets/Art/Textures/rpgpp_lt_sky_a.tga";
    const string DomeMaterial = "rpgpp_lt_sky_a";

    [MenuItem("Memetica/Sky/Use Sky Gradient As Skybox (open scene)")]
    static void Use()
    {
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (sky == null)
        {
            Shader shader = Shader.Find("Memetica/Sky Gradient");
            if (shader == null) { Debug.LogError("[Sky] Shader Memetica/Sky Gradient not found (_Rendering/Sky/SkyGradient.shader)."); return; }
            sky = new Material(shader) { name = "Sky_Gradient" };
            sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            AssetDatabase.CreateAsset(sky, MaterialPath);
            AssetDatabase.SaveAssets();
        }

        // The same ambient light as now, as three colors (the skybox it came from is being replaced)
        string before = $"Source {RenderSettings.ambientMode}, Intensity {RenderSettings.ambientIntensity}, Skybox '{(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}'";
        string ambient = "kept as it was";
        if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox && RenderSettings.skybox != sky)
        {
            var directions = new[] { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.down };
            var colors = new Color[directions.Length];
            RenderSettings.ambientProbe.Evaluate(directions, colors);
            Color equator = (colors[1] + colors[2] + colors[3] + colors[4]) * 0.25f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = colors[0];
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = colors[5];
            ambient = $"kept as Gradient: sky {colors[0]}, equator {equator}, ground {colors[5]}";
        }
        RenderSettings.skybox = sky;
        DynamicGI.UpdateEnvironment();

        // The domes: off
        int off = 0;
        foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
        {
            if (r.sharedMaterial == null || r.sharedMaterial.name != DomeMaterial) continue;
            Undo.RecordObject(r.gameObject, "Sky Domes Off");
            r.gameObject.SetActive(false);
            off++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Sky] Skybox: {MaterialPath}. {off} dome(s) turned off: check it in the Game view, then delete them. " +
                  $"Ambient light {ambient} (before: {before}). Save the scene.", sky);
    }
}
