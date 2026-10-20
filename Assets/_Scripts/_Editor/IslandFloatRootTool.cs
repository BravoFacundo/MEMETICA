// TEMP: moves every island to the new structure. Menu: Memetica > Islands float from the root (temporary)
// Run it once in each scene (Game and Game_Old) and save. Then delete this file.
//   Before                            After
//   Island_NN (Island)                Island_NN (Island + FloatMotion, same values as before)
//   └── Island (FloatMotion)          ├── pedestals, paintings, memes, triggers, anchors, Pivots…  (interactive)
//       ├── Pedestal …                └── Scenery
//       ├── Pivots …                      ├── Ground, Rocks, Vegetation, Grass, Decor…            (only looked at)
//       └── Ground, Rocks, Grass …
// Interactive = what LevelStructureTool calls gameplay (our scripts, tags, known names), or anything that has some inside.
// Everything keeps its place in the world. Ctrl+Z undoes everything.

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IslandFloatRootTool
{
    const string SceneryName = "Scenery";

    [MenuItem("Memetica/Islands float from the root (temporary)")]
    public static void Migrate()
    {
        LevelManager level = Object.FindObjectOfType<LevelManager>(true);
        if (level == null) { Debug.LogError("[Islands] No LevelManager in the scene."); return; }

        Undo.SetCurrentGroupName("Islands float from the root");
        int undoGroup = Undo.GetCurrentGroup();
        int moved = 0;

        foreach (Island island in Object.FindObjectsOfType<Island>(true))
        {
            Transform root = island.transform;
            if (root.GetComponent<FloatMotion>() != null) continue;   // already done

            FloatMotion old = root.GetComponentsInChildren<FloatMotion>(true).FirstOrDefault(f => f.transform.parent == root);
            if (old == null) { Debug.LogWarning($"[Islands] {island.name}: no floating child, skipped.", island); continue; }

            try
            {
                // The floating, now on the island itself, with the same values
                ComponentUtility.CopyComponent(old);
                ComponentUtility.PasteComponentAsNew(root.gameObject);

                Transform scenery = root.Find(SceneryName);
                if (scenery == null)
                {
                    scenery = new GameObject(SceneryName).transform;
                    Undo.RegisterCreatedObjectUndo(scenery.gameObject, "Islands float from the root");
                    Undo.SetTransformParent(scenery, root, "Islands float from the root");
                    scenery.localPosition = Vector3.zero;
                    scenery.localRotation = Quaternion.identity;
                    scenery.localScale = Vector3.one;
                }

                // Interactive things to the island, the rest to Scenery (world places kept)
                foreach (Transform child in old.transform.Cast<Transform>().ToList())
                    Undo.SetTransformParent(child, IsInteractive(child, level) ? root : scenery, "Islands float from the root");

                Undo.DestroyObjectImmediate(old.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(root);
                moved++;
                Debug.Log($"[Islands] {island.name}: floats from the root now.", island);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Islands] {island.name}: couldn't be moved ({e.Message}). If it is a prefab, unpack it first.", island);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[Islands] Done: {moved} islands. Save the scene.");
    }

    // It, or anything inside it, is something the code or the player uses
    static bool IsInteractive(Transform t, LevelManager level)
    {
        if (t.name == "Pivots") return true;
        return t.GetComponentsInChildren<Transform>(true).Any(c => LevelStructureTool.IsGameplay(c, level) && c.name != LevelStructureTool.DecorName && c.name != LevelStructureTool.VisualsName);
    }
}
