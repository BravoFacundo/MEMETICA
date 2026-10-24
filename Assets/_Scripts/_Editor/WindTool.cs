// Herramienta de editor: pone viento a los objetos seleccionados (y sus hijos).
// Menú: Memetica > Level > Wind > Apply Wind to Selection
//   - Cada material de cada Mesh Renderer se cambia por su copia "_Wind" (shader Memetica/Wind Sway).
//     La copia se crea una sola vez en la MISMA carpeta que el shader WindSway y se reutiliza (sirve para atlas).
//     Así, si movés la carpeta, la herramienta la sigue sola.
//     Copia color, textura, smoothness y metallic del original.
//   - Unticks "Batching Static" and "Contribute GI": moving objects can't use static batching or baked lighting.
//   - Si la escena no tiene WindController, lo crea.
// Ctrl+Z deshace los cambios en la escena.
//
// Menú: Memetica > Level > Wind > Particles Follow the Wind
//   Agrega WindParticles a las partículas de hojas (material con "Lea"/"Leaf") y de ráfagas (objetos "WindParticles").

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WindTool
{
    const string ShaderName = "Memetica/Wind Sway";

    [MenuItem("Memetica/Level/Wind/Apply Wind to Selection", false, 310)]
    static void ApplyToSelection()
    {
        Shader wind = Shader.Find(ShaderName);
        if (wind == null) { Debug.LogError("[Viento] No encuentro el shader " + ShaderName); return; }
        string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(wind)).Replace('\\', '/');

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
                mats[i] = GetWindCopy(mats[i], wind, folder, ref created);
                changed = true;
            }
            if (changed)
            {
                Undo.RecordObject(r, "Aplicar viento");
                r.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                renderers++;
            }

            // Moving objects: no static batching and no baked lighting (a baked shadow would stay at the original pose)
            GameObject go = r.gameObject;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            StaticEditorFlags movingFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI;
            if ((flags & movingFlags) != 0)
            {
                Undo.RecordObject(go, "Remove Batching Static and Contribute GI");
                GameObjectUtility.SetStaticEditorFlags(go, flags & ~movingFlags);
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
        Debug.Log($"[Viento] Listo: {renderers} renderers con viento, {created} materiales _Wind nuevos en {folder}.");
    }

    [MenuItem("Memetica/Level/Wind/Particles Follow the Wind", false, 311)]
    static void AddWindParticles()
    {
        Undo.SetCurrentGroupName("Partículas siguen el viento");
        int undoGroup = Undo.GetCurrentGroup();
        int added = 0;

        foreach (ParticleSystem ps in Object.FindObjectsOfType<ParticleSystem>(true))
        {
            if (ps.GetComponent<WindParticles>() != null) continue;
            Material mat = ps.GetComponent<ParticleSystemRenderer>()?.sharedMaterial;
            bool leaves = mat != null && (mat.name.Contains("Lea") || mat.name.Contains("Leaf"));
            bool gusts = ps.name.StartsWith("WindParticles");
            if (!leaves && !gusts) continue;

            Undo.AddComponent<WindParticles>(ps.gameObject);
            added++;
            Debug.Log("[Viento] WindParticles → " + ps.transform.parent?.name + "/" + ps.name, ps);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Viento] Listo: " + added + " sistemas de partículas siguen el viento.");
    }

    [MenuItem("Memetica/Level/Wind/Apply Wind to Selection", true)]
    static bool HasSelection() => Selection.gameObjects.Length > 0;

    static Material GetWindCopy(Material src, Shader wind, string folder, ref int created)
    {
        string path = folder + "/" + src.name + "_Wind.mat";
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
