// Herramienta de editor: pone viento a los objetos seleccionados (y sus hijos).
// Menú: Memetica > Viento > Aplicar viento a la selección
//   - Cada material de cada Mesh Renderer se cambia por su copia "_Wind" (shader Memetica/Wind Sway).
//     La copia se crea una sola vez en Art/Materials/Wind y se reutiliza (sirve para materiales atlas).
//     Copia color, textura, smoothness y metallic del original.
//   - Destilda "Batching Static" (el shader lo necesita).
//   - Si la escena no tiene WindController, lo crea.
// Ctrl+Z deshace los cambios en la escena.

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WindTool
{
    const string ShaderName = "Memetica/Wind Sway";
    const string Folder = "Assets/Art/Materials/Wind";

    [MenuItem("Memetica/Viento/Aplicar viento a la selección")]
    static void ApplyToSelection()
    {
        Shader wind = Shader.Find(ShaderName);
        if (wind == null) { Debug.LogError("[Viento] No encuentro el shader " + ShaderName); return; }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Materials", "Wind");

        Undo.SetCurrentGroupName("Aplicar viento");
        int undoGroup = Undo.GetCurrentGroup();
        int renderers = 0, created = 0;

        var all = Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Distinct();
        foreach (MeshRenderer r in all)
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].shader == wind) continue;
                mats[i] = GetWindCopy(mats[i], wind, ref created);
                changed = true;
            }
            if (changed)
            {
                Undo.RecordObject(r, "Aplicar viento");
                r.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                renderers++;
            }

            // Sin batching estático
            GameObject go = r.gameObject;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            if ((flags & StaticEditorFlags.BatchingStatic) != 0)
            {
                Undo.RecordObject(go, "Sacar Batching Static");
                GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.BatchingStatic);
            }
        }

        if (Object.FindObjectOfType<WindController>(true) == null)
        {
            GameObject controller = new GameObject("WindController", typeof(WindController));
            Undo.RegisterCreatedObjectUndo(controller, "Crear WindController");
            Debug.Log("[Viento] Creé el WindController en la escena: el viento se ajusta desde ahí.", controller);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"[Viento] Listo: {renderers} renderers con viento, {created} materiales _Wind nuevos en {Folder}.");
    }

    [MenuItem("Memetica/Viento/Aplicar viento a la selección", true)]
    static bool HasSelection() => Selection.gameObjects.Length > 0;

    static Material GetWindCopy(Material src, Shader wind, ref int created)
    {
        string path = Folder + "/" + src.name + "_Wind.mat";
        Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (copy != null) return copy;

        copy = new Material(wind) { name = src.name + "_Wind" };
        if (src.HasProperty("_Color")) copy.SetColor("_Color", src.GetColor("_Color"));
        if (src.HasProperty("_MainTex"))
        {
            copy.SetTexture("_MainTex", src.GetTexture("_MainTex"));
            copy.SetTextureScale("_MainTex", src.GetTextureScale("_MainTex"));
            copy.SetTextureOffset("_MainTex", src.GetTextureOffset("_MainTex"));
        }
        if (src.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness", src.GetFloat("_Glossiness"));
        copy.SetFloat("_Metallic", src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f); // Specular → 0

        AssetDatabase.CreateAsset(copy, path);
        created++;
        Debug.Log("[Viento] Material nuevo: " + path, copy);
        return copy;
    }
}
