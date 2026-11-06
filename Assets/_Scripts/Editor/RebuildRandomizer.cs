using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Right click on a RandomizerPedestal (⋮) > Move To Its Platform (and make the prefab):
// the hub's randomizer goes from the pedestal to the platform it stands on, so the whole thing is one piece that turns:
//   Platform (AimPlatform)              →  Pedestal (AimPlatform, RandomizerPedestal, PedestalRetire, AudioSource, trigger)
//   ├── Pedestal (RandomizerPedestal…)  →  ├── Body (the physical pedestal: what slams; the Pedestal's Body)
//   └── SpiningPlatform                 →  └── SpiningPlatform
// Every value is copied, every reference to the old components (GameManager's Start Pedestal…) points to the new ones,
// and the result is saved as Assets/_Prefabs/Puzzle/Pedestal_Randomizer.prefab (replacing it). Ctrl+Z undoes it
// (except the prefab file). Delete this script once done.
public static class RebuildRandomizer
{
    const string PrefabPath = "Assets/_Prefabs/Puzzle/Pedestal_Randomizer.prefab";

    [MenuItem("CONTEXT/RandomizerPedestal/Move To Its Platform (and make the prefab)")]
    static void Run(MenuCommand command)
    {
        var old = (RandomizerPedestal)command.context;
        AimPlatform aim = old.GetComponentInParent<AimPlatform>();
        if (aim == null || aim.gameObject == old.gameObject)
        {
            Debug.LogWarning($"[RebuildRandomizer] '{old.name}' isn't inside a platform (AimPlatform), or is already on it.", old);
            return;
        }
        GameObject root = aim.gameObject;
        Transform body = old.transform;
        Undo.SetCurrentGroupName("Move Randomizer To Its Platform");
        int group = Undo.GetCurrentGroup();
        var map = new Dictionary<Object, Object>();

        // Audio and trigger first (the pedestal requires them): same settings, the trigger where it was in the world
        AudioSource oldAudio = old.GetComponent<AudioSource>();
        AudioSource audio = root.GetComponent<AudioSource>();
        if (audio == null) audio = Undo.AddComponent<AudioSource>(root);
        if (oldAudio != null) { EditorUtility.CopySerialized(oldAudio, audio); map[oldAudio] = audio; }

        SphereCollider oldTrigger = old.GetComponent<SphereCollider>();
        SphereCollider trigger = root.GetComponent<SphereCollider>();
        if (trigger == null) trigger = Undo.AddComponent<SphereCollider>(root);
        if (oldTrigger != null)
        {
            EditorUtility.CopySerialized(oldTrigger, trigger);
            trigger.center = root.transform.InverseTransformPoint(body.TransformPoint(oldTrigger.center));
            trigger.radius = oldTrigger.radius * MaxScale(body) / Mathf.Max(MaxScale(root.transform), 0.0001f);   // same size in the world
            map[oldTrigger] = trigger;
        }

        // The randomizer, with every value; its Body is the old pedestal
        var randomizer = Undo.AddComponent<RandomizerPedestal>(root);
        EditorUtility.CopySerialized(old, randomizer);
        randomizer.body = body;
        map[old] = randomizer;

        PedestalRetire oldRetire = old.GetComponent<PedestalRetire>();
        if (oldRetire != null)
        {
            var retire = Undo.AddComponent<PedestalRetire>(root);
            EditorUtility.CopySerialized(oldRetire, retire);
            map[oldRetire] = retire;
        }

        int remapped = Remap(map);

        // The old components go (the retire first: it requires the pedestal; the pedestal before what it requires)
        if (oldRetire != null) Undo.DestroyObjectImmediate(oldRetire);
        Undo.DestroyObjectImmediate(old);
        if (oldTrigger != null) Undo.DestroyObjectImmediate(oldTrigger);
        if (oldAudio != null) Undo.DestroyObjectImmediate(oldAudio);

        Undo.RecordObject(root, "Rename");
        root.name = "Pedestal";
        Undo.RecordObject(body.gameObject, "Rename");
        body.gameObject.name = "Body";
        Undo.CollapseUndoOperations(group);

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        Selection.activeGameObject = root;
        Debug.Log($"[RebuildRandomizer] Done: '{root.name}' holds the randomizer, 'Body' slams; {remapped} reference(s) now point to it. " +
                  $"Prefab: {PrefabPath}. Save the scene.", root);
    }

    // Every reference in the open scenes to an old component, to its new one
    static int Remap(Dictionary<Object, Object> map)
    {
        int count = 0;
        foreach (Component comp in Object.FindObjectsOfType<Component>(true))
        {
            if (comp == null || comp is Transform || map.ContainsKey(comp)) continue;
            var so = new SerializedObject(comp);
            SerializedProperty p = so.GetIterator();
            bool changed = false;
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                if (!map.TryGetValue(p.objectReferenceValue, out Object to)) continue;
                p.objectReferenceValue = to;
                changed = true;
                count++;
                Debug.Log($"[RebuildRandomizer] '{comp.name}' ({comp.GetType().Name}) {p.propertyPath} → the new one.", comp);
            }
            if (changed) so.ApplyModifiedProperties();
        }
        return count;
    }

    static float MaxScale(Transform t) => Mathf.Max(t.lossyScale.x, t.lossyScale.y, t.lossyScale.z);
}
