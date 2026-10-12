// Herramienta de editor: ordena todo lo del "look" del juego dentro de _Scenes/Rendering.
// Menú: Memetica > Rendering > Ordenar carpeta Rendering
//   Shaders/     todo lo que está en Art/Shaders
//   Vegetation/  materiales con shader Memetica/Wind Sway
//   Particles/   materiales de partículas (hojitas, ráfagas, brillo del pedestal)
//   Sky/         domo, nubes y materiales con shader Memetica/Cloud
//   (raíz)       Sky_Ambient y el perfil de post procesado quedan donde están
// Muestra el plan en la consola y pide confirmación. Mueve con AssetDatabase: no se pierden referencias.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RenderingOrganizer
{
    const string Root = "Assets/_Scenes/Rendering";

    static readonly string[] ParticleMaterials = { "LeavesGreen", "Wind", "WindAlpha", "GlowingParticleMaterial" };
    static readonly string[] SkyMaterials = { "rpgpp_lt_sky_a", "rpgpp_lt_cloud_a" };

    [MenuItem("Memetica/Rendering/Ordenar carpeta Rendering")]
    static void Organize()
    {
        var plan = new List<(string from, string toFolder)>();

        // Shaders
        foreach (string p in AssetsIn("Assets/Art/Shaders")) plan.Add((p, Root + "/Shaders"));

        // Materiales del proyecto (sin paquetes de terceros)
        foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => !p.StartsWith("Assets/ThirdParty")))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            string shader = m.shader != null ? m.shader.name : "";

            if (shader == "Memetica/Wind Sway") plan.Add((path, Root + "/Vegetation"));
            else if (shader == "Memetica/Cloud" || SkyMaterials.Contains(name)) plan.Add((path, Root + "/Sky"));
            else if (ParticleMaterials.Contains(name)) plan.Add((path, Root + "/Particles"));
        }

        // Saco lo que ya está en su lugar
        plan = plan.Where(x => Path.GetDirectoryName(x.from).Replace('\\', '/') != x.toFolder).ToList();
        if (plan.Count == 0) { Debug.Log("[Rendering] Ya está todo ordenado."); return; }

        foreach (var x in plan) Debug.Log("[Rendering] MOVER " + x.from + "  →  " + x.toFolder);
        if (!EditorUtility.DisplayDialog("Ordenar Rendering",
            $"Mover {plan.Count} archivos a {Root}.\nEl detalle está en la consola.", "Hacerlo", "Cancelar"))
            return;

        int moved = 0;
        foreach (var x in plan)
        {
            EnsureFolder(x.toFolder);
            string dest = x.toFolder + "/" + Path.GetFileName(x.from);
            if (File.Exists(dest)) { Debug.LogWarning("[Rendering] Ya existe " + dest + ", no lo muevo."); continue; }
            string error = AssetDatabase.MoveAsset(x.from, dest);
            if (string.IsNullOrEmpty(error)) moved++;
            else Debug.LogError("[Rendering] No pude mover " + x.from + ": " + error);
        }

        // Si Art/Shaders quedó vacía, la borro
        if (AssetDatabase.IsValidFolder("Assets/Art/Shaders") && AssetsIn("Assets/Art/Shaders").Length == 0)
            AssetDatabase.DeleteAsset("Assets/Art/Shaders");

        AssetDatabase.SaveAssets();
        Debug.Log($"[Rendering] Listo: {moved} archivos movidos.");
    }

    static string[] AssetsIn(string folder) =>
        AssetDatabase.IsValidFolder(folder)
            ? AssetDatabase.FindAssets("", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p)).Distinct().ToArray()
            : new string[0];

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
