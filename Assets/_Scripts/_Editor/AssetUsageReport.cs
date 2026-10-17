// Herramienta de editor: genera un reporte de qué assets usa el juego y cuáles no.
// Menú: Memetica > Reporte de assets en uso
// Resultado: AssetUsageReport.csv en la misma carpeta que este script.
// No mueve ni borra nada, solo lee.
//
// Estados:
//   En uso (escena)       → lo usa una escena del Build Settings (Menu, Game), directa o indirectamente
//   En uso (Resources)    → está en Resources o lo usa algo de ahí (se carga por código)
//   Solo escenas viejas   → solo lo usan escenas que NO están en el Build Settings (las de Unused)
//   Script / Editor       → scripts: revisar a mano (pueden usarse por código sin estar en una escena)
//   Sin usar              → nadie lo usa

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AssetUsageReport
{
    [MenuItem("Memetica/Reporte de assets en uso")]
    public static void Generate()
    {
        // 1. Escenas del juego (Build Settings) y escenas viejas (el resto)
        string[] buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string[] oldScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                                          .Select(AssetDatabase.GUIDToAssetPath)
                                          .Where(p => !buildScenes.Contains(p)).ToArray();

        // 2. Todo lo que está dentro de una carpeta Resources se carga por código: cuenta como usado
        List<string> allAssets = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p))
            .ToList();
        string[] resources = allAssets.Where(p => p.Contains("/Resources/")).ToArray();

        // 3. Dependencias (recursivas)
        HashSet<string> usedByBuild = new HashSet<string>(AssetDatabase.GetDependencies(buildScenes, true));
        HashSet<string> usedByResources = new HashSet<string>(AssetDatabase.GetDependencies(resources, true));
        HashSet<string> usedByOld = new HashSet<string>(AssetDatabase.GetDependencies(oldScenes, true));

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Estado;Carpeta principal;Ruta;Tamaño KB");
        Dictionary<string, int> counts = new Dictionary<string, int>();
        long unusedBytes = 0;

        foreach (string path in allAssets.OrderBy(p => p))
        {
            string state;
            if (path.Contains("/Editor/")) state = "Editor";
            else if (usedByBuild.Contains(path)) state = "En uso (escena)";
            else if (usedByResources.Contains(path)) state = "En uso (Resources)";
            else if (path.EndsWith(".cs")) state = "Script";
            else if (usedByOld.Contains(path)) state = "Solo escenas viejas";
            else state = "Sin usar";

            long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (state == "Sin usar" || state == "Solo escenas viejas") unusedBytes += bytes;
            counts[state] = counts.TryGetValue(state, out int c) ? c + 1 : 1;

            string top = path.Split('/').Length > 1 ? path.Split('/')[1] : "";
            sb.AppendLine($"{state};{top};{path};{bytes / 1024}");
        }

        // El CSV se guarda al lado de este script, esté donde esté
        string scriptPath = AssetDatabase.FindAssets("AssetUsageReport t:Script")
                                         .Select(AssetDatabase.GUIDToAssetPath)
                                         .First(p => p.EndsWith("AssetUsageReport.cs"));
        string outPath = Path.Combine(Path.GetDirectoryName(scriptPath), "AssetUsageReport.csv");
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        AssetDatabase.ImportAsset(outPath);

        Debug.Log("Reporte de assets listo: " + string.Join(", ", counts.Select(kv => kv.Value + " " + kv.Key)) +
                  $". Sin usar + solo escenas viejas: {unusedBytes / (1024 * 1024)} MB. Archivo: {outPath}");
    }
}
