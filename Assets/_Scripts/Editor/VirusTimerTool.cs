// Memetica > HUD > Make Virus Timer: select your HUD canvas (or an object inside it) and run it. It builds the virus'
// countdown there, top right: a small meme window (the frame and background PNGs of Assets/_Prefabs/MemeWindow) with
// the time in its hole (VirusClock, hidden until the countdown starts). Running it again rebuilds it (Ctrl+Z undoes
// it). Replace its look with your design later: keep VirusClock and its Label.

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class VirusTimerTool
{
    const string WindowPrefabPath = "Assets/_Prefabs/MemeWindow.prefab";
    const float Width = 220f;   // canvas units

    [MenuItem("Memetica/HUD/Make Virus Timer", false, 103)]
    static void Make()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || !(selected.transform is RectTransform) || selected.GetComponentInParent<Canvas>() == null)
        { Debug.LogError("[HUD] Select your HUD canvas (or an object inside it) first."); return; }

        // The meme window's frame, background and hole (as the UI meme window)
        Texture frameTexture = null, background = null;
        Vector2 frameSize = new Vector2(1f, 0.75f), holeCenter = Vector2.zero, holeSize = new Vector2(0.9f, 0.6f);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath);
        if (prefab != null && prefab.TryGetComponent(out WindowControls controls) && controls.picture != null &&
            prefab.TryGetComponent(out MemeWindow window3D) && window3D.window != null)
        {
            Transform frame = controls.picture, content = window3D.window.transform;
            frameTexture = frame.GetComponent<Renderer>().sharedMaterial.mainTexture;
            Material contentMaterial = content.GetComponent<Renderer>().sharedMaterial;
            if (contentMaterial != null && contentMaterial.HasProperty("_PaperTex")) background = contentMaterial.GetTexture("_PaperTex");
            frameSize = frame.localScale;
            holeCenter = new Vector2(-(content.localPosition.x - frame.localPosition.x), content.localPosition.y - frame.localPosition.y);
            holeSize = content.localScale;
        }
        else Debug.LogWarning($"[HUD] No meme window at {WindowPrefabPath}: a plain panel instead.");

        Transform old = selected.transform.Find("VirusTimer");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Root: the whole window, top right
        RectTransform root = MakeRect("VirusTimer", selected.transform);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Virus Timer");
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-40f, -40f);
        root.sizeDelta = new Vector2(Width, Width * frameSize.y / frameSize.x);
        root.gameObject.AddComponent<CanvasGroup>();

        // Background in the hole, the time on it, the frame on top
        RawImage back = FillImage("Background", root, background, background != null ? Color.white : new Color(0.1f, 0.1f, 0.12f));
        back.rectTransform.anchorMin = new Vector2(0.5f + (holeCenter.x - holeSize.x / 2f) / frameSize.x, 0.5f + (holeCenter.y - holeSize.y / 2f) / frameSize.y);
        back.rectTransform.anchorMax = new Vector2(0.5f + (holeCenter.x + holeSize.x / 2f) / frameSize.x, 0.5f + (holeCenter.y + holeSize.y / 2f) / frameSize.y);

        RectTransform labelRect = MakeRect("Label", back.rectTransform);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = "05:00";
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 200f;
        label.color = background != null ? new Color(0.12f, 0.12f, 0.12f) : Color.white;
        label.raycastTarget = false;

        if (frameTexture != null) FillImage("Frame", root, frameTexture, Color.white);

        var clock = root.gameObject.AddComponent<VirusClock>();
        clock.label = label;

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[HUD] Virus timer made (top right, hidden until the countdown starts). Save the scene.", root);
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
