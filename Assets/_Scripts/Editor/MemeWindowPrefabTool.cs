// Memetica > Memes > Make MemeWindow Prefab from Selected PNGs. Select in the Project your frame PNG (its middle
// transparent: the hole) and, optionally, the PNG of the empty window's background (behind the meme) and its back
// (named with Back / Reverse / Reverso). Builds
// Assets/_Prefabs/MemeWindow.prefab (see MemeWindow):
//   Frame     your frame, in front                     (Unlit/Transparent, _Rendering/Windows/<frame>.mat)
//   Content   behind the hole: the background, and the meme on it (Memetica/MemeWindow, _Rendering/Windows/<frame>_Content.mat)
//   Body      the box behind, its depth, rounded like the frame's corners (WindowBuilder)
//   <back>    your back PNG on the box's back
// Width meters wide (the frame's opaque part). Running it again rebuilds it but keeps what you set on its MemeWindow
// and WindowControls (the buttons, sounds). An instance is left in the open scene, selected.

using UnityEditor;
using UnityEngine;

public static class MemeWindowPrefabTool
{
    const string PrefabPath = "Assets/_Prefabs/MemeWindow.prefab";
    const string ContentShader = "Memetica/MemeWindow";
    const float Width = 0.62f;   // meters wide

    [MenuItem("Memetica/Memes/Make MemeWindow Prefab from Selected PNGs", false, 201)]
    public static void Make()
    {
        Texture2D[] selected = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
        Texture2D frame = null, background = null, back = null;
        WindowBuilder.Shape shape = default;
        foreach (Texture2D texture in selected)
        {
            if (back == null && WindowBuilder.IsBack(texture)) { back = texture; continue; }
            WindowBuilder.Shape s = WindowBuilder.Analyze(texture);
            if (frame == null && s.hasHole) { frame = texture; shape = s; }
            else if (background == null) background = texture;
        }
        if (frame == null) { Debug.LogError("[MemeWindow] Select your frame PNG (its middle transparent) and, if you want, the background PNG."); return; }
        WindowBuilder.SetUpTexture(frame);
        if (background != null) WindowBuilder.SetUpTexture(background);
        if (back != null) WindowBuilder.SetUpTexture(back);

        float metersPerPixel = Width / shape.opaque.width;
        float depth = Width * WindowBuilder.DepthRatio;
        float gap = Width * 0.01f;   // between the layers, so they never fight

        // What you set on the old one
        GameObject old = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        string oldMeme = old != null && old.TryGetComponent(out MemeWindow m) ? EditorJsonUtility.ToJson(m) : null;
        string oldControls = old != null && old.TryGetComponent(out WindowControls c) ? EditorJsonUtility.ToJson(c) : null;

        // Root: the meme (physics, the collider you grab)
        var root = new GameObject("Meme_Window") { tag = "Meme" };
        MemeWindow meme = root.AddComponent<MemeWindow>();
        root.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
        WindowControls controls = root.AddComponent<WindowControls>();

        var window = new GameObject("Window");
        window.transform.SetParent(root.transform, false);
        FaceCamera facing = window.AddComponent<FaceCamera>();

        // Body, then the Content behind the hole, then the Frame in front
        WindowBuilder.ToMeters(shape.opaque, shape.size, metersPerPixel, out Vector2 bodyCenter, out Vector2 bodySize);
        WindowBuilder.Body(window.transform, frame.name, bodyCenter, bodySize, shape.radius * metersPerPixel, depth, gap * 2f, 0);
        var box = root.AddComponent<BoxCollider>();
        box.center = WindowBuilder.InWindow(bodyCenter, 0f);
        box.size = new Vector3(bodySize.x, bodySize.y, depth);

        RectInt hole = shape.hole;
        hole = new RectInt(hole.x - 2, hole.y - 2, hole.width + 4, hole.height + 4);   // under the frame's edge: no seam
        WindowBuilder.ToMeters(hole, shape.size, metersPerPixel, out Vector2 holeCenter, out Vector2 holeSize);
        Transform content = WindowBuilder.Layer(window.transform, "Content", ContentMaterial(frame, background), holeCenter, holeSize, depth / 2 - gap, 0);   // recessed

        var whole = new RectInt(0, 0, shape.size.x, shape.size.y);
        WindowBuilder.ToMeters(whole, shape.size, metersPerPixel, out Vector2 frameCenter, out Vector2 frameSize);
        if (back != null) WindowBuilder.BackLayer(window.transform, back, metersPerPixel, depth, 0);
        Transform framed = WindowBuilder.Layer(window.transform, "Frame", WindowBuilder.LayerMaterial(frame), frameCenter, frameSize, depth / 2, 0);   // flush on the body

        // Settings kept, references new
        if (oldMeme != null) EditorJsonUtility.FromJsonOverwrite(oldMeme, meme);
        if (oldControls != null) EditorJsonUtility.FromJsonOverwrite(oldControls, controls);
        meme.window = content.GetComponent<Renderer>();
        meme.facing = facing;
        meme.data = null;   // it starts empty (the randomizer gives it its meme)
        controls.picture = framed;
        if (oldControls == null) DefaultButtons(controls, shape);

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        if (SceneView.lastActiveSceneView != null) root.transform.position = SceneView.lastActiveSceneView.pivot;
        Undo.RegisterCreatedObjectUndo(root, "Make MemeWindow");
        Selection.activeGameObject = root;
        Debug.Log($"[MemeWindow] Prefab saved at {PrefabPath} ({bodySize.x:F2} x {bodySize.y:F2} x {depth:F2} m, corners {shape.radius} px). " +
                  "Set its buttons on WindowControls (Figma pixels: the yellow frames).", prefab);
    }

    // _Rendering/Windows/<frame>_Content.mat: the meme on the background
    static Material ContentMaterial(Texture2D frame, Texture2D background)
    {
        WindowBuilder.Folder("Assets/_Rendering/Windows");
        string path = $"Assets/_Rendering/Windows/{frame.name}_Content.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(ContentShader)) { name = frame.name + "_Content" };
            AssetDatabase.CreateAsset(material, path);
        }
        if (background != null) material.SetTexture("_PaperTex", background);
        EditorUtility.SetDirty(material);
        return material;
    }

    // A first guess (top right: minimize, resize, close locked): move them where your PNG has them
    static void DefaultButtons(WindowControls controls, WindowBuilder.Shape shape)
    {
        float s = shape.opaque.width * 0.08f;
        float top = shape.size.y - (shape.opaque.y + shape.opaque.height) + s * 0.4f;   // from the top, as in Figma
        float right = shape.opaque.x + shape.opaque.width - s * 0.4f;
        controls.buttons.Clear();
        controls.buttons.Add(new WindowControls.Button { name = "Minimize", action = WindowControls.Action.Minimize, area = new Rect(right - s * 3.3f, top, s, s) });
        controls.buttons.Add(new WindowControls.Button { name = "Resize", action = WindowControls.Action.Resize, area = new Rect(right - s * 2.2f, top, s, s) });
        controls.buttons.Add(new WindowControls.Button { name = "Close", action = WindowControls.Action.Locked, area = new Rect(right - s * 1.1f, top, s, s) });
    }
}
