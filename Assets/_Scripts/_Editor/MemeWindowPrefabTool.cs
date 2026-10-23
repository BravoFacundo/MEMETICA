// Memetica > Memes: make MemeWindow prefab. Builds the meme window (see MemeWindow): a flat picture of the window in
// Art/Models/MemeWindow/MemeWindow_Frame.png (its left window, with _ □ X) on a quad, and saves it as
// Assets/_Prefabs/MemeWindow.prefab. Its material: _Rendering/MemeWindow.mat (Memetica/MemeWindow, made if it isn't there).
// Behind the picture, a body: the window's rounded rectangle pushed back Thickness meters, so it is a slab, not a
// sheet, when it falls and lies down. Its sides in the border's color (#313131, lit: _Rendering/MemeWindow_Body.mat),
// its back in the panel's grey (#DEDEDE, unlit like the picture: _Rendering/MemeWindow_Back.mat). Its mesh is saved
// as Art/Models/MemeWindow/MemeWindow_Body.asset.
// An instance is left in the open scene where the Scene view is looking, selected. It starts empty: give it a Data to
// test a meme, or put it on a randomizer pedestal (MemeRandomizer).
// Running it again rebuilds the prefab (what you changed by hand in it is replaced; the material is kept).

using UnityEditor;
using UnityEngine;

public static class MemeWindowPrefabTool
{
    const string FramePath = "Assets/Art/Models/MemeWindow/MemeWindow_Frame.png";
    const string PrefabPath = "Assets/_Prefabs/MemeWindow.prefab";
    const string MaterialPath = "Assets/_Rendering/MemeWindow.mat";
    const string BodyMaterialPath = "Assets/_Rendering/MemeWindow_Body.mat";
    const string BackMaterialPath = "Assets/_Rendering/MemeWindow_Back.mat";
    const string BodyMeshPath = "Assets/Art/Models/MemeWindow/MemeWindow_Body.asset";
    const float Width = 0.62f;       // meters wide
    const float Thickness = 0.12f;   // meters thick (body and collider)
    const int CornerSteps = 6;       // segments per rounded corner
    static readonly Color BorderColor = new Color32(0x31, 0x31, 0x31, 0xFF);
    static readonly Color PanelColor = new Color32(0xDE, 0xDE, 0xDE, 0xFF);

    [MenuItem("Memetica/Memes: make MemeWindow prefab")]
    public static void Make()
    {
        Texture2D frame = SetUpFrame();
        if (frame == null) { Debug.LogError($"[MemeWindow] No window picture at {FramePath}."); return; }
        Material material = WindowMaterial(frame);
        if (material == null) return;
        float height = Width / material.GetFloat("_Aspect");
        float corner = material.GetFloat("_Corner") * height;

        // Root: the meme (physics, the collider you grab)
        var root = new GameObject("Meme_Window") { tag = "Meme" };
        MemeWindow meme = root.AddComponent<MemeWindow>();
        root.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(Width, height, Thickness);

        // Window: faces the camera. Picture: a quad turned so its front faces the Window's forward (the camera)
        var window = new GameObject("Window");
        window.transform.SetParent(root.transform, false);
        FaceCamera facing = window.AddComponent<FaceCamera>();
        GameObject picture = GameObject.CreatePrimitive(PrimitiveType.Quad);
        picture.name = "Picture";
        Object.DestroyImmediate(picture.GetComponent<Collider>());
        picture.transform.SetParent(window.transform, false);
        picture.transform.localPosition = new Vector3(0f, 0f, Thickness * 0.5f);   // on the body's front
        picture.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        picture.transform.localScale = new Vector3(Width, height, 1f);
        var renderer = picture.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Body: behind the picture, the same rounded shape, Thickness deep
        var body = new GameObject("Body");
        body.transform.SetParent(window.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh = BodyMesh(Width, height, Thickness, corner);
        body.AddComponent<MeshRenderer>().sharedMaterials = new[]
        {
            MaterialAt(BodyMaterialPath, "Legacy Shaders/Diffuse", BorderColor),   // submesh 0: the sides
            MaterialAt(BackMaterialPath, "Unlit/Color", PanelColor),               // submesh 1: the back
        };

        meme.window = renderer;
        meme.facing = facing;

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        if (SceneView.lastActiveSceneView != null) root.transform.position = SceneView.lastActiveSceneView.pivot;
        Undo.RegisterCreatedObjectUndo(root, "Make MemeWindow");
        Selection.activeGameObject = root;
        Debug.Log($"[MemeWindow] Prefab saved at {PrefabPath} ({Width:F2} x {height:F2} m). An instance is selected in the scene: " +
                  "it starts empty, give it a Data or put it on a randomizer pedestal.", prefab);
    }

    // The window picture, imported to be sampled by part: no wrapping (its edges don't bleed), full size, sRGB
    static Texture2D SetUpFrame()
    {
        if (AssetImporter.GetAtPath(FramePath) is TextureImporter importer
            && (importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize < 2048 || importer.textureType != TextureImporterType.Default))
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(FramePath);
    }

    // The rounded rectangle (w x h, corner radius r) pushed back: its sides (submesh 0) and its back (submesh 1).
    // The front is the picture.
    // Centered, from z -depth/2 (back) to +depth/2 (front, toward the Window's forward)
    static Mesh BodyMesh(float w, float h, float depth, float r)
    {
        // The outline, counterclockwise seen from the front
        var outline = new System.Collections.Generic.List<Vector2>();
        Vector2[] centers = { new Vector2(w / 2 - r, h / 2 - r), new Vector2(-w / 2 + r, h / 2 - r),
                              new Vector2(-w / 2 + r, -h / 2 + r), new Vector2(w / 2 - r, -h / 2 + r) };
        for (int c = 0; c < 4; c++)
            for (int k = 0; k <= CornerSteps; k++)
            {
                float a = (c * 90f + 90f * k / CornerSteps) * Mathf.Deg2Rad;
                outline.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }

        var vertices = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        var backTriangles = new System.Collections.Generic.List<int>();
        float front = depth / 2, back = -depth / 2;

        // Sides: a strip around, each edge with its own outward normal (hard edges read as a solid)
        for (int i = 0; i < outline.Count; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
            Vector2 d = (b - a).normalized;
            var outward = new Vector3(d.y, -d.x, 0f);
            int v = vertices.Count;
            vertices.AddRange(new[] { new Vector3(a.x, a.y, front), new Vector3(b.x, b.y, front), new Vector3(b.x, b.y, back), new Vector3(a.x, a.y, back) });
            normals.AddRange(new[] { outward, outward, outward, outward });
            AddFacing(vertices, triangles, outward, v, v + 1, v + 2);
            AddFacing(vertices, triangles, outward, v, v + 2, v + 3);
        }

        // Back: a fan from the middle
        int middle = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, back)); normals.Add(Vector3.back);
        int first = vertices.Count;
        foreach (Vector2 p in outline) { vertices.Add(new Vector3(p.x, p.y, back)); normals.Add(Vector3.back); }
        for (int i = 0; i < outline.Count; i++)
            AddFacing(vertices, backTriangles, Vector3.back, middle, first + i, first + (i + 1) % outline.Count);

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, BodyMeshPath); }
        mesh.Clear();
        mesh.name = "MemeWindow_Body";
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(triangles, 0);
        mesh.SetTriangles(backTriangles, 1);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // A triangle wound so it is seen from the side 'facing' points to
    static void AddFacing(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> tris, Vector3 facing, int a, int b, int c)
    {
        bool towardFacing = Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), facing) > 0f;
        tris.AddRange(towardFacing ? new[] { a, b, c } : new[] { a, c, b });
    }

    // The material at that path with that shader, made with that color if it isn't there (a color you changed stays)
    static Material MaterialAt(string path, string shaderName, Color color)
    {
        Shader shader = Shader.Find(shaderName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path), color = color };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader) { material.shader = shader; material.color = color; EditorUtility.SetDirty(material); }
        return material;
    }

    // _Rendering/MemeWindow.mat with the window picture, made (or switched to the shader) if needed
    static Material WindowMaterial(Texture2D frame)
    {
        Shader shader = Shader.Find("Memetica/MemeWindow");
        if (shader == null) { Debug.LogError("[MemeWindow] No shader Memetica/MemeWindow."); return null; }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MemeWindow" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader) material.shader = shader;
        material.SetTexture("_Frame", frame);
        EditorUtility.SetDirty(material);
        return material;
    }
}
