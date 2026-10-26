// Memetica > Memes > Add Window Titles: the meme's name (TextMeshPro, the Impact font asset) on the title bar of the
// meme windows: the 3D ones (MemeWindow prefab, Meme_Window_Dex) and the HUD ones in the open scene (HudWindow). It
// goes in the band between the top of the frame and the hole, white and centered; move it, resize it or give it an
// outline (its material) as you like. Running it again leaves the titles that exist. Save the scene after.
// The font: the first TMP font asset named Impact (make it with Window > TextMeshPro > Font Asset Creator).

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MemeWindowTitleTool
{
    static readonly string[] Prefabs = { "Assets/_Prefabs/MemeWindow.prefab", "Assets/_Prefabs/Meme_Window_Dex.prefab" };

    [MenuItem("Memetica/Memes/Add Window Titles", false, 202)]
    static void AddTitles()
    {
        TMP_FontAsset font = FindFont();
        int made = 0;

        // The 3D windows (their scene copies get it from the prefab)
        foreach (string path in Prefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.TryGetComponent(out MemeWindow window) && window.titleText == null && WorldTitle(root, window, font))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    made++;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // The HUD ones: the same band, from the meme window prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[0]);
        if (prefab != null && Band(prefab, out Vector2 min, out Vector2 max))
            foreach (HudWindow hud in Object.FindObjectsOfType<HudWindow>(true))
                if (hud.titleText == null) { HudTitle(hud, font, min, max); made++; }

        Debug.Log($"[Titles] {made} title(s) added" + (font == null ? " (no Impact font asset found: TextMeshPro's default)." : $" ({font.name})."));
    }

    // In the 3D window: on the frame, in the band above the hole, a little in front
    static bool WorldTitle(GameObject root, MemeWindow window, TMP_FontAsset font)
    {
        if (!root.TryGetComponent(out WindowControls controls) || controls.picture == null || window.window == null) return false;
        Transform frame = controls.picture, hole = window.window.transform;
        float frameTop = frame.localPosition.y + Mathf.Abs(frame.localScale.y) / 2f;
        float holeTop = hole.localPosition.y + Mathf.Abs(hole.localScale.y) / 2f;
        float band = frameTop - holeTop;
        if (band < 0.01f) { band = Mathf.Abs(frame.localScale.y) * 0.08f; holeTop = frameTop - band; }   // (no band: along the top)

        var go = new GameObject("Title");
        go.transform.SetParent(frame.parent, false);
        go.transform.localRotation = frame.localRotation;   // facing where the frame faces
        go.transform.localPosition = new Vector3(hole.localPosition.x, holeTop + band / 2f, frame.localPosition.z) + frame.localRotation * Vector3.back * 0.004f;
        var text = go.AddComponent<TextMeshPro>();
        Style(text, font);
        text.rectTransform.sizeDelta = new Vector2(Mathf.Abs(hole.localScale.x) * 0.9f, band * 0.8f);
        text.fontSizeMin = 0.05f;
        text.fontSizeMax = 20f;
        window.titleText = text;
        return true;
    }

    // In the HUD window: the same band, as anchors of the whole frame
    static void HudTitle(HudWindow hud, TMP_FontAsset font, Vector2 min, Vector2 max)
    {
        var go = new GameObject("Title", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Window Title");
        var rect = (RectTransform)go.transform;
        rect.SetParent(hud.transform, false);
        rect.SetAsLastSibling();   // over the frame
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = go.AddComponent<TextMeshProUGUI>();
        Style(text, font);
        text.raycastTarget = false;
        text.fontSizeMin = 8f;
        text.fontSizeMax = 72f;
        Undo.RecordObject(hud, "Window Title");
        hud.titleText = text;
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    static void Style(TMP_Text text, TMP_FontAsset font)
    {
        if (font != null) text.font = font;
        text.text = "";
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    // The band above the hole, 0-1 of the frame (x mirrored as the HUD window: the 3D one faces the other way)
    static bool Band(GameObject prefab, out Vector2 min, out Vector2 max)
    {
        min = max = default;
        if (!prefab.TryGetComponent(out WindowControls controls) || controls.picture == null ||
            !prefab.TryGetComponent(out MemeWindow window) || window.window == null) return false;
        Transform frame = controls.picture, hole = window.window.transform;
        Vector2 frameSize = new Vector2(Mathf.Abs(frame.localScale.x), Mathf.Abs(frame.localScale.y));
        Vector2 holeSize = new Vector2(Mathf.Abs(hole.localScale.x), Mathf.Abs(hole.localScale.y));
        Vector2 holeCenter = new Vector2(-(hole.localPosition.x - frame.localPosition.x), hole.localPosition.y - frame.localPosition.y);
        float top = 0.5f + (holeCenter.y + holeSize.y / 2f) / frameSize.y;
        if (top > 0.98f) top = 0.92f;   // (no band: along the top)
        float side = holeSize.x * 0.05f;
        min = new Vector2(0.5f + (holeCenter.x - holeSize.x / 2f + side) / frameSize.x, top);
        max = new Vector2(0.5f + (holeCenter.x + holeSize.x / 2f - side) / frameSize.x, 1f);
        return true;
    }

    static TMP_FontAsset FindFont()
    {
        foreach (string guid in AssetDatabase.FindAssets("Impact t:TMP_FontAsset"))
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (font != null) return font;
        }
        Debug.LogWarning("[Titles] No TMP font asset named Impact: make one (Window > TextMeshPro > Font Asset Creator) and set it on the titles.");
        return null;
    }
}
