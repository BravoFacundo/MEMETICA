// Herramienta de editor: acomoda el escritorio del menú según el diseño de Figma.
// Menú: Memetica > UI > Acomodar escritorio (diseño Figma)
// - Ordena Button_Play (imagen arriba, texto abajo) y le da tamaño.
// - Crea Button_Config, Button_Exit y Button_Trash copiando Button_Play (si no existen).
// - Ubica los 4 íconos donde están en el diseño.
// - Crea Window_Logo, el lugar donde va a ir la ventana con el logo.
// Se puede deshacer todo junto con Ctrl+Z. Si lo volvés a correr, solo reubica (no duplica).

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DesktopLayoutTool
{
    const string IconsFolder = "Assets/UI/Icons/";
    static readonly Vector2 ButtonSize = new Vector2(200f, 190f);   // ícono 175x135 + 5 de espacio + texto 50

    // Posiciones medidas sobre el diseño, en píxeles de una pantalla 1920x1080
    static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    static readonly Vector2 BottomRight = new Vector2(1f, 0f);

    [MenuItem("Memetica/UI/Acomodar escritorio (diseño Figma)")]
    static void Layout()
    {
        GameObject desktop = GameObject.Find("Canvas_Desktop");
        GameObject play = GameObject.Find("Button_Play");
        if (desktop == null || play == null)
        {
            Debug.LogError("[Escritorio] No encuentro Canvas_Desktop o Button_Play (tienen que estar activos).");
            return;
        }

        Undo.SetCurrentGroupName("Acomodar escritorio");
        int undoGroup = Undo.GetCurrentGroup();

        PrepareButton(play);
        Place(play, TopLeft, new Vector2(298f, -508f));

        Place(GetOrCreate(play, "Button_Config", "Icon_Config", "Configuration"), TopLeft, new Vector2(298f, -723f));
        Place(GetOrCreate(play, "Button_Exit", "Icon_Exit", "Exit Game"), TopLeft, new Vector2(298f, -938f));
        Place(GetOrCreate(play, "Button_Trash", "Icon_Trash", "Recycle Bin"), BottomRight, new Vector2(-137f, 147f));

        GameObject logo = CreateLogoWindow(desktop.transform);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(desktop.scene);
        Selection.activeGameObject = logo;
        Debug.Log("[Escritorio] Listo. Revisá el Game view; Ctrl+Z deshace todo.");
    }

    // Imagen arriba y texto abajo dentro del Vertical Layout, y tamaño del botón
    static void PrepareButton(GameObject button)
    {
        Undo.RegisterFullObjectHierarchyUndo(button, "Preparar botón");
        Transform image = button.transform.Find("IconImage");
        Transform text = button.transform.Find("IconText");
        if (image != null) image.SetSiblingIndex(0);
        if (text != null) text.SetSiblingIndex(1);
        ((RectTransform)button.transform).sizeDelta = ButtonSize;
    }

    static GameObject GetOrCreate(GameObject template, string name, string iconFile, string label)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) return existing;

        GameObject copy = Object.Instantiate(template, template.transform.parent);
        copy.name = name;
        Undo.RegisterCreatedObjectUndo(copy, "Crear " + name);

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconsFolder + iconFile + ".png");
        Transform image = copy.transform.Find("IconImage");
        if (image != null && sprite != null) image.GetComponent<Image>().sprite = sprite;
        else Debug.LogWarning("[Escritorio] No encontré el sprite " + IconsFolder + iconFile + ".png para " + name);

        Transform text = copy.transform.Find("IconText");
        if (text != null) text.GetComponent<TMP_Text>().text = label;
        return copy;
    }

    static void Place(GameObject go, Vector2 anchor, Vector2 position)
    {
        RectTransform rt = (RectTransform)go.transform;
        Undo.RecordObject(rt, "Ubicar " + go.name);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
    }

    // Ventana del logo: por ahora un rectángulo blanco del tamaño y lugar del diseño.
    // Bloquea los clics (Raycast Target) para que tape lo que tenga abajo.
    static GameObject CreateLogoWindow(Transform desktop)
    {
        GameObject logo = GameObject.Find("Window_Logo");
        if (logo == null)
        {
            logo = new GameObject("Window_Logo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(logo, "Crear Window_Logo");
            logo.transform.SetParent(desktop, false);
        }

        RectTransform rt = (RectTransform)logo.transform;
        Undo.RecordObject(rt, "Ubicar Window_Logo");
        rt.anchorMin = TopLeft;
        rt.anchorMax = TopLeft;
        rt.pivot = TopLeft;                     // se ubica desde su esquina superior izquierda
        rt.anchoredPosition = new Vector2(97f, -109f);
        rt.sizeDelta = new Vector2(586f, 243f);
        logo.transform.SetAsLastSibling();      // arriba de los íconos
        return logo;
    }
}
