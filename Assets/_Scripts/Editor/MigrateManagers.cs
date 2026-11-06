using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Memetica > Check > Migrate Managers: moves the LevelManager's start and end values to the GameManager, once per scene
// (every scene in Assets/_Scenes with a LevelManager):
//   Start Fade Time, Skip Elevator / Look At / Meme…, Ending Duration / Rise… / Popup, Menu Scene → GameManager
//   Skip Tutorial → DebugTools (pressing Play in the scene)
//   Memes with a Reserve Group (MemeGroup, going away): the group's spare for its slot becomes its Data
//   Player: the respawn values of PlayerMove go to a PlayerRespawn added next to it (the prefabs first, the base before
//   its variants, then each scene's Player, keeping its overrides)
// A scene without a GameManager (OriginalGame) gets one next to the LevelManager: Minutes 0 (no countdown), Ending Ascend.
// Each scene is saved; a scene already migrated is skipped. Delete this script once every scene is done.
public static class MigrateManagers
{
    [MenuItem("Memetica/Check/Migrate Managers")]
    static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var report = new StringBuilder("[MigrateManagers]\n");
        PlayerPrefabs(report);

        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Unused/")) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            LevelManager level = Object.FindObjectOfType<LevelManager>(true);
            int memes = MemesOffGroups(scene.name, report) + ScenePlayers(scene.name, report);
            if (level == null) { if (memes > 0) EditorSceneManager.SaveScene(scene); continue; }
            if (level.migratedToGameManager) { report.AppendLine($"  {scene.name}: already migrated"); if (memes > 0) EditorSceneManager.SaveScene(scene); continue; }

            GameManager game = Object.FindObjectOfType<GameManager>(true);
            bool made = game == null;
            if (made)
            {
                var go = new GameObject("GameManager");
                go.transform.SetParent(level.transform.parent, false);
                go.transform.SetSiblingIndex(level.transform.GetSiblingIndex() + 1);
                game = go.AddComponent<GameManager>();
                game.minutes = 0f;
                game.ending = GameManager.Ending.Ascend;
            }
            level.CopyOldValuesTo(game);

            DebugTools debug = Object.FindObjectOfType<DebugTools>(true);
            if (debug != null) { debug.skipTutorial = level.oldSkipTutorial; EditorUtility.SetDirty(debug); }

            level.migratedToGameManager = true;
            EditorUtility.SetDirty(level);
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            report.AppendLine($"  {scene.name}: " + (made ? "GameManager made (no countdown, Ending Ascend)" : $"into '{game.name}'") +
                              $", Skip Elevator {Name(game.skipElevator)}, Skip Meme {Name(game.skipMeme)}, Ending Popup {Name(game.endingPopup)}" +
                              (debug != null ? $", DebugTools Skip Tutorial {debug.skipTutorial}" : ", no DebugTools (Skip Tutorial not kept)"));
        }

        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log(report.ToString());
    }

    // Each meme with a Reserve Group: Data = the group's spare for its slot (a Random Reserve group: its first pick), group cleared
    static int MemesOffGroups(string scene, StringBuilder report)
    {
        int count = 0;
        foreach (Meme meme in Object.FindObjectsOfType<Meme>(true))
        {
            if (meme.reserveGroup == null) continue;
            int slot = Mathf.Clamp(meme.reserveSlot, 1, Mathf.Max(meme.reserveGroup.reserve.Count, 1));
            MemeData spare = meme.reserveGroup.reserve.Count > 0 ? meme.reserveGroup.reserve[slot - 1] : null;
            report.AppendLine($"  {scene}: '{meme.name}' Data {Name(meme.data)} → {Name(spare)} (from '{meme.reserveGroup.name}' slot {slot}" +
                              (meme.reserveGroup.randomReserve ? ", it was RANDOM: now fixed" : "") + ")");
            if (spare != null) meme.data = spare;
            meme.reserveGroup = null;
            EditorUtility.SetDirty(meme);
            count++;
        }
        if (count > 0) EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return count;
    }

    // Prefabs with a PlayerMove (Player_Advanced, then its variant Player_Original): add PlayerRespawn, copy the old values
    static void PlayerPrefabs(StringBuilder report)
    {
        var paths = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null && asset.GetComponentInChildren<PlayerMove>(true) != null) paths.Add(path);
        }
        // Bases before variants (a variant gets the PlayerRespawn from its base, then keeps its own values as overrides)
        paths.Sort((a, b) => IsVariant(a).CompareTo(IsVariant(b)));

        foreach (string path in paths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            foreach (PlayerMove move in root.GetComponentsInChildren<PlayerMove>(true))
            {
                PlayerRespawn respawn = move.GetComponent<PlayerRespawn>();
                bool added = respawn == null;
                if (added) respawn = move.gameObject.AddComponent<PlayerRespawn>();
                move.CopyOldValuesTo(respawn);
                move.migratedToPlayerRespawn = true;
                report.AppendLine($"  {System.IO.Path.GetFileName(path)}: PlayerRespawn {(added ? "added" : "updated")} (Respawn Style {respawn.respawnStyle})");
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool IsVariant(string path) =>
        PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(path)) == PrefabAssetType.Variant;

    // Each Player in the scene: its own respawn values (overrides included) into its PlayerRespawn
    static int ScenePlayers(string scene, StringBuilder report)
    {
        int count = 0;
        foreach (PlayerMove move in Object.FindObjectsOfType<PlayerMove>(true))
        {
            PlayerRespawn respawn = move.GetComponent<PlayerRespawn>();
            if (respawn == null) respawn = move.gameObject.AddComponent<PlayerRespawn>();
            move.CopyOldValuesTo(respawn);
            move.migratedToPlayerRespawn = true;
            if (PrefabUtility.IsPartOfPrefabInstance(respawn))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(respawn);
                PrefabUtility.RecordPrefabInstancePropertyModifications(move);
            }
            EditorUtility.SetDirty(respawn);
            EditorUtility.SetDirty(move);
            report.AppendLine($"  {scene}: Player '{move.name}' respawn → PlayerRespawn (Respawn Style {respawn.respawnStyle})");
            count++;
        }
        if (count > 0) EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return count;
    }

    static string Name(Object o) => o != null ? $"'{o.name}'" : "-";
}
