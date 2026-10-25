// Memetica > HUD: the fake HUD laid out on a canvas (see HudLayout).
//   Layout canvas   select your canvas (Screen Space Overlay), or an object inside one, and run it: it gets a HudLayout and an image for each
//                   element that has none (DropPivot, GrabPivot), where it shows now.
//                   Nothing selected: it uses the scene's layout canvas, or makes Canvas_HudLayout. Then just move and
//                   resize the images: the pivots follow
//   Apply layout canvas   places everything once (with Ctrl+Z), e.g. with Live off

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HudLayoutTool
{
    const string CanvasName = "Canvas_HudLayout";
    const string WindowPrefabPath = "Assets/_Prefabs/MemeWindow.prefab";

    [MenuItem("Memetica/HUD/Layout Canvas", false, 101)]
    public static void MakeOrRefresh()
    {
        HudLayout layout = FindOrMake();
        if (layout == null) return;
        if (!layout.Elements(out _)) { Debug.LogError("[HUD] No Main Camera in the open scene."); return; }

        GameObject window = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath);
        Texture windowTexture = window != null && window.TryGetComponent(out WindowControls controls) ? Picture(controls) : null;
        Undo.RecordObject(layout, "HUD layout");
        if (window != null) layout.windowHeight = HudLayout.Height(window.transform);

        layout.AddMissingImages(e =>
            e.kind == HudLayout.Kind.Drop ? null
          : windowTexture);

        EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
        Selection.activeGameObject = layout.gameObject;
        Debug.Log($"[HUD] '{layout.name}' ready: move and resize its images (named like what they place) and the 3D elements follow. Save the scene.", layout);
    }

    [MenuItem("Memetica/HUD/Apply Layout Canvas", false, 102)]
    public static void Apply()
    {
        HudLayout layout = Object.FindObjectOfType<HudLayout>(true);
        if (layout == null) { Debug.LogError("[HUD] No layout canvas: run Memetica > HUD > Layout Canvas first."); return; }
        int placed = layout.Apply(true);
        EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
        Debug.Log($"[HUD] {placed} element(s) placed from '{layout.name}'. Save the scene (and apply the Player prefab if you want it in every scene).");
    }

    // The selected canvas, the scene's layout canvas, or a new one
    static HudLayout FindOrMake()
    {
        // The selected canvas, or any object inside one (e.g. Canvas_Grab under Canvas)
        GameObject selected = Selection.activeGameObject;
        Canvas parentCanvas = selected != null && selected.transform is RectTransform ? selected.GetComponentInParent<Canvas>() : null;
        if (parentCanvas != null)
        {
            if (parentCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) { Debug.LogError("[HUD] The layout canvas must be Screen Space - Overlay."); return null; }
            if (!selected.TryGetComponent(out HudLayout own)) own = Undo.AddComponent<HudLayout>(selected);
            Undo.RecordObject(selected, "HUD layout");
            if (selected.CompareTag("EditorOnly")) selected.tag = "Untagged";   // it places everything when the game starts: in the build
            return own;
        }
        HudLayout found = Object.FindObjectOfType<HudLayout>(true);
        if (found != null)
        {
            if (found.CompareTag("EditorOnly")) { Undo.RecordObject(found.gameObject, "HUD layout"); found.gameObject.tag = "Untagged"; }
            return found;
        }

        var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudLayout));
        Undo.RegisterCreatedObjectUndo(go, "HUD layout");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Canvas.ForceUpdateCanvases();
        return go.GetComponent<HudLayout>();
    }

    static Texture Picture(WindowControls controls) =>
        controls.picture != null && controls.picture.TryGetComponent(out Renderer r) && r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null;
}
