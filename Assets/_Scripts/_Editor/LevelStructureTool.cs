// Editor tool: sorts the Levels hierarchy into the standard structure.
// Menu: Memetica > Ordenar estructura de niveles
//   1. Loose orbs in a Level → children of the island below them
//   2. Islands and elevators: everything that is not gameplay goes into "Island", sorted in Grass / Rocks / Vegetation / Ground / Decor.
//      What the keywords don't recognize goes to "Unsorted" and is listed in the console.
//      Island/Grass gets a GrassField with the grass material and its ground colliders, and is filled once.
//   3. Bridges: everything that is not gameplay goes into "Decor"
//   4. Lists inactive objects in the console (never deletes them)
// Safe to run again: what is already sorted stays. Ctrl+Z undoes everything.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelStructureTool
{
    private const string LevelsRoot = "Levels";
    public const string DecorName = "Decor";
    public const string VisualsName = "Island";
    private const string GrassName = "Grass";
    private const string UnsortedName = "Unsorted";
    private const string GrassShaderName = "Memetica/Grass";

    // Children the code looks up by name → never moved
    private static readonly string[] GameplayNames =
        { "Pintura", "Painting", "WindAudio", "PlatformPivotSuperior", "PlatformPivotInferior", DecorName, VisualsName };

    // Checked in this order against the object name, its prefab and its meshes (lowercase)
    private static readonly (string group, string[] keywords)[] VisualGroups =
    {
        (GrassName, new[] { "hill_small_02", "terrain_grass" }),
        ("Ground", new[] { "mountain", "isla", "hill", "cliff", "terrain", "sand", "path", "ground" }),
        ("Vegetation", new[] { "tree", "bush", "flower", "plant", "grass", "birch", "spruce", "cactus", "palm", "coconut", "banana", "broad_leaved", "fern", "mushroom", "leaf", "leaves", "arbol", "arbusto", "flor", "planta" }),
        ("Rocks", new[] { "rock", "boulder", "stone", "crystal", "stalagmite", "stalactite", "pebble", "roca", "piedra" }),
        (DecorName, new[] { "fence", "bench", "chair", "table", "log", "stump", "sign", "column", "banner", "easel", "painting", "canvas", "box", "barrel", "crate", "lamp", "bridge", "post", "banco", "tronco", "cerca", "valla", "silla", "mesa" }),
    };

    [MenuItem("Memetica/Ordenar estructura de niveles")]
    private static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Structure] Exit Play mode before using the tool."); return; }

        LevelManager level = Object.FindObjectOfType<LevelManager>(true);
        if (level == null) { Debug.LogError("[Structure] No LevelManager in the scene."); return; }

        Transform levels = SceneManager.GetActiveScene().GetRootGameObjects()
            .Select(g => g.transform).FirstOrDefault(t => t.name == LevelsRoot);
        if (levels == null) { Debug.LogError($"[Structure] No root object named '{LevelsRoot}'."); return; }

        Undo.SetCurrentGroupName("Sort level structure");
        int undoGroup = Undo.GetCurrentGroup();

        MoveLooseOrbs(levels);
        int sorted = SortIslands(levels, level);
        int decor = GroupBridgeDecor(levels, level);
        ListInactive(levels);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[Structure] Done: {sorted} island props sorted, {decor} bridge props to Decor. Run Memetica > Validar jerarquía. Ctrl+Z undoes everything.");
    }

    //-------------------------------------------------------------------------------------------------------------------

    private static void MoveLooseOrbs(Transform levels)
    {
        Physics.SyncTransforms();
        foreach (Meme orb in levels.GetComponentsInChildren<Meme>(true))
        {
            if (orb.GetComponentInParent<Island>(true) != null) continue;

            RaycastHit ground = Physics.RaycastAll(orb.transform.position, Vector3.down, 200f)
                .Where(h => !h.transform.IsChildOf(orb.transform))
                .OrderBy(h => h.distance)
                .FirstOrDefault();
            Island below = ground.transform != null ? ground.transform.GetComponentInParent<Island>(true) : null;

            if (below == null)
            {
                string on = ground.transform != null ? ground.transform.name : "nothing";
                Debug.LogWarning($"[Structure] Orb '{orb.name}' is over '{on}', not an island. Left where it is.", orb);
                continue;
            }

            Undo.SetTransformParent(orb.transform, below.transform, "Move " + orb.name);
            Debug.Log($"[Structure] Orb {orb.name} → {below.name}", orb);
        }
    }

    //-------------------------------------------------------------------------------------------------------------------

    private static int SortIslands(Transform levels, LevelManager level)
    {
        int sorted = 0;
        var containers = levels.GetComponentsInChildren<Island>(true).Select(c => c.transform)
            .Concat(levels.GetComponentsInChildren<Elevator>(true).Select(c => c.transform));

        foreach (Transform container in containers)
        {
            var props = container.Cast<Transform>()
                .Where(c => c.gameObject.activeSelf && c.name != VisualsName && c.name != DecorName && !IsGameplay(c, level))
                .ToList();

            Transform oldDecor = container.Find(DecorName);
            if (oldDecor != null) props.AddRange(oldDecor.Cast<Transform>());
            if (props.Count == 0 && container.Find(VisualsName) == null) continue;

            Transform visuals = GetOrCreateChild(container, VisualsName);
            foreach (Transform prop in props)
            {
                if (!CanMove(prop))
                {
                    Debug.LogWarning($"[Structure] {container.name}: '{prop.name}' is inside a prefab and can't be moved. Unpack the prefab (right click > Prefab > Unpack) and run again.", prop);
                    continue;
                }

                string group = Classify(prop);
                Undo.SetTransformParent(prop, GetOrCreateChild(visuals, group), "Sort " + prop.name);
                if (group == UnsortedName) Debug.LogWarning($"[Structure] {container.name}: '{prop.name}' not recognized → {VisualsName}/{UnsortedName}", prop);
                sorted++;
            }

            if (oldDecor != null && oldDecor.childCount == 0) Undo.DestroyObjectImmediate(oldDecor.gameObject);
            OrderGroups(visuals);
            SetupGrass(visuals);
            if (props.Count > 0) Debug.Log($"[Structure] {container.name}: {props.Count} props sorted", visuals);
        }
        return sorted;
    }

    // Children of a prefab instance can't change parent; the instance root itself can
    private static bool CanMove(Transform prop)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(prop.gameObject)) return true;
        return PrefabUtility.IsOutermostPrefabInstanceRoot(prop.gameObject);
    }

    private static string Classify(Transform prop)
    {
        var words = new List<string> { prop.name };
        GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prop.gameObject);
        if (source != null) words.Add(source.name);
        words.AddRange(prop.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).Select(f => f.sharedMesh.name));
        string text = string.Join(" ", words).ToLowerInvariant();

        foreach (var (group, keywords) in VisualGroups)
            if (keywords.Any(k => text.Contains(k))) return group;
        return UnsortedName;
    }

    private static void OrderGroups(Transform visuals)
    {
        var order = VisualGroups.Select(g => g.group).Append(UnsortedName).ToList();
        int index = 0;
        foreach (string group in order)
        {
            Transform child = visuals.Find(group);
            if (child == null) continue;
            Undo.RecordObject(child, "Order groups");
            child.SetSiblingIndex(index++);
        }
    }

    // Island/Grass: GrassField with the grass material and the colliders of its meshes, filled once
    private static void SetupGrass(Transform visuals)
    {
        Transform grass = visuals.Find(GrassName);
        if (grass == null) return;

        GrassField field = grass.GetComponent<GrassField>();
        bool isNew = field == null;
        if (isNew) field = Undo.AddComponent<GrassField>(grass.gameObject);

        var colliders = grass.GetComponentsInChildren<MeshCollider>(true).Where(c => !c.isTrigger).ToList();
        if (colliders.Count == 0) Debug.LogWarning($"[Structure] {visuals.parent.name}/{VisualsName}/{GrassName} has no MeshColliders: the grass has nothing to grow on.", grass);

        SerializedObject serialized = new(field);
        SerializedProperty material = serialized.FindProperty("grassMaterial");
        if (material.objectReferenceValue == null) material.objectReferenceValue = FindGrassMaterial();

        SerializedProperty grounds = serialized.FindProperty("groundColliders");
        grounds.arraySize = colliders.Count;
        for (int i = 0; i < colliders.Count; i++) grounds.GetArrayElementAtIndex(i).objectReferenceValue = colliders[i];
        serialized.ApplyModifiedProperties();

        if (isNew && colliders.Count > 0) field.TestFill();
    }

    private static Material FindGrassMaterial()
    {
        return AssetDatabase.FindAssets("t:Material")
            .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))
            .FirstOrDefault(m => m != null && m.shader != null && m.shader.name == GrassShaderName);
    }

    //-------------------------------------------------------------------------------------------------------------------

    private static int GroupBridgeDecor(Transform levels, LevelManager level)
    {
        int moved = 0;
        foreach (Bridge bridge in levels.GetComponentsInChildren<Bridge>(true))
        {
            Transform container = bridge.transform;
            var props = container.Cast<Transform>().Where(c => c.gameObject.activeSelf && !IsGameplay(c, level)).ToList();
            if (props.Count == 0) continue;

            Transform decor = GetOrCreateChild(container, DecorName);
            foreach (Transform prop in props) Undo.SetTransformParent(prop, decor, "Move to Decor");
            Undo.RecordObject(decor, "Order Decor");
            decor.SetAsLastSibling();
            moved += props.Count;
        }
        return moved;
    }

    // Gameplay = looked up by code, tagged, has sound, or has one of our scripts (except FloatMotion)
    public static bool IsGameplay(Transform t, LevelManager level)
    {
        string n = t.name;
        if (GameplayNames.Contains(n) || n == level.respawnPointName) return true;
        if (n.StartsWith(level.islandPrefix) || n.StartsWith(level.bridgePrefix) || n.StartsWith(level.elevatorPrefix) || n.StartsWith("Pedestal")) return true;
        if (!t.CompareTag("Untagged")) return true;
        if (t.GetComponentInChildren<AudioSource>(true) != null) return true;
        return t.GetComponentsInChildren<MonoBehaviour>(true)
            .Any(m => m != null && !(m is FloatMotion) && !(m is GrassField) && m.GetType().Assembly.GetName().Name == "Assembly-CSharp");
    }

    //-------------------------------------------------------------------------------------------------------------------

    private static void ListInactive(Transform levels)
    {
        foreach (Transform t in levels.GetComponentsInChildren<Transform>(true).Where(t => !t.gameObject.activeSelf))
            Debug.LogWarning($"[Structure] Inactive: {PathOf(t)} (delete it?)", t);
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;

        GameObject newChild = new(name);
        Undo.RegisterCreatedObjectUndo(newChild, "Create " + name);
        Undo.SetTransformParent(newChild.transform, parent, "Create " + name);
        newChild.transform.localPosition = Vector3.zero;
        newChild.transform.localRotation = Quaternion.identity;
        newChild.transform.localScale = Vector3.one;
        return newChild.transform;
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
