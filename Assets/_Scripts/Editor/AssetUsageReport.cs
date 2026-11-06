// Editor tool: a report of which assets the game uses and which it doesn't.
// Menu: Memetica > Check > Asset Usage Report
// Result: AssetUsageReport.csv in the same folder as this script.
// It moves and deletes nothing, it only reads.
//
// States:
//   In use (scene)        → used by a scene in the Build Settings (Menu, Game, OriginalGame), directly or not
//   In use (Resources)    → in Resources or used by something there (loaded by code)
//   Old scenes only       → used only by scenes NOT in the Build Settings (the Unused ones)
//   Script / Editor       → scripts: check by hand (code can use them without a scene)
//   Unused                → nothing uses it

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AssetUsageReport
{
    [MenuItem("Memetica/Check/Asset Usage Report", false, 401)]
    public static void Generate()
    {
        // 1. The game's scenes (Build Settings) and old scenes (the rest)
        string[] buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string[] oldScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                                          .Select(AssetDatabase.GUIDToAssetPath)
                                          .Where(p => !buildScenes.Contains(p)).ToArray();

        // 2. Everything inside a Resources folder is loaded by code: it counts as used
        List<string> allAssets = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p))
            .ToList();
        string[] resources = allAssets.Where(p => p.Contains("/Resources/")).ToArray();

        // 3. Dependencies (recursive)
        HashSet<string> usedByBuild = new HashSet<string>(AssetDatabase.GetDependencies(buildScenes, true));
        HashSet<string> usedByResources = new HashSet<string>(AssetDatabase.GetDependencies(resources, true));
        HashSet<string> usedByOld = new HashSet<string>(AssetDatabase.GetDependencies(oldScenes, true));

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("State;Top folder;Path;Size KB");
        Dictionary<string, int> counts = new Dictionary<string, int>();
        long unusedBytes = 0;

        foreach (string path in allAssets.OrderBy(p => p))
        {
            string state;
            if (path.Contains("/Editor/")) state = "Editor";
            else if (usedByBuild.Contains(path)) state = "In use (scene)";
            else if (usedByResources.Contains(path)) state = "In use (Resources)";
            else if (path.EndsWith(".cs")) state = "Script";
            else if (usedByOld.Contains(path)) state = "Old scenes only";
            else state = "Unused";

            long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (state == "Unused" || state == "Old scenes only") unusedBytes += bytes;
            counts[state] = counts.TryGetValue(state, out int c) ? c + 1 : 1;

            string top = path.Split('/').Length > 1 ? path.Split('/')[1] : "";
            sb.AppendLine($"{state};{top};{path};{bytes / 1024}");
        }

        // The CSV is saved next to this script, wherever it is
        string scriptPath = AssetDatabase.FindAssets("AssetUsageReport t:Script")
                                         .Select(AssetDatabase.GUIDToAssetPath)
                                         .First(p => p.EndsWith("AssetUsageReport.cs"));
        string outPath = Path.Combine(Path.GetDirectoryName(scriptPath), "AssetUsageReport.csv");
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        AssetDatabase.ImportAsset(outPath);

        Debug.Log("[Assets] Report ready: " + string.Join(", ", counts.Select(kv => kv.Value + " " + kv.Key)) +
                  $". Unused + old scenes only: {unusedBytes / (1024 * 1024)} MB. File: {outPath}");
    }
}
