// Herramienta de editor: genera un reporte de qué assets usa el juego y cuáles no.
// Menú: Memetica > Reporte de assets en uso
// Resultado: AssetUsageReport.csv en la misma carpeta que este script.
// No mueve ni borra nada, solo lee.

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
        // 1. Escenas: las del Build Settings + todas las escenas del proyecto (Menu y Tutorial incluidas)
        var buildScenes = EditorBuildSettings.scenes.Select(s => s.path);
        var allScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                                     .Select(AssetDatabase.GUIDToAssetPath);
        var scenes = buildScenes.Concat(allScenes).Distinct().ToArray();

        // 2. Todo lo que está dentro de una carpeta Resources se carga por código: cuenta como usado
        var allAssets = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p))
            .ToList();
        var resources = allAssets.Where(p => p.Contains("/Resources/")).ToArray();

        // 3. Dependencias (recursivas) de escenas y de Resources
        var usedByScenes = new HashSet<string>(AssetDatabase.GetDependencies(scenes, true));
        var usedByResources = new HashSet<string>(AssetDatabase.GetDependencies(resources, true));

        var sb = new StringBuilder();
        sb.AppendLine("Estado;Carpeta principal;Ruta;Tamaño KB");
        int used = 0, unused = 0;
        long unusedBytes = 0;

        foreach (var path in allAssets.OrderBy(p => p))
        {
            string state;
            if (path.Contains("/Editor/")) state = "Editor";
            else if (usedByScenes.Contains(path)) state = "En uso (escena)";
            else if (usedByResources.Contains(path)) state = "En uso (Resources)";
            else if (path.EndsWith(".cs")) state = "Script sin referencia";
            else state = "Sin usar";

            long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (state == "Sin usar") { unused++; unusedBytes += bytes; } else used++;

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

        Debug.Log($"Reporte de assets listo: {used} en uso, {unused} sin usar ({unusedBytes / (1024 * 1024)} MB). Archivo: {outPath}");
        EditorUtility.RevealInFinder(outPath);
    }
}
