// Memetica > Level > Portal > New Portal Window: the way back to the hub (PortalWindow), on the ground where the Scene
// view's middle points, inside that island, its top toward where the view looks. It is your meme window lying on the
// ground (the frame PNG of Assets/_Prefabs/MemeWindow; without it, only the screen), Size meters wide, with the live
// view of the hub in its hole (loading in from the empty window's PNG). Its Exit: the LevelManager's Start Respawn Point (the hub), if it is a RespawnPoint.
// Then set Open After (the target island's pedestal, PedestalRetire Swallowed). Ctrl+Z undoes it. Save the scene.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PortalWindowTool
{
    const string WindowPrefabPath = "Assets/_Prefabs/MemeWindow.prefab";
    const string MaterialPath = "Assets/_Rendering/PortalScreen.mat";
    const float Width = 4.5f;   // meters

    [MenuItem("Memetica/Level/Portal/New Portal Window", false, 350)]
    static void Make()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) { Debug.LogError("[Portal] Open a Scene view first."); return; }
        Ray ray = sceneView.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
        { Debug.LogError("[Portal] No ground in the middle of the Scene view: point it at the target island."); return; }

        // The frame and where its hole is (as the HUD window: Memetica > HUD > Make UI Meme Window)
        Material frameMaterial = null;
        Texture emptyTexture = null;
        Vector2 frameSize = new Vector2(1f, 1f), holeCenter = Vector2.zero, holeSize = new Vector2(1f, 1f);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath);
        if (prefab != null && prefab.TryGetComponent(out WindowControls controls) && controls.picture != null &&
            prefab.TryGetComponent(out MemeWindow window3D) && window3D.window != null)
        {
            Transform frame = controls.picture, content = window3D.window.transform;
            frameMaterial = frame.GetComponent<Renderer>().sharedMaterial;
            frameSize = frame.localScale;
            holeCenter = new Vector2(-(content.localPosition.x - frame.localPosition.x), content.localPosition.y - frame.localPosition.y);
            holeSize = content.localScale;
            Material contentMaterial = content.GetComponent<Renderer>().sharedMaterial;
            if (contentMaterial != null && contentMaterial.HasProperty("_PaperTex")) emptyTexture = contentMaterial.GetTexture("_PaperTex");
        }
        else Debug.LogWarning($"[Portal] No meme window at {WindowPrefabPath}: only the screen, no frame.");

        float width = Width, height = Width * frameSize.y / frameSize.x;
        var root = new GameObject("PortalWindow");
        Undo.RegisterCreatedObjectUndo(root, "New Portal Window");
        Transform island = IslandOf(hit.transform);
        if (island != null) root.transform.SetParent(island, true);
        Vector3 facing = Vector3.ProjectOnPlane(sceneView.camera.transform.forward, Vector3.up);
        root.transform.SetPositionAndRotation(hit.point + Vector3.up * 0.03f, Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward, Vector3.up));

        // What you stand on: its top at the screen
        var floor = root.AddComponent<BoxCollider>();
        floor.center = new Vector3(0f, -0.15f, 0f);
        floor.size = new Vector3(width, 0.3f, height);

        // Lying flat, facing up
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
        if (frameMaterial != null)
        {
            Transform frame = Quad("Frame", visual, frameMaterial);
            frame.localScale = new Vector3(width, height, 1f);
        }
        Transform screen = Quad("Screen", visual, ScreenMaterial());
        screen.localPosition = new Vector3(holeCenter.x / frameSize.x * width, holeCenter.y / frameSize.y * height, 0.004f);   // (just under the frame)
        screen.localScale = new Vector3(holeSize.x / frameSize.x * width, holeSize.y / frameSize.y * height, 1f);

        var portal = root.AddComponent<PortalWindow>();
        portal.screen = screen.GetComponent<Renderer>();
        portal.floor = floor;
        portal.emptyTexture = emptyTexture;
        LevelManager level = Object.FindObjectOfType<LevelManager>();
        if (level != null && level.startRespawnPoint != null) portal.exit = level.startRespawnPoint.GetComponent<RespawnPoint>();

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[Portal] Portal window on '{hit.transform.name}'" + (portal.exit != null ? $", leading to '{portal.exit.name}'" : " (set its Exit: a RespawnPoint in the hub)") +
                  ". Set Open After: the target island's pedestal.", root);
    }

    static Transform Quad(string name, Transform parent, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        var r = quad.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return quad.transform;
    }

    static Material ScreenMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;
        Shader shader = Shader.Find("Memetica/PortalScreen");
        if (shader == null) { Debug.LogError("[Portal] Shader Memetica/PortalScreen not found (Assets/_Rendering/PortalScreen.shader)."); return null; }
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    // The floating part of the island (it floats with it), else the island
    static Transform IslandOf(Transform t)
    {
        FloatMotion floating = t.GetComponentInParent<FloatMotion>();
        if (floating != null) return floating.transform;
        Island island = t.GetComponentInParent<Island>();
        if (island != null) return island.transform;
        for (Transform p = t; p != null; p = p.parent) if (p.CompareTag("Isla")) return p;
        return null;
    }
}
