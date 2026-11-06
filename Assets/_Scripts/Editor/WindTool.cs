// Editor tool: gives wind to the selected objects (and their children).
// Menu: Memetica > Level > Wind > Apply Wind to Selection
//   - Every material of every Mesh Renderer is swapped for its "_Wind" copy (shader Memetica/Wind Sway).
//     The copy is made once, in the SAME folder as the WindSway shader, and reused (works for atlases).
//     So if you move the folder, the tool follows it.
//     It copies color, texture, smoothness and metallic from the original.
//   - Unticks "Batching Static" and "Contribute GI": moving objects can't use static batching or baked lighting.
//   - If the scene has no WindController, it makes one.
// Ctrl+Z undoes the changes in the scene.
//
// Menu: Memetica > Level > Wind > Particles Follow the Wind
//   Adds WindParticles to the leaf particles (material with "Lea"/"Leaf") and the gusts (objects named "WindParticles").

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
        if (wind == null) { Debug.LogError("[Wind] Can't find the shader " + ShaderName); return; }
        string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(wind)).Replace('\\', '/');

        Undo.SetCurrentGroupName("Apply Wind");
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
                Undo.RecordObject(r, "Apply Wind");
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
            Undo.RegisterCreatedObjectUndo(controller, "Create WindController");
            Debug.Log("[Wind] Made a WindController in the scene: the wind is tuned there.", controller);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"[Wind] Done: {renderers} renderers with wind, {created} new _Wind materials in {folder}.");
    }

    [MenuItem("Memetica/Level/Wind/Particles Follow the Wind", false, 311)]
    static void AddWindParticles()
    {
        Undo.SetCurrentGroupName("Particles Follow the Wind");
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
            Debug.Log("[Wind] WindParticles → " + ps.transform.parent?.name + "/" + ps.name, ps);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Wind] Done: " + added + " particle systems follow the wind.");
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
        Debug.Log("[Wind] New material: " + path, copy);
        return copy;
    }
}
