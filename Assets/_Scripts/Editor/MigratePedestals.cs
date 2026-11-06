// One-time migration to the pedestal kinds (Memetica > Check > Migrate Pedestals). In every scene in _Scenes:
//   white pedestals (Keyless off)   → KeyPedestal: Key Meme → Key, Key ID → Key ID (orbs), Drain → Download, Clue → Hint
//   with a MemeRandomizer           → RandomizerPedestal: all of the randomizer's values, its axes' names → its lists
//   black ones                      → stay Pedestal
//   all of them: what they raise by name → their Raises list, in order (island "0" → Completes Game);
//   PedestalReactions → the pedestal's Reactions; the same Group ID → a PedestalGroup next to them
// The component changes its kind in place (its values and everything that points at it stay). A white or randomizer
// pedestal that is a prefab instance is unpacked first (Unity can't change a prefab instance's script); the Console
// lists them. Also makes Pedestal_Keyed and Pedestal_Randomizer prefabs next to Pedestal, for new ones.
// Commit first if you want a way back. Once it ran, this file, MemeRandomizer.cs and PedestalReactions.cs can go.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MigratePedestals
{
    static readonly List<string> report = new List<string>();
    static LevelManager level;
    static List<Island> sceneIslands;
    static List<Elevator> sceneElevators;
    static List<Bridge> sceneBridges;

    [MenuItem("Memetica/Check/Migrate Pedestals", false, 411)]
    static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Migrate Pedestals",
                "Turns white pedestals into KeyPedestal and randomizers into RandomizerPedestal, moves what they raise " +
                "from names to references, reactions and groups, in every scene in _Scenes (saved). White and " +
                "randomizer pedestals that are prefab instances get unpacked. Commit first if you want a way back.", "Migrate", "Cancel")) return;

        report.Clear();
        var openScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            MigratePrefabs();
            string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Scenes" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Migrate Pedestals", scenes[i], (float)i / scenes.Length);
                var scene = EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
                if (MigrateScene(scene.name)) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            MakeKindPrefabs();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (openScenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(openScenes);
        }
        Debug.Log("[MigratePedestals] Done:\n" + string.Join("\n", report));
    }

    //---- Prefabs: the Pedestal prefab's own reactions and names (kinds only in scenes)

    static void MigratePrefabs()
    {
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (Pedestal pedestal in root.GetComponentsInChildren<Pedestal>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(pedestal)) continue;   // (a nested Pedestal prefab: done in its own asset)
                changed |= MoveReactions(pedestal);
                if (pedestal.ControlledIslands.Length > 0 || pedestal.ControlledBridges.Length > 0)
                    report.Add($"  {path}: '{pedestal.name}' raises by name (left as is: names only resolve in a scene)");
            }
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); report.Add($"{path}: reactions moved into the pedestal"); }
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    //---- A scene

    static bool MigrateScene(string sceneName)
    {
        level = Object.FindObjectOfType<LevelManager>(true);
        sceneIslands = Object.FindObjectsOfType<Island>(true).ToList();
        sceneElevators = Object.FindObjectsOfType<Elevator>(true).ToList();
        sceneBridges = Object.FindObjectsOfType<Bridge>(true).ToList();
        bool changed = false;
        var groups = new Dictionary<string, List<Pedestal>>();

        foreach (Pedestal found in Object.FindObjectsOfType<Pedestal>(true))
        {
            if (found is KeyPedestal || found is RandomizerPedestal) continue;   // (already)
            GameObject go = found.gameObject;
            MemeRandomizer randomizer = go.GetComponent<MemeRandomizer>();
            Pedestal pedestal = found;

            if (randomizer != null) pedestal = ChangeKind(pedestal, typeof(RandomizerPedestal), sceneName);
            else if (!pedestal.KeylessPedestal) pedestal = ChangeKind(pedestal, typeof(KeyPedestal), sceneName);
            if (pedestal == null) continue;

            var so = new SerializedObject(pedestal);
            if (pedestal is KeyPedestal) KeyFields(so, pedestal);
            if (pedestal is RandomizerPedestal) RandomizerFields(so, randomizer);
            RaisedByName(so, pedestal);
            so.ApplyModifiedPropertiesWithoutUndo();

            MoveReactions(pedestal);
            if (randomizer != null) Object.DestroyImmediate(randomizer);
            pedestal.ApplyVisual();   // (white or black, by its kind)
            if (!string.IsNullOrEmpty(pedestal.groupID))
            {
                if (!groups.TryGetValue(pedestal.groupID, out var list)) groups[pedestal.groupID] = list = new List<Pedestal>();
                list.Add(pedestal);
            }
            report.Add($"{sceneName}: '{pedestal.name}' → {pedestal.GetType().Name}");
            changed = true;
        }

        foreach (var pair in groups) MakeGroup(pair.Key, pair.Value, sceneName);
        return changed;
    }

    // Changes the component's script in place (values and references stay). A prefab instance is unpacked first
    static Pedestal ChangeKind(Pedestal pedestal, System.Type kind, string sceneName)
    {
        GameObject go = pedestal.gameObject;
        while (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            report.Add($"  {sceneName}: unpacked '{root.name}' (to make '{go.name}' a {kind.Name})");
        }
        int index = System.Array.IndexOf(go.GetComponents<Component>(), pedestal);
        MonoScript script = FindScript(kind);
        if (script == null) { report.Add($"  !! {kind.Name} script not found: '{go.name}' left as is"); return pedestal; }
        var so = new SerializedObject(pedestal);
        so.FindProperty("m_Script").objectReferenceValue = script;
        so.ApplyModifiedPropertiesWithoutUndo();
        Component changed = go.GetComponents<Component>()[index];
        return changed as Pedestal;
    }

    static MonoScript FindScript(System.Type type) =>
        AssetDatabase.FindAssets($"t:MonoScript {type.Name}").Select(g => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g)))
                     .FirstOrDefault(s => s != null && s.GetClass() == type);

    //---- Fields

    static void KeyFields(SerializedObject so, Pedestal old)
    {
        so.FindProperty("key").objectReferenceValue = old.keyMeme;
        so.FindProperty("keyId").stringValue = old.KeyID;
        so.FindProperty("downloadMeme").boolValue = old.drainMeme;
        Painting painting = null;
        if (old.cluePainting != null)
        {
            painting = old.cluePainting.GetComponentInParent<Painting>();
            if (painting == null) painting = old.cluePainting.GetComponentInChildren<Painting>(true);
        }
        so.FindProperty("hint").objectReferenceValue = painting;
        so.FindProperty("keyMeme").objectReferenceValue = null;
        so.FindProperty("KeyID").stringValue = "";
        so.FindProperty("cluePainting").objectReferenceValue = null;
    }

    static readonly string[] RandomizerValues =
    {
        "forceResult", "raffleTime", "tickClip", "winClip", "turnTime", "targetRise", "islandsAfter", "islandRise",
        "islandGap", "bridgesAfter", "bridgeRise", "bridgeGap", "snapRadius", "walkToStand", "standPoint",
        "standDistance", "standWalkSpeed", "lockPlayer", "focusView", "focusRadius", "focusFov"
    };

    static void RandomizerFields(SerializedObject so, MemeRandomizer old)
    {
        if (old == null) return;
        var from = new SerializedObject(old);
        foreach (string field in RandomizerValues) Copy(from.FindProperty(field), so.FindProperty(field));

        SerializedProperty axes = so.FindProperty("axes");
        axes.ClearArray();
        for (int i = 0; i < old.axes.Count; i++)
        {
            MemeRandomizer.Axis axis = old.axes[i];
            axes.InsertArrayElementAtIndex(i);
            SerializedProperty to = axes.GetArrayElementAtIndex(i);
            to.FindPropertyRelative("meme").objectReferenceValue = axis.meme;
            to.FindPropertyRelative("aimAt").objectReferenceValue = axis.aimAt;
            to.FindPropertyRelative("sequence").ClearArray();
            Resolve(axis.sequence, to.FindPropertyRelative("way"), out _, old.name);
        }
    }

    // Its old names (Controlled Islands, then Bridges: the old order) into Raises
    static void RaisedByName(SerializedObject so, Pedestal old)
    {
        var names = new List<string>(old.ControlledIslands ?? new string[0]);
        foreach (string id in old.ControlledBridges ?? new string[0]) names.Add(level != null ? level.bridgePrefix + id : id);
        if (names.Count == 0) return;
        Resolve(names.ToArray(), so.FindProperty("raises"), out bool end, old.name);
        if (end) so.FindProperty("completesGame").boolValue = true;
        so.FindProperty("ControlledIslands").ClearArray();
        so.FindProperty("ControlledBridges").ClearArray();
    }

    // Names (Island_09, Bridge_T1, or just the ID) into the list, in order. "0": the end of the game
    static void Resolve(string[] names, SerializedProperty list, out bool end, string owner)
    {
        end = false;
        if (names == null) return;
        foreach (string raw in names)
        {
            string entry = raw != null ? raw.Trim() : "";
            if (entry.Length == 0) continue;
            if (entry == "0") { end = true; continue; }
            string ip = level != null ? level.islandPrefix : "Island_", ep = level != null ? level.elevatorPrefix : "Elevator_", bp = level != null ? level.bridgePrefix : "Bridge_";
            Elevator elevator = Find(sceneElevators, entry.StartsWith(ep) ? entry : ep + entry);
            Island island = Find(sceneIslands, entry.StartsWith(ip) ? entry : ip + entry);
            Bridge bridge = Find(sceneBridges, entry.StartsWith(bp) ? entry : bp + entry);
            if (entry.StartsWith(bp)) { island = null; elevator = null; }
            if (entry.StartsWith(ip) || entry.StartsWith(ep)) bridge = null;
            if (elevator != null) Add(list, elevator.transform);
            else if (island != null) Add(list, island.transform);
            else if (bridge != null) Add(list, bridge.transform);
            else report.Add($"  !! '{owner}' raised '{entry}', not found in the scene: left out");
        }
    }

    static T Find<T>(List<T> list, string objectName) where T : Component =>
        list.FirstOrDefault(c => LevelManager.NormalizeName(c.name) == LevelManager.NormalizeName(objectName));

    static void Add(SerializedProperty list, Object item)
    {
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
    }

    static void Copy(SerializedProperty from, SerializedProperty to)
    {
        if (from == null || to == null) return;
        switch (from.propertyType)
        {
            case SerializedPropertyType.Float: to.floatValue = from.floatValue; break;
            case SerializedPropertyType.Integer: to.intValue = from.intValue; break;
            case SerializedPropertyType.Boolean: to.boolValue = from.boolValue; break;
            case SerializedPropertyType.String: to.stringValue = from.stringValue; break;
            case SerializedPropertyType.ObjectReference: to.objectReferenceValue = from.objectReferenceValue; break;
            case SerializedPropertyType.Enum: to.enumValueIndex = from.enumValueIndex; break;
        }
    }

    //---- Reactions: PedestalReactions' values into the pedestal, then it goes

    static readonly (string from, string to)[] ReactionValues =
    {
        ("like", "like"), ("dislike", "dislike"), ("question", "question"), ("height", "reactionHeight"), ("size", "reactionSize"),
        ("spread", "reactionSpread"), ("riseSpeed", "reactionRise"), ("lifetime", "reactionLifetime"), ("burst", "reactionBurst"),
        ("questionRate", "questionRate"), ("sortingOrder", "reactionOrder")
    };

    static bool MoveReactions(Pedestal pedestal)
    {
        PedestalReactions reactions = pedestal.GetComponent<PedestalReactions>();
        if (reactions == null) return false;
        var from = new SerializedObject(reactions);
        var to = new SerializedObject(pedestal);
        foreach (var (a, b) in ReactionValues) Copy(from.FindProperty(a), to.FindProperty(b));
        to.ApplyModifiedPropertiesWithoutUndo();
        if (!PrefabUtility.IsPartOfPrefabInstance(reactions) || PrefabUtility.IsAddedComponentOverride(reactions))
            Object.DestroyImmediate(reactions, true);
        return true;
    }

    //---- Groups

    static void MakeGroup(string id, List<Pedestal> members, string sceneName)
    {
        var go = new GameObject($"PedestalGroup_{id}");
        go.transform.SetParent(members[0].transform.parent, false);
        go.transform.position = members.Aggregate(Vector3.zero, (sum, p) => sum + p.transform.position) / members.Count;
        PedestalGroup group = go.AddComponent<PedestalGroup>();
        group.members = new List<Pedestal>(members);
        foreach (Pedestal member in members)
        {
            var so = new SerializedObject(member);
            so.FindProperty("groupID").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        report.Add($"{sceneName}: group '{id}' → {go.name} ({members.Count} pedestals)");
    }

    //---- Prefabs for new ones: Pedestal_Keyed and Pedestal_Randomizer, copies of Pedestal with their kind

    static void MakeKindPrefabs()
    {
        string basePath = AssetDatabase.FindAssets("Pedestal t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                                       .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == "Pedestal");
        if (basePath == null) { report.Add("  !! No Pedestal prefab found: no kind prefabs made"); return; }
        MakeKindPrefab(basePath, "Pedestal_Keyed", typeof(KeyPedestal));
        MakeKindPrefab(basePath, "Pedestal_Randomizer", typeof(RandomizerPedestal));
    }

    static void MakeKindPrefab(string basePath, string newName, System.Type kind)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(basePath), newName + ".prefab").Replace('\\', '/');
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;   // (made before)
        if (!AssetDatabase.CopyAsset(basePath, path)) { report.Add($"  !! Could not copy {basePath} to {path}"); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Pedestal pedestal = root.GetComponent<Pedestal>();
        if (pedestal != null)
        {
            var so = new SerializedObject(pedestal);
            so.FindProperty("m_Script").objectReferenceValue = FindScript(kind);
            so.ApplyModifiedPropertiesWithoutUndo();
            Pedestal changed = root.GetComponent<Pedestal>();
            if (changed != null) changed.ApplyVisual();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            report.Add($"New prefab {path}");
        }
        PrefabUtility.UnloadPrefabContents(root);
    }
}
