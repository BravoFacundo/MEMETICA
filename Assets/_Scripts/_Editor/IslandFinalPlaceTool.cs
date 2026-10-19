// TEMP: one-time migration to "islands placed in their final place". Menu: Memetica > Islands in final place (temporary)
// Run it once in each scene (Game and Game_Old) and save. Then delete this file.
//   Islands placed below Top Y (waiting in the void, e.g. Island_02 at -25) → moved up to Top Y, Start Raised off
//   Islands already at Top Y (they were up from the start, e.g. Island_01) → Start Raised on
// Everything inside each island moves with it (anchors, pedestals, dock points). Ctrl+Z undoes everything.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IslandFinalPlaceTool
{
    [MenuItem("Memetica/Islands in final place (temporary)")]
    public static void Migrate()
    {
        LevelManager level = Object.FindObjectOfType<LevelManager>(true);
        if (level == null) { Debug.LogError("[Islands] No LevelManager in the scene."); return; }

        Undo.SetCurrentGroupName("Islands in final place");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (Island island in Object.FindObjectsOfType<Island>(true))
        {
            Undo.RecordObject(island, "Islands in final place");
            Transform t = island.transform;
            if (t.position.y < level.topY - 0.5f)
            {
                Undo.RecordObject(t, "Islands in final place");
                float from = t.position.y;
                t.position = new Vector3(t.position.x, level.topY, t.position.z);
                island.startRaised = false;
                Debug.Log($"[Islands] {island.name}: moved up from {from} to {level.topY}, starts below.", island);
            }
            else
            {
                island.startRaised = true;
                Debug.Log($"[Islands] {island.name}: already up, Start Raised on.", island);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(island);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Islands] Done. Check loose objects that were sitting on a lowered island (not its children): they didn't move.");
    }
}
