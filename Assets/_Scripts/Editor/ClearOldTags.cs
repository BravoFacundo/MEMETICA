// One-time cleanup: the old tags (Isla, Puente, Pintura, Meme, Pedestal, Platform) are no longer used by any script
// (islands, bridges, memes and paintings are found by their components). Memetica > Check > Clear Old Tags sets every
// object that has one of them to Untagged, in every prefab and every scene in _Scenes, saves them, and then removes the
// tags from Tags & Layers. Once it ran, this file can be deleted.

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ClearOldTags
{
    static readonly string[] OldTags = { "Isla", "Puente", "Pintura", "Meme", "Pedestal", "Platform" };

    [MenuItem("Memetica/Check/Clear Old Tags", false, 410)]
    static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Clear Old Tags",
                "Sets every object tagged " + string.Join(", ", OldTags) + " to Untagged (all prefabs and all scenes in " +
                "_Scenes, saved), then removes those tags. Commit first if you want a way back.", "Clear", "Cancel")) return;

        var openScenes = EditorSceneManager.GetSceneManagerSetup();
        int prefabObjects = 0, prefabFiles = 0, sceneObjects = 0, sceneFiles = 0;

        try
        {
            // Prefabs first: the instances in the scenes take Untagged from them
            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            for (int i = 0; i < prefabs.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Clear Old Tags", prefabs[i], (float)i / prefabs.Length * 0.5f);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabs[i]);
                int n = Clear(root);
                if (n > 0) { PrefabUtility.SaveAsPrefabAsset(root, prefabs[i]); prefabObjects += n; prefabFiles++; }
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Then the scenes (what is only in the scene, or an instance that overrode the tag)
            string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Scenes" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Clear Old Tags", scenes[i], 0.5f + (float)i / scenes.Length * 0.5f);
                var scene = EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
                int n = scene.GetRootGameObjects().Sum(Clear);
                if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); sceneObjects += n; sceneFiles++; }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (openScenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(openScenes);
        }

        // Nothing uses them now: out of Tags & Layers
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        int removed = 0;
        for (int i = tags.arraySize - 1; i >= 0; i--)
            if (OldTags.Contains(tags.GetArrayElementAtIndex(i).stringValue)) { tags.DeleteArrayElementAtIndex(i); removed++; }
        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        Debug.Log($"[ClearOldTags] {prefabObjects} objects in {prefabFiles} prefabs and {sceneObjects} in {sceneFiles} scenes set to Untagged; {removed} tags removed.");
    }

    // Untagged every object under it with an old tag. Returns how many
    static int Clear(GameObject root)
    {
        int n = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!OldTags.Contains(t.tag)) continue;
            t.gameObject.tag = "Untagged";
            n++;
        }
        return n;
    }
}
