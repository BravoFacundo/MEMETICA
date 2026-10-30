// Memetica > UI > Make MemeDex Window. Select a canvas (or an object inside it) and run it: it builds the MemeDex
// window there (MemeDexView), almost the whole screen (a margin for the CRT's edges), named MemeDexWindow:
//   MemeDexWindow (Image: the panel, MemeDexView)
//   ├── Header: Title, Mode (whose memes and how many), Global button, Name input, Show button
//   └── Scroll (ScrollRect): Viewport > Content (the cells are made there) and the vertical Scrollbar on the right
// Move, resize and restyle anything (frame PNGs, fonts): the view only needs its references. Its catalog is copied from
// the scene's MemeDex if there is one (needed in the Menu, which has none). Running it again rebuilds it (Ctrl+Z
// undoes it). Save the scene after.

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MemeDexWindowTool
{
    const float Margin = 0.05f;   // of the screen, all around (CRT overscan)

    [MenuItem("Memetica/UI/Make MemeDex Window", false, 120)]
    static void Make()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || !(selected.transform is RectTransform) || selected.GetComponentInParent<Canvas>() == null)
        { Debug.LogError("[MemeDex] Select a canvas (or an object inside it) first."); return; }

        Transform old = selected.transform.Find("MemeDexWindow");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var resources = new DefaultControls.Resources();
        var tmpResources = new TMP_DefaultControls.Resources();

        // The panel
        var root = new GameObject("MemeDexWindow", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "MemeDex window");
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(selected.transform, false);
        rootRect.anchorMin = new Vector2(Margin, Margin);
        rootRect.anchorMax = new Vector2(1f - Margin, 1f - Margin);
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        MemeDexView view = root.AddComponent<MemeDexView>();

        // Header: title, mode, Global, name, Show
        RectTransform header = Rect("Header", rootRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), Vector2.zero);
        TMP_Text title = Text("Title", header, "MemeDex", 34, TextAlignmentOptions.Left);
        Place(title.rectTransform, 0f, 0.3f);
        TMP_Text mode = Text("Mode", header, "Everyone", 20, TextAlignmentOptions.Left);
        Place(mode.rectTransform, 0.3f, 0.55f);

        GameObject global = TMP_DefaultControls.CreateButton(tmpResources);
        global.name = "GlobalButton";
        global.transform.SetParent(header, false);
        global.GetComponentInChildren<TMP_Text>().text = "Everyone";
        Place((RectTransform)global.transform, 0.56f, 0.68f, 10f);

        GameObject input = TMP_DefaultControls.CreateInputField(tmpResources);
        input.name = "NameInput";
        input.transform.SetParent(header, false);
        var field = input.GetComponent<TMP_InputField>();
        field.characterLimit = 12;
        if (field.placeholder is TMP_Text placeholder) placeholder.text = "Name…";
        Place((RectTransform)input.transform, 0.69f, 0.87f, 10f);

        GameObject show = TMP_DefaultControls.CreateButton(tmpResources);
        show.name = "ShowButton";
        show.transform.SetParent(header, false);
        show.GetComponentInChildren<TMP_Text>().text = "Show";
        Place((RectTransform)show.transform, 0.88f, 1f, 10f);

        // The scroll, vertical only, its scrollbar on the right
        GameObject scrollGo = DefaultControls.CreateScrollView(resources);
        scrollGo.name = "Scroll";
        var scrollRect = (RectTransform)scrollGo.transform;
        scrollRect.SetParent(rootRect, false);
        scrollRect.anchorMin = Vector2.zero; scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(10f, 10f); scrollRect.offsetMax = new Vector2(-10f, -80f);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        if (scroll.horizontalScrollbar != null) Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;   // always shown on the right
        if (scroll.verticalScrollbar != null)
        {
            var bar = (RectTransform)scroll.verticalScrollbar.transform;
            bar.sizeDelta = new Vector2(24f, bar.sizeDelta.y);
        }
        // The content: the grids one under the other, as tall as they are
        RectTransform content = scroll.content;
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // The view's references and catalog
        view.scroll = scroll;
        view.content = content;
        view.modeLabel = mode;
        view.nameInput = field;
        view.globalButton = global.GetComponent<Button>();
        view.showButton = show.GetComponent<Button>();
        MemeDex dex = Object.FindObjectOfType<MemeDex>();
        if (dex != null) { view.axes = new System.Collections.Generic.List<MemeData>(dex.axes); view.singles = new System.Collections.Generic.List<MemeData>(dex.optional); }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[MemeDex] MemeDex window made" + (dex != null ? $" ({dex.axes.Count} axes, {dex.optional.Count} single memes from the MemeDex)." : " (no MemeDex in this scene: fill its Axes and Singles).") , root);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        return rect;
    }

    static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // From x0 to x1 of the header's width, its whole height (inside a margin)
    static void Place(RectTransform rect, float x0, float x1, float margin = 12f)
    {
        rect.anchorMin = new Vector2(x0, 0f); rect.anchorMax = new Vector2(x1, 1f);
        rect.offsetMin = new Vector2(margin, margin); rect.offsetMax = new Vector2(-margin, -margin);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
