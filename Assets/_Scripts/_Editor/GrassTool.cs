// Memetica > Level > Grass: paints the grass again (GrassField: over its children's meshes, plus its Extra Ground
// Meshes), e.g. after moving or adding ground pieces under it.
//   Regenerate Selected   the GrassFields in the selection (selected, or under a selected object)
//   Regenerate All        every GrassField in the open scene
// Ctrl+Z undoes it. Save the scene after.

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GrassTool
{
    [MenuItem("Memetica/Level/Grass/Regenerate Selected", false, 320)]
    static void RegenerateSelected() => Regenerate(Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<GrassField>(true)).Distinct().ToArray());

    [MenuItem("Memetica/Level/Grass/Regenerate Selected", true)]
    static bool CanRegenerateSelected() => Selection.gameObjects.Any(g => g.GetComponentInChildren<GrassField>(true) != null);

    [MenuItem("Memetica/Level/Grass/Regenerate All", false, 321)]
    static void RegenerateAll() => Regenerate(Object.FindObjectsOfType<GrassField>(true));

    static void Regenerate(GrassField[] fields)
    {
        if (fields.Length == 0) { Debug.LogWarning("[Grass] No GrassField found."); return; }
        foreach (GrassField field in fields) field.TestFill();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[Grass] {fields.Length} grass field(s) painted again. Save the scene.");
    }
}
