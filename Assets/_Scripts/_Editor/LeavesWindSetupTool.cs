// TEMP: Memetica > Leaves & wind: set up (temporary). Run it once in each scene (Game_Old and Game), save, then delete this file.
//   - Every leaf particle system (material with "Lea" / "Leaf") gets FallingLeaves with LowPolySmoke.mat.
//     Autumn when its tree's material color is warm (orange, red, yellow, brown), Green otherwise: the Console
//     lists each one, so a wrong guess is fixed by hand (Kind).
//   - The old wind emitters (objects "WindParticles…", same paths every time) are turned off, and WindEffects
//     covers all the islands (one already in the scene stays where it is; if none, it is placed in their middle).
// Can be run again (it finds what a first run left). Ctrl+Z undoes everything.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LeavesWindSetupTool
{
    private const string LeafMaterialPath = "Assets/_Rendering/LowPolySmoke.mat";
    private const string WindMaterialPath = "Assets/_Rendering/Particles/Wind.mat";
    private const string UndoName = "Leaves & wind set up";

    [MenuItem("Memetica/Leaves & wind: set up (temporary)")]
    public static void SetUp()
    {
        Material leafMaterial = AssetDatabase.LoadAssetAtPath<Material>(LeafMaterialPath);
        if (leafMaterial == null) { Debug.LogError($"[Leaves] No material at {LeafMaterialPath}."); return; }

        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();
        int leaves = 0, oldWind = 0;
        Transform windParent = null;

        foreach (ParticleSystem ps in Object.FindObjectsOfType<ParticleSystem>(true))
        {
            if (ps.name.StartsWith("WindParticles"))
            {
                windParent = ps.transform.parent;
                if (!ps.gameObject.activeSelf) continue;
                Undo.RecordObject(ps.gameObject, UndoName);
                ps.gameObject.SetActive(false);
                oldWind++;
                continue;
            }

            FallingLeaves falling = ps.GetComponent<FallingLeaves>();
            Material old = ps.GetComponent<ParticleSystemRenderer>()?.sharedMaterial;
            bool isLeaves = falling != null || (old != null && (old.name.Contains("Lea") || old.name.Contains("Leaf")));
            if (!isLeaves) continue;

            if (falling == null)
            {
                falling = Undo.AddComponent<FallingLeaves>(ps.gameObject);
                falling.kind = WarmTree(ps.transform.parent, out string colorName) ? FallingLeaves.Kind.Autumn : FallingLeaves.Kind.Green;
                Debug.Log($"[Leaves] {Path(ps.transform)}: {falling.kind} (tree color: {colorName}).", ps);
            }
            Undo.RecordObject(falling, UndoName);
            falling.material = leafMaterial;
            PrefabUtility.RecordPrefabInstancePropertyModifications(falling);
            leaves++;
        }

        PlaceWind(windParent);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[Leaves] Done: {leaves} leaf systems, {oldWind} old wind emitters turned off. Save the scene.");
    }

    // WindEffects in the middle of all the islands (where they end up), its area over all of them and a bit more.
    // Already in the scene (e.g. on the wind sound's object): stays where it is, only its area and material are set
    static void PlaceWind(Transform parent)
    {
        Island[] islands = Object.FindObjectsOfType<Island>(true);
        if (islands.Length == 0) { Debug.LogWarning("[Leaves] No islands: WindEffects not placed."); return; }
        Bounds bounds = new Bounds(islands[0].transform.position, Vector3.zero);
        foreach (Island island in islands) bounds.Encapsulate(island.transform.position);

        // Only one: the one on another object (the wind sound) wins; extra "WindEffects" objects made by earlier runs go
        WindEffects[] all = Object.FindObjectsOfType<WindEffects>(true);
        WindEffects wind = System.Array.Find(all, w => w.name != "WindEffects") ?? (all.Length > 0 ? all[0] : null);
        foreach (WindEffects extra in all)
        {
            if (extra == wind) continue;
            if (extra.name == "WindEffects" && extra.GetComponents<Component>().Length == 2) Undo.DestroyObjectImmediate(extra.gameObject);
            else Undo.DestroyObjectImmediate(extra);
            Debug.Log("[Leaves] Removed an extra WindEffects.");
        }
        if (wind == null)
        {
            var go = new GameObject("WindEffects");
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            if (parent != null) Undo.SetTransformParent(go.transform, parent, UndoName);
            wind = Undo.AddComponent<WindEffects>(go);
            Undo.RecordObject(go.transform, UndoName);
            go.transform.SetPositionAndRotation(bounds.center + Vector3.up * 6f, Quaternion.identity);   // head height and above
        }
        Undo.RecordObject(wind, UndoName);
        wind.area = new Vector3(bounds.size.x + 40f, 20f, bounds.size.z + 40f);
        wind.perSecond = 0.6f;   // a few now and then
        if (wind.material == null) wind.material = AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath);
        Debug.Log($"[Leaves] WindEffects on '{wind.name}' at {wind.transform.position}, area {wind.area}.", wind);
    }

    // An autumn tree: no green on its materials and a strong warm one (red, orange, yellow; a brown trunk is too dull)
    static bool WarmTree(Transform tree, out string colorName)
    {
        colorName = "none (textured: green)";
        if (tree == null) return false;
        bool warm = false;
        foreach (Renderer r in tree.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !m.HasProperty("_Color")) continue;
                Color c = m.GetColor("_Color");
                Color.RGBToHSV(c, out float h, out float s, out float v);
                bool green = s > 0.15f && h > 0.17f && h < 0.45f;
                bool strongWarm = s > 0.45f && (h < 0.17f || h > 0.95f);
                if (green) { colorName = $"{m.name} #{ColorUtility.ToHtmlStringRGB(c)}"; return false; }
                if (strongWarm && !warm) { warm = true; colorName = $"{m.name} #{ColorUtility.ToHtmlStringRGB(c)}"; }
            }
        }
        return warm;
    }

    static string Path(Transform t) => t.parent != null ? t.parent.name + "/" + t.name : t.name;
}
