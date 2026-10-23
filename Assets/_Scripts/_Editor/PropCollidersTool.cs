// Memetica > Props: solid for memes. Memes never sink into decoration again: every mesh inside an island (or a bridge)
// that has no collider of its own (grass patches like rpgpp_lt_hill_small, rocks…) gets a MeshCollider on the Props
// layer (made if the project doesn't have it). Memes rest on them; the player still walks through them and ignores
// them in its checks (PhysicsLayers).
// Left out: trees (layer Trees), pedestals, paintings, memes, and anything already with a collider.
// Run it in each scene (Game, Game_Old) and save. Again after adding decoration. Ctrl+Z undoes it. The Console lists them.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PropCollidersTool
{
    const string UndoName = "Props solid for memes";

    [MenuItem("Memetica/Props: solid for memes")]
    public static void Make()
    {
        int layer = EnsureLayer(PhysicsLayers.PropsName);
        if (layer < 0) { Debug.LogError($"[Props] No free layer for '{PhysicsLayers.PropsName}'."); return; }

        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();
        int trees = LayerMask.NameToLayer("Trees");
        var done = new List<string>();

        var roots = Object.FindObjectsOfType<Transform>(true).Where(t => t.CompareTag("Isla") || t.CompareTag("Puente"));
        var renderers = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Distinct();
        foreach (MeshRenderer renderer in renderers)
        {
            GameObject go = renderer.gameObject;
            if (go.GetComponent<Collider>() != null || go.layer == trees) continue;
            if (go.GetComponentInParent<Pedestal>(true) != null || go.GetComponentInParent<Painting>(true) != null
                || go.GetComponentInParent<Meme>(true) != null) continue;
            if (!go.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;

            var collider = Undo.AddComponent<MeshCollider>(go);
            collider.sharedMesh = filter.sharedMesh;
            Undo.RecordObject(go, UndoName);
            go.layer = layer;
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            done.Add(go.name);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        string list = string.Join(", ", done.GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));
        Debug.Log($"[Props] {done.Count} decoration meshes are now solid for memes (layer {PhysicsLayers.PropsName}). Save the scene.\n{list}");
    }

    // The layer with that name, added in the first free user slot if missing
    static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;
            slot.stringValue = name;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"[Props] Layer '{name}' added (layer {i}).");
            return i;
        }
        return -1;
    }
}
