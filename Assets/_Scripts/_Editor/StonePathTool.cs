// Memetica > Level > Stone Path: designing a stone path (StonePath) between two islands.
//   New Stone Path    select your platform prefabs in the Project window (they become its Models) and run it: a new
//                     bridge (Bridge_New, Appearance Stone By Stone) with two end stones, in front of the Scene view.
//                     Nothing selected: it copies the Models of another stone path in the scene.
//                     Place each end stone where the path meets its island, rename the bridge (Bridge_ID)
//   Generate Stones   fills the middle along the yellow curve (StonePath > Curve), every Spacing meters, sizes going
//                     from one end stone to the other (x Size Scale). Replaces the stones in between (asks first)
//   Create Anchors    an anchor on the island under each end stone (inside its floating part), set on the Bridge
//   Sort Stones       orders the stones in the hierarchy from anchor A to anchor B (that is the order they rise in),
//                     each one followed by its shadows; the ones called Stone_NN get their new number
//   Add Shadow        for the selected stones: one more ghost of it, the next point of its route (61, 62…).
//                     Move it anywhere; it is hidden while playing
// A stone is an empty with its PathStone and a child, Model: the prefab its Model picks (change it there, several
// stones at once too: its shadows change with it). The stone's scale is its size; the Model child's is the model's
// Size (StonePath > Models), the same for every stone using it: don't scale the Model child by hand.
// Generate Stones picks the models at random (the ones In Generate), and turns and lifts each stone a little at
// random (StonePath > Random Turn / Random Height).
// Select the bridge or anything inside it. Ctrl+Z undoes each one. Save the scene after.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StonePathTool
{
    const string Menu = "Memetica/Level/Stone Path/";
    const string ShadowMaterialPath = "Assets/_Rendering/StoneShadow.mat";
    const string ModelName = "Model";

    static StonePath Selected => Selection.activeTransform != null ? Selection.activeTransform.GetComponentInParent<StonePath>() : null;

    //---- New Stone Path

    [MenuItem(Menu + "New Stone Path", false, 330)]
    static void NewPath()
    {
        List<StonePath.Model> models = SelectedPrefabs().Select(p => new StonePath.Model { prefab = p }).ToList();
        if (models.Count == 0)
        {
            StonePath other = Object.FindObjectsOfType<StonePath>(true).FirstOrDefault(p => p.models.Any(m => m.prefab != null));
            if (other != null) models = other.models.Where(m => m.prefab != null)
                .Select(m => new StonePath.Model { prefab = m.prefab, size = m.size, inGenerate = m.inGenerate }).ToList();
        }
        if (models.Count == 0) { Debug.LogError("[StonePath] Select your platform prefabs in the Project window first (they become its Models)."); return; }

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 center = view != null ? view.pivot : Vector3.zero;
        Vector3 right = view != null ? Vector3.ProjectOnPlane(view.camera.transform.right, Vector3.up).normalized : Vector3.right;
        if (right.sqrMagnitude < 0.01f) right = Vector3.right;

        var root = new GameObject("Bridge_New");
        Undo.RegisterCreatedObjectUndo(root, "New Stone Path");
        root.transform.SetPositionAndRotation(center, Quaternion.LookRotation(right, Vector3.up));
        Bridge bridge = root.AddComponent<Bridge>();
        bridge.appearance = Bridge.Appearance.StoneByStone;
        StonePath path = root.AddComponent<StonePath>();
        path.models = models;

        MakeStone(path, "Stone_01", center - right * 4f, root.transform.rotation, 0);
        MakeStone(path, "Stone_02", center + right * 4f, root.transform.rotation, 0);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[StonePath] Bridge_New made ({models.Count} model(s)): place its two end stones where the path meets each island, set its Curve and run Generate Stones. Rename it Bridge_ID.", root);
    }

    // Prefabs selected in the Project window (or the prefabs of instances selected in the scene)
    static List<GameObject> SelectedPrefabs()
    {
        var list = new List<GameObject>();
        foreach (GameObject g in Selection.gameObjects)
        {
            GameObject prefab = PrefabUtility.IsPartOfPrefabAsset(g) ? g
                : PrefabUtility.IsPartOfPrefabInstance(g) ? PrefabUtility.GetCorrespondingObjectFromSource(PrefabUtility.GetOutermostPrefabInstanceRoot(g)) : null;
            if (prefab != null && !list.Contains(prefab)) list.Add(prefab);
        }
        return list;
    }

    // An empty with its PathStone, looking like Models[model]
    static PathStone MakeStone(StonePath path, string name, Vector3 position, Quaternion rotation, int model)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Stone");
        go.transform.SetParent(path.transform, false);
        go.transform.SetPositionAndRotation(position, rotation);
        PathStone stone = go.AddComponent<PathStone>();
        SetModel(stone, model);
        return stone;
    }

    //---- Model (PathStone > Model)

    // Its look (child Model) becomes Models[index], and its shadows too
    public static void SetModel(PathStone stone, int index)
    {
        if (stone == null) return;
        StonePath path = stone.GetComponentInParent<StonePath>();
        StonePath.Model model = ModelOf(path, index);
        if (model == null) { Debug.LogWarning($"[StonePath] '{stone.name}': no Model {index} in its StonePath's Models.", stone); return; }
        GameObject prefab = model.prefab;

        Undo.RecordObject(stone, "Stone Model");
        stone.model = index;
        ClearLook(stone.gameObject, stone);

        var look = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stone.transform);
        Undo.RegisterCreatedObjectUndo(look, "Stone Model");
        look.name = ModelName;
        look.transform.localPosition = Vector3.zero;
        look.transform.localRotation = Quaternion.identity;
        look.transform.localScale = LookScale(model);   // the stone's own size: the stone (its scale)
        foreach (FallingPlatform platform in look.GetComponentsInChildren<FallingPlatform>(true)) platform.enabled = false;   // the stone falls itself

        foreach (Transform shadow in stone.shadows) if (shadow != null) BuildGhost(shadow, model);
        stone.ApplyColor();
        EditorUtility.SetDirty(stone);
    }

    static StonePath.Model ModelOf(StonePath path, int index) =>
        path != null && index >= 0 && index < path.models.Count && path.models[index].prefab != null ? path.models[index] : null;

    static Vector3 LookScale(StonePath.Model model) => model.prefab.transform.localScale * model.size;

    // Everything that makes it look like something: its children, and on itself anything but its PathStone
    static void ClearLook(GameObject holder, Component keep)
    {
        foreach (Transform child in holder.transform.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
        foreach (Component c in holder.GetComponents<Component>().Where(c => !(c is Transform) && c != keep && !(c is PathStone)).Reverse().ToArray())
            Undo.DestroyObjectImmediate(c);
    }

    // A shadow: the prefab, only its look (no scripts, no collisions), see-through
    static void BuildGhost(Transform shadow, StonePath.Model model)
    {
        ClearLook(shadow.gameObject, null);
        GameObject look = Object.Instantiate(model.prefab, shadow);
        Undo.RegisterCreatedObjectUndo(look, "Stone Shadow");
        look.name = ModelName;
        look.transform.localPosition = Vector3.zero;
        look.transform.localRotation = Quaternion.identity;
        look.transform.localScale = LookScale(model);
        foreach (MonoBehaviour script in look.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
        foreach (Collider c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (AudioSource source in look.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(source);
        Material ghost = ShadowMaterial();
        foreach (Renderer r in look.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(ghost, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    //---- Generate Stones

    [MenuItem(Menu + "Generate Stones", false, 331)]
    static void Generate()
    {
        StonePath path = Selected;
        List<PathStone> stones = path.Stones;
        if (stones.Count < 2) { Debug.LogError("[StonePath] It needs its two end stones (New Stone Path).", path); return; }
        PathStone first = stones[0], last = stones[stones.Count - 1];
        List<PathStone> middle = stones.GetRange(1, stones.Count - 2);
        if (middle.Count > 0 && !EditorUtility.DisplayDialog("Generate Stones", $"Replace the {middle.Count} stone(s) between the two ends?", "Replace", "Cancel")) return;

        Undo.SetCurrentGroupName("Generate Stones");
        foreach (PathStone stone in middle)
        {
            foreach (Transform shadow in stone.shadows) if (shadow != null) Undo.DestroyObjectImmediate(shadow.gameObject);
            Undo.DestroyObjectImmediate(stone.gameObject);
        }

        Vector3 a = first.transform.position, b = last.transform.position;
        float length = path.CurveLength(a, b);
        int count = Mathf.Max(0, Mathf.RoundToInt(length / Mathf.Max(path.spacing, 0.1f)) - 1);
        Quaternion startTurn = Quaternion.Inverse(Facing(path, a, b, 0f)) * first.transform.rotation;   // the first stone's turn, kept along the curve

        // Models picked at random (never the same twice in a row, if there are more)
        List<int> pool = Enumerable.Range(0, path.models.Count).Where(i => path.models[i].prefab != null && path.models[i].inGenerate).ToList();
        if (pool.Count == 0) pool.Add(first.model);
        int previous = first.model;

        int index = first.transform.GetSiblingIndex() + 1 + first.shadows.Count(s => s != null && s.parent == path.transform);
        for (int i = 1; i <= count; i++)
        {
            float t = AtDistance(path, a, b, length * i / (count + 1));
            List<int> choices = pool.Count > 1 ? pool.Where(m => m != previous).ToList() : pool;
            int model = choices[Random.Range(0, choices.Count)];
            previous = model;

            Vector3 position = path.CurvePoint(a, b, t) + Vector3.up * Random.Range(-path.randomHeight, path.randomHeight);
            Quaternion rotation = Quaternion.AngleAxis(Random.Range(-path.randomTurn, path.randomTurn), Vector3.up) * Facing(path, a, b, t) * startTurn;
            PathStone stone = MakeStone(path, $"Stone_{i + 1:00}", position, rotation, model);
            stone.transform.localScale = Vector3.Lerp(first.transform.localScale, last.transform.localScale, t) * path.sizeScale;
            stone.transform.SetSiblingIndex(index++);
        }
        if (last.name.StartsWith("Stone_")) { Undo.RecordObject(last.gameObject, "Generate Stones"); last.name = $"Stone_{count + 2:00}"; }

        EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
        Debug.Log($"[StonePath] '{path.name}': {count} stone(s) generated. Move, add or delete them as you like.", path);
    }

    // The curve's way at t, flat
    static Quaternion Facing(StonePath path, Vector3 a, Vector3 b, float t)
    {
        Vector3 way = path.CurvePoint(a, b, Mathf.Min(t + 0.01f, 1f)) - path.CurvePoint(a, b, Mathf.Max(t - 0.01f, 0f));
        way.y = 0f;
        return way.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(way.normalized, Vector3.up) : Quaternion.identity;
    }

    // t (0-1) where the curve has gone Distance meters
    static float AtDistance(StonePath path, Vector3 a, Vector3 b, float distance)
    {
        const int steps = 200;
        float walked = 0f;
        Vector3 previous = a;
        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = path.CurvePoint(a, b, i / (float)steps);
            float step = Vector3.Distance(previous, point);
            if (walked + step >= distance) return (i - 1 + (distance - walked) / Mathf.Max(step, 1e-6f)) / steps;
            walked += step;
            previous = point;
        }
        return 1f;
    }

    //---- Create Anchors

    [MenuItem(Menu + "Create Anchors", false, 332)]
    static void CreateAnchors()
    {
        StonePath path = Selected;
        List<PathStone> stones = path.Stones;
        if (stones.Count < 2) { Debug.LogError("[StonePath] It needs its two end stones.", path); return; }
        Bridge bridge = path.GetComponent<Bridge>();
        Undo.RecordObject(bridge, "Create Anchors");
        bridge.anchorA = Anchor(bridge, bridge.anchorA, stones[0].transform, "A");
        bridge.anchorB = Anchor(bridge, bridge.anchorB, stones[stones.Count - 1].transform, "B");
        EditorUtility.SetDirty(bridge);
        EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
    }

    // At the end stone, inside the island (its floating part) under it. An anchor it already has is moved there
    static Transform Anchor(Bridge bridge, Transform anchor, Transform stone, string label)
    {
        Transform island = null;
        foreach (RaycastHit hit in Physics.RaycastAll(stone.position + Vector3.up * 2f, Vector3.down, 40f).OrderBy(h => h.distance))
        {
            if (hit.transform.IsChildOf(bridge.transform)) continue;
            FloatMotion floating = hit.transform.GetComponentInParent<FloatMotion>();
            Island found = hit.transform.GetComponentInParent<Island>();
            island = floating != null ? floating.transform : found != null ? found.transform : null;
            if (island != null) break;
        }

        if (anchor == null)
        {
            anchor = new GameObject($"BridgeAnchor_{bridge.name}_{label}").transform;
            Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Create Anchors");
        }
        else Undo.RecordObject(anchor, "Create Anchors");
        if (island != null) Undo.SetTransformParent(anchor, island, "Create Anchors");
        anchor.position = stone.position;

        if (island == null) Debug.LogWarning($"[StonePath] '{bridge.name}': no island under end stone {label} ('{stone.name}'). Put anchor {label} inside its island by hand.", anchor);
        else Debug.Log($"[StonePath] '{bridge.name}': anchor {label} in '{island.name}'.", anchor);
        return anchor;
    }

    //---- Sort Stones

    [MenuItem(Menu + "Sort Stones", false, 333)]
    static void Sort()
    {
        StonePath path = Selected;
        Bridge bridge = path.GetComponent<Bridge>();
        List<PathStone> stones = path.Stones;
        if (stones.Count < 2) return;
        Vector3 a = bridge.anchorA != null ? bridge.anchorA.position : stones[0].transform.position;
        Vector3 b = bridge.anchorB != null ? bridge.anchorB.position : stones[stones.Count - 1].transform.position;
        Vector3 way = b - a;

        // From where the first stone is: each stone in order from A, followed by its shadows
        List<PathStone> sorted = stones.OrderBy(s => Vector3.Dot(s.transform.position - a, way)).ToList();
        Undo.RegisterFullObjectHierarchyUndo(path.gameObject, "Sort Stones");
        int slot = stones.Min(s => s.transform.GetSiblingIndex());
        for (int i = 0; i < sorted.Count; i++)
        {
            PathStone stone = sorted[i];
            stone.transform.SetSiblingIndex(slot++);
            if (stone.name.StartsWith("Stone_")) stone.name = $"Stone_{i + 1:00}";
            for (int k = 0; k < stone.shadows.Count; k++)
            {
                Transform shadow = stone.shadows[k];
                if (shadow == null || shadow.parent != path.transform) continue;
                shadow.SetSiblingIndex(slot++);
                shadow.name = $"{stone.name}_Shadow{k + 1}";
            }
        }
        EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
        Debug.Log($"[StonePath] '{path.name}': {sorted.Count} stones sorted from A to B.", path);
    }

    //---- Add Shadow

    [MenuItem(Menu + "Add Shadow", false, 334)]
    static void AddShadow()
    {
        PathStone[] stones = Selection.gameObjects.Select(g => g.GetComponent<PathStone>()).Where(s => s != null).ToArray();
        if (stones.Length == 0) { Debug.LogError("[StonePath] Select the stones (children of the path) that get a shadow."); return; }
        var made = new List<Object>();

        foreach (PathStone stone in stones)
        {
            StonePath path = stone.GetComponentInParent<StonePath>();
            StonePath.Model model = ModelOf(path, stone.model);
            if (model == null) { Debug.LogWarning($"[StonePath] '{stone.name}' has no Model to copy.", stone); continue; }

            Undo.RecordObject(stone, "Add Shadow");
            stone.shadows.RemoveAll(s => s == null);
            int number = stone.shadows.Count + 1;
            var shadow = new GameObject($"{stone.name}_Shadow{number}").transform;
            Undo.RegisterCreatedObjectUndo(shadow.gameObject, "Add Shadow");
            shadow.SetParent(stone.transform.parent, false);
            // 2 m to one side, the next one to the other, then further…
            float side = (number % 2 == 1 ? 1f : -1f) * 2f * ((number + 1) / 2);
            shadow.SetPositionAndRotation(stone.transform.position + stone.transform.right * side, stone.transform.rotation);
            shadow.localScale = stone.transform.localScale;
            shadow.SetSiblingIndex(stone.transform.GetSiblingIndex() + number);
            BuildGhost(shadow, model);

            stone.shadows.Add(shadow);
            if (stone.motion == PathStone.Motion.None || stone.motion == PathStone.Motion.Fall) stone.motion = PathStone.Motion.Slide;
            stone.ApplyColor();
            EditorUtility.SetDirty(stone);
            made.Add(shadow.gameObject);
        }
        if (made.Count == 0) return;
        Selection.objects = made.ToArray();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[StonePath] Shadow(s) added (selected): move each to the next point of its stone's route.");
    }

    static Material ShadowMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
        if (material != null) return material;
        material = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse")) { color = new Color(0.3f, 0.8f, 1f, 0.35f) };
        AssetDatabase.CreateAsset(material, ShadowMaterialPath);
        return material;
    }

    //---- Enabled only with a stone path selected

    [MenuItem(Menu + "Generate Stones", true)]
    [MenuItem(Menu + "Create Anchors", true)]
    [MenuItem(Menu + "Sort Stones", true)]
    static bool HasPath() => Selected != null;

    [MenuItem(Menu + "Add Shadow", true)]
    static bool HasStone() => Selection.gameObjects.Any(g => g.GetComponent<PathStone>() != null);
}

// PathStone > Model: a dropdown with its StonePath's Models; changing it swaps the stone's look (and its shadows')
[CustomPropertyDrawer(typeof(StoneModelAttribute))]
public class StoneModelDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var first = property.serializedObject.targetObject as PathStone;
        StonePath path = first != null ? first.GetComponentInParent<StonePath>() : null;
        if (path == null || path.models.Count == 0)
        {
            EditorGUI.LabelField(position, label.text, "Add Models to its StonePath");
            return;
        }

        string[] names = path.models.Select((m, i) => $"{i}: {(m.prefab != null ? m.prefab.name : "(empty)")}").ToArray();
        EditorGUI.BeginChangeCheck();
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        int chosen = EditorGUI.Popup(position, label.text, property.intValue, names);
        EditorGUI.showMixedValue = false;
        if (!EditorGUI.EndChangeCheck()) return;

        property.intValue = chosen;
        PathStone[] stones = property.serializedObject.targetObjects.OfType<PathStone>().ToArray();
        EditorApplication.delayCall += () =>   // (not while the Inspector is drawing)
        {
            foreach (PathStone stone in stones) StonePathTool.SetModel(stone, chosen);
            EditorSceneManager.MarkAllScenesDirty();
        };
    }
}
