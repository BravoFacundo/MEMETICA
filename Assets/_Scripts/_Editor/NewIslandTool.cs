// Menu: Memetica > New Island. Creates an empty island with everything the code needs, ready to fill:
//   Island_NN            Island (rises, falls) + FloatMotion (floats), tag "Isla". Place it where it ends up (up).
//   │                    Interactive things go right here: pedestals, paintings, memes, triggers, bridge anchors
//   ├── Pivots
//   │   └── RespawnPoint where the player and memes come back (high: they fall onto the island)
//   └── Scenery          what is only looked at: the island mesh, rocks, vegetation, grass
//       ├── Ground       the island mesh with its collider
//       ├── Rocks
//       ├── Vegetation
//       └── Grass
// Everything inside floats and rises with the island. It appears at the middle of the Scene view,
// named with the next free number, and gets selected.

using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class NewIslandTool
{
    const float RespawnHeight = 20f;

    [MenuItem("Memetica/New Island")]
    public static void Create()
    {
        LevelManager level = Object.FindObjectOfType<LevelManager>(true);
        string prefix = level != null ? level.islandPrefix : "Island_";

        // Next free number (Island_01, Island_02…)
        int next = Object.FindObjectsOfType<Island>(true)
            .Select(i => Regex.Match(i.name, prefix + @"(\d+)$"))
            .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value))
            .DefaultIfEmpty(0).Max() + 1;

        GameObject root = new GameObject($"{prefix}{next:00}");
        Undo.RegisterCreatedObjectUndo(root, "New Island");
        if (Selection.activeTransform != null && Selection.activeTransform.GetComponent<Island>() == null)
            root.transform.SetParent(Selection.activeTransform.parent, false);   // next to what was selected (e.g. inside a Level)
        Vector3 at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        root.transform.position = new Vector3(at.x, 0f, at.z);
        root.AddComponent<Island>();
        TrySetTag(root, "Isla");

        root.AddComponent<FloatMotion>();

        Transform pivots = Child(root.transform, "Pivots");
        Transform respawn = Child(pivots, level != null ? level.respawnPointName : "RespawnPoint");
        respawn.localPosition = Vector3.up * RespawnHeight;

        Transform scenery = Child(root.transform, "Scenery");
        Child(scenery, "Ground");
        Child(scenery, "Rocks");
        Child(scenery, "Vegetation");
        Child(scenery, "Grass");

        Selection.activeGameObject = root;
        Debug.Log($"[New Island] {root.name} created. Put the island mesh in Scenery/Ground, pedestals and paintings right in the island.", root);
    }

    static Transform Child(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    static void TrySetTag(GameObject go, string tag)
    {
        try { go.tag = tag; }
        catch { Debug.LogWarning($"[New Island] Tag '{tag}' doesn't exist: add it in Tags & Layers and tag {go.name}.", go); }
    }
}
