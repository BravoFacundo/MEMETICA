using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Memetica > Pedestals > Make Every Pedestal A Prefab (open scene): every pedestal that isn't a prefab instance (the
// migration had to unpack them to change their script) is swapped for an instance of the prefab of its kind:
//   KeyPedestal         → Pedestal_Keyed
//   RandomizerPedestal  → Pedestal_Randomizer
//   Pedestal (black)    → Pedestal_Free (Pedestal.prefab, renamed the first time: same asset, nothing breaks)
// The instance takes its place, name, transform and every value (its own as overrides), parts it has that the prefab
// doesn't (MemeDelivery, a child added by hand) come along, and everything that pointed to the old one points to the
// new one. One undo step per scene (Ctrl+Z). Run it in Game and in OriginalGame, then save.
public static class PedestalPrefabs
{
    const string Folder = "Assets/_Prefabs/Puzzle/";

    [MenuItem("Memetica/Pedestals/Make Every Pedestal A Prefab (open scene)")]
    static void Run()
    {
        RenameFree();
        var prefabs = new Dictionary<System.Type, GameObject>
        {
            { typeof(KeyPedestal), AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Pedestal_Keyed.prefab") },
            { typeof(RandomizerPedestal), AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Pedestal_Randomizer.prefab") },
            { typeof(Pedestal), AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Pedestal_Free.prefab") },
        };

        Undo.SetCurrentGroupName("Make Every Pedestal A Prefab");
        int group = Undo.GetCurrentGroup();
        var report = new System.Text.StringBuilder("[PedestalPrefabs]\n");
        int done = 0;
        foreach (Pedestal ped in Object.FindObjectsOfType<Pedestal>(true))
        {
            if (ped == null || PrefabUtility.IsPartOfPrefabInstance(ped)) continue;   // already a prefab
            prefabs.TryGetValue(ped.GetType(), out GameObject prefab);
            if (prefab == null) { report.AppendLine($"  !! '{ped.name}' ({ped.GetType().Name}): no prefab for its kind, left as it is"); continue; }
            string where = PathOf(ped.transform);
            GameObject made = Replace(ped.gameObject, prefab, out int moved, out int refs);
            report.AppendLine($"  {where} → {prefab.name}" + (moved > 0 ? $", {moved} extra part(s) kept" : "") + (refs > 0 ? $", {refs} reference(s) repointed" : ""));
            done++;
        }
        Undo.CollapseUndoOperations(group);
        if (done > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        report.AppendLine(done > 0 ? $"  {done} pedestal(s) are prefabs now. Save the scene." : "  Every pedestal already is a prefab.");
        Debug.Log(report.ToString());
    }

    // Pedestal.prefab (the black one) → Pedestal_Free.prefab, once. Renaming keeps its asset: every instance stays linked
    static void RenameFree()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Pedestal_Free.prefab") != null) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Pedestal.prefab") == null) return;
        string error = AssetDatabase.RenameAsset(Folder + "Pedestal.prefab", "Pedestal_Free");
        Debug.Log(string.IsNullOrEmpty(error) ? "[PedestalPrefabs] Pedestal.prefab renamed Pedestal_Free." : "[PedestalPrefabs] Couldn't rename Pedestal.prefab: " + error);
    }

    static GameObject Replace(GameObject old, GameObject prefab, out int moved, out int refs)
    {
        var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.scene);
        Undo.RegisterCreatedObjectUndo(made, "Pedestal prefab");
        Transform o = old.transform, n = made.transform;
        n.SetParent(o.parent, false);
        n.SetSiblingIndex(o.GetSiblingIndex());
        n.localPosition = o.localPosition;
        n.localRotation = o.localRotation;
        n.localScale = o.localScale;
        made.name = old.name;
        made.layer = old.layer;
        made.SetActive(old.activeSelf);

        // Old object → its counterpart (same place in the hierarchy, same name); components by type and order
        var map = new Dictionary<Object, Object>();
        var copies = new List<(Component from, Component to)>();
        var extra = new List<(Transform child, Transform under)>();
        Match(o, n, map, copies, extra, true);

        // Parts the prefab doesn't have: moved over as they are
        foreach (var (child, under) in extra) Undo.SetTransformParent(child, under, "Pedestal prefab");
        moved = extra.Count;

        // Every value, then the references inside it to the old parts → the new parts
        foreach (var (from, to) in copies)
        {
            EditorUtility.CopySerialized(from, to);
            var so = new SerializedObject(to);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null &&
                    map.TryGetValue(p.objectReferenceValue, out Object now))
                    p.objectReferenceValue = now;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(to);
        }

        refs = Repoint(map, new HashSet<Component>(copies.Select(c => c.to)));
        Undo.DestroyObjectImmediate(old);
        return made;
    }

    static void Match(Transform o, Transform n, Dictionary<Object, Object> map, List<(Component, Component)> copies,
                      List<(Transform, Transform)> extra, bool root)
    {
        map[o.gameObject] = n.gameObject;
        map[o] = n;
        if (!root)
        {
            Undo.RecordObject(n, "Pedestal prefab");
            n.localPosition = o.localPosition;
            n.localRotation = o.localRotation;
            n.localScale = o.localScale;
            if (n.gameObject.activeSelf != o.gameObject.activeSelf) n.gameObject.SetActive(o.gameObject.activeSelf);
        }

        // Components: the k-th of each type on the old one ↔ the k-th on the new one (added if the prefab lacks it)
        var used = new HashSet<Component>();
        foreach (Component oc in o.GetComponents<Component>())
        {
            if (oc == null || oc is Transform) continue;
            System.Type type = oc.GetType();
            Component nc = n.GetComponents(type).FirstOrDefault(c => c.GetType() == type && !used.Contains(c));
            if (nc == null) nc = Undo.AddComponent(n.gameObject, type);
            if (nc == null) continue;
            used.Add(nc);
            map[oc] = nc;
            copies.Add((oc, nc));
        }
        // What the prefab has and the old one didn't: off (removed on the instance), so it behaves as before
        foreach (Component nc in n.GetComponents<Component>())
            if (nc != null && !(nc is Transform) && !used.Contains(nc) && !(nc is Pedestal)) Undo.DestroyObjectImmediate(nc);

        // Children by name (in order); one the prefab doesn't have comes along as it is
        var taken = new HashSet<Transform>();
        foreach (Transform oChild in o.Cast<Transform>().ToList())
        {
            Transform nChild = n.Cast<Transform>().FirstOrDefault(c => c.name == oChild.name && !taken.Contains(c));
            if (nChild == null) { extra.Add((oChild, n)); continue; }
            taken.Add(nChild);
            Match(oChild, nChild, map, copies, extra, false);
        }
        foreach (Transform nChild in n.Cast<Transform>())   // a part of the prefab the old one didn't have: hidden
            if (!taken.Contains(nChild) && nChild.gameObject.activeSelf) { Undo.RecordObject(nChild.gameObject, "Pedestal prefab"); nChild.gameObject.SetActive(false); }
    }

    // Everything else in the scene that pointed to the old pedestal (or a part of it): to the new one
    static int Repoint(Dictionary<Object, Object> map, HashSet<Component> fixedAlready)
    {
        int count = 0;
        foreach (Component comp in Object.FindObjectsOfType<Component>(true))
        {
            if (comp == null || comp is Transform || map.ContainsKey(comp) || fixedAlready.Contains(comp)) continue;   // (the old ones go; the new ones are done)
            var so = new SerializedObject(comp);
            SerializedProperty p = so.GetIterator();
            bool changed = false;
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                if (!map.TryGetValue(p.objectReferenceValue, out Object now)) continue;
                p.objectReferenceValue = now;
                changed = true;
                count++;
            }
            if (changed) so.ApplyModifiedProperties();
        }
        return count;
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
