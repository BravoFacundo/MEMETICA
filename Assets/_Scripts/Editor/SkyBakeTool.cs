using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Memetica > Sky > Bake Sky Domes Into Skybox: select the object holding the sky domes (Sky/SkyBox, with the two
// rpgpp_lt_sky_01 inside) and run it. It photographs the domes from their middle in the six directions (only them:
// nothing else in the scene shows) into a cubemap, and that becomes the scene's skybox. The sky looks the same, but it
// is no longer an object: always behind everything, no matter how far you are or the camera's far plane.
//   Assets/_Rendering/Sky/<scene>_Sky.cubemap  the picture        <scene>_Sky.mat  its skybox material (Skybox/Cubemap)
// The light doesn't change: the ambient light came from the old skybox (Sky_Ambient, procedural); it is kept as
// three colors (Lighting > Environment > Source: Gradient) taken from that same light before the change.
// The domes are turned off, not deleted: check it, then delete them. The clouds stay objects (they move).
// Run it once per scene that has domes (Game, OriginalGame). Ctrl+Z puts the domes back on (not the Lighting values:
// the log says what they were).
public static class SkyBakeTool
{
    const string Folder = "Assets/_Rendering/Sky/";
    const int FaceSize = 1024;

    [MenuItem("Memetica/Sky/Bake Sky Domes Into Skybox")]
    static void Bake()
    {
        GameObject holder = Selection.activeGameObject;
        if (holder == null) { Debug.LogWarning("[Sky] Select the object holding the sky domes (e.g. Sky/SkyBox) first."); return; }
        Renderer[] domes = holder.GetComponentsInChildren<Renderer>(false);
        if (domes.Length == 0) { Debug.LogWarning($"[Sky] '{holder.name}' has no visible meshes inside.", holder); return; }
        if (domes.Length > 6)   // (Sky, with the clouds inside: they would be frozen into the picture and turned off)
        {
            Debug.LogWarning($"[Sky] '{holder.name}' has {domes.Length} meshes inside: select only the domes' holder (SkyBox), not the clouds.", holder);
            return;
        }

        // Only the domes: on a free layer for a moment, the camera sees that layer alone
        int layer = FreeLayer();
        if (layer < 0) { Debug.LogWarning("[Sky] No free layer to photograph the domes alone (all 32 have names)."); return; }
        var oldLayers = new int[domes.Length];
        Bounds bounds = domes[0].bounds;
        for (int i = 0; i < domes.Length; i++)
        {
            oldLayers[i] = domes[i].gameObject.layer;
            domes[i].gameObject.layer = layer;
            bounds.Encapsulate(domes[i].bounds);
        }

        var cubemap = new Cubemap(FaceSize, TextureFormat.RGBA32, false);
        var go = new GameObject("SkyBakeCamera") { hideFlags = HideFlags.HideAndDontSave };
        bool ok;
        try
        {
            Camera cam = go.AddComponent<Camera>();
            cam.transform.position = bounds.center;
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = bounds.extents.magnitude * 3f;
            ok = cam.RenderToCubemap(cubemap);
        }
        finally
        {
            Object.DestroyImmediate(go);
            for (int i = 0; i < domes.Length; i++) domes[i].gameObject.layer = oldLayers[i];
        }
        if (!ok) { Debug.LogError("[Sky] Couldn't photograph the domes (RenderToCubemap failed)."); return; }

        string scene = EditorSceneManager.GetActiveScene().name;
        string cubePath = Folder + scene + "_Sky.cubemap", matPath = Folder + scene + "_Sky.mat";
        AssetDatabase.DeleteAsset(cubePath);   // (again: a new picture)
        AssetDatabase.CreateAsset(cubemap, cubePath);

        Material sky = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Cubemap"));
            AssetDatabase.CreateAsset(sky, matPath);
        }
        sky.SetTexture("_Tex", cubemap);
        sky.SetFloat("_Exposure", 1f);
        sky.SetFloat("_Rotation", 0f);
        EditorUtility.SetDirty(sky);
        AssetDatabase.SaveAssets();

        // The same ambient light as now, as three colors (the skybox it came from is being replaced)
        string before = $"Source {RenderSettings.ambientMode}, Intensity {RenderSettings.ambientIntensity}, Skybox '{(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}'";
        var directions = new[] { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.down };
        var colors = new Color[directions.Length];
        RenderSettings.ambientProbe.Evaluate(directions, colors);
        Color equator = (colors[1] + colors[2] + colors[3] + colors[4]) * 0.25f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = colors[0];
        RenderSettings.ambientEquatorColor = equator;
        RenderSettings.ambientGroundColor = colors[5];
        RenderSettings.skybox = sky;
        DynamicGI.UpdateEnvironment();

        Undo.RecordObject(holder, "Bake Sky");
        holder.SetActive(false);
        EditorSceneManager.MarkSceneDirty(holder.scene);

        Debug.Log($"[Sky] '{holder.name}' photographed into {cubePath} ({FaceSize} px a side) and set as the skybox ({matPath}). " +
                  $"The domes are off: check it in the Game view, then delete them. Ambient light kept as Gradient: sky {colors[0]}, " +
                  $"equator {equator}, ground {colors[5]} (before: {before}). Save the scene.", sky);
    }

    static int FreeLayer()
    {
        for (int i = 31; i >= 8; i--) if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
        return -1;
    }
}
