// Memetica > HUD > Make UI Meme Window. Select your HUD canvas (or an object inside it) and run it: it builds the meme
// window's HUD version (HudWindow) there from the MemeWindow prefab, with the same PNGs: frame, background behind the
// hole, the meme on it, and a UI button (WindowButton) on each of its buttons (WindowControls: Minimize, Resize,
// Close, Locked), named MemeWindow. Top left, 300 tall: move and resize it as any UI (keep its proportion). Running it again rebuilds
// it (Ctrl+Z undoes it). It has its own Canvas, so the meme changing doesn't redraw the rest of the HUD.

using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class HudWindowTool
{
    const string WindowPrefabPath = "Assets/_Prefabs/MemeWindow.prefab";
    const float Height = 300f;   // canvas units
    const float MemeMargin = 0.03f;   // of the hole's height, around the meme

    [MenuItem("Memetica/HUD/Make UI Meme Window", false, 100)]
    public static void Make()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || !(selected.transform is RectTransform) || selected.GetComponentInParent<Canvas>() == null)
        { Debug.LogError("[HUD] Select your HUD canvas (or an object inside it) first."); return; }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath);
        if (prefab == null || !prefab.TryGetComponent(out WindowControls controls) || controls.picture == null || !prefab.TryGetComponent(out MemeWindow window3D) || window3D.window == null)
        { Debug.LogError($"[HUD] No meme window at {WindowPrefabPath} (Memetica > Memes > Make MemeWindow Prefab from Selected PNGs)."); return; }

        Transform frameQuad = controls.picture, contentQuad = window3D.window.transform;
        Texture frameTexture = frameQuad.GetComponent<Renderer>().sharedMaterial.mainTexture;
        Material contentMaterial = contentQuad.GetComponent<Renderer>().sharedMaterial;
        Texture background = contentMaterial != null && contentMaterial.HasProperty("_PaperTex") ? contentMaterial.GetTexture("_PaperTex") : null;

        Transform old = selected.transform.Find("MemeWindow");
        if (old == null) old = selected.transform.Find("MemeWindow_Min");   // (its name before)
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Root: the whole frame
        Vector2 frameSize = frameQuad.localScale;
        RectTransform root = MakeRect("MemeWindow", selected.transform);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "UI meme window");
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);   // top left
        root.anchoredPosition = new Vector2(40f, -40f);
        root.sizeDelta = new Vector2(Height * frameSize.x / frameSize.y, Height);
        root.gameObject.AddComponent<Canvas>();             // its own: the meme changing doesn't redraw the rest
        root.gameObject.AddComponent<GraphicRaycaster>();   // its buttons
        root.gameObject.AddComponent<CanvasGroup>();

        // Background behind the hole (positions from the 3D window: x is mirrored there, it faces the other way)
        Vector2 holeCenter = new Vector2(-(contentQuad.localPosition.x - frameQuad.localPosition.x), contentQuad.localPosition.y - frameQuad.localPosition.y);
        Vector2 holeSize = contentQuad.localScale;
        RawImage back = FillImage("Background", root, background, Color.white);
        back.rectTransform.anchorMin = new Vector2(0.5f + (holeCenter.x - holeSize.x / 2f) / frameSize.x, 0.5f + (holeCenter.y - holeSize.y / 2f) / frameSize.y);
        back.rectTransform.anchorMax = new Vector2(0.5f + (holeCenter.x + holeSize.x / 2f) / frameSize.x, 0.5f + (holeCenter.y + holeSize.y / 2f) / frameSize.y);

        // The meme: as big as it fits, inside a margin
        RectTransform inset = MakeRect("Margin", back.rectTransform);
        float margin = MemeMargin * holeSize.y / frameSize.y * Height;
        inset.anchorMin = Vector2.zero;
        inset.anchorMax = Vector2.one;
        inset.offsetMin = new Vector2(margin, margin);
        inset.offsetMax = new Vector2(-margin, -margin);
        RawImage meme = FillImage("Meme", inset, null, Color.white);
        var fitter = meme.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        meme.enabled = false;   // empty until it has a meme
        RawImage flash = FillImage("Flash", meme.rectTransform, null, new Color(1f, 1f, 1f, 0f));

        FillImage("Frame", root, frameTexture, Color.white);

        // HudWindow
        HudWindow hud = root.gameObject.AddComponent<HudWindow>();
        hud.meme = meme;
        hud.fitter = fitter;
        hud.flash = flash;
        hud.deniedClip = controls.deniedClip;
        // Its maximized size, if you made a rect for it (MaxReference): Resize grows it there and back
        Transform max = selected.transform.Find("MaxReference");
        if (max != null) hud.maxReference = (RectTransform)max;

        // A UI button on each of its buttons (Figma pixels of the frame PNG)
        Vector2 pixels = new Vector2(frameTexture.width, frameTexture.height);
        foreach (WindowControls.Button b in controls.buttons)
        {
            RectTransform rect = MakeRect(b.name, root);
            rect.anchorMin = new Vector2(b.area.xMin / pixels.x, 1f - b.area.yMax / pixels.y);
            rect.anchorMax = new Vector2(b.area.xMax / pixels.x, 1f - b.area.yMin / pixels.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);   // invisible, but it takes the click
            var button = rect.gameObject.AddComponent<WindowButton>();
            button.onHover = b.action == WindowControls.Action.Locked ? CursorState.NotAllowed : CursorState.Point;
            UnityAction call = b.action == WindowControls.Action.Minimize ? (UnityAction)hud.Minimize
                             : b.action == WindowControls.Action.Resize ? (UnityAction)hud.Resize
                             : b.action == WindowControls.Action.Close ? (UnityAction)hud.Close
                             : (UnityAction)hud.Denied;
            if (button.onClick == null) button.onClick = new UnityEvent();   // just added: not made yet
            UnityEventTools.AddPersistentListener(button.onClick, call);
        }

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[HUD] UI meme window made (top left): move and resize it where it goes (keep its proportion). Save the scene.", root);
    }

    static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    // A RawImage filling its parent
    static RawImage FillImage(string name, RectTransform parent, Texture texture, Color color)
    {
        RectTransform rect = MakeRect(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
