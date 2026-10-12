// Herramienta de editor (usar UNA vez): vacía la carpeta Resources.
// Todo lo que está en Resources entra en el build aunque nadie lo use, así que la idea es que quede vacía.
//
// Paso 1 — Memetica > Resources > 1. Asignar imágenes a los orbes
//   Llena la lista "Materials" de cada orbe en el Inspector con lo que antes cargaba por código.
//   Después guardá la escena (Ctrl+S).
//
// Paso 2 — Memetica > Resources > 2. Vaciar Resources
//   Lo que usan las escenas del juego (Build Settings) → se mueve a Assets/Art/ con la misma subcarpeta
//     (Resources/Memes/Reli/0.png → Art/Memes/Reli/0.png). Las referencias no se pierden.
//   Lo que nadie usa → a la papelera del sistema (se puede recuperar, y además está en git).
//   Antes de hacer nada muestra el plan en la consola y pide confirmación.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CleanResourcesTool
{
    const string ResourcesRoot = "Assets/Resources";
    const string ArtRoot = "Assets/Art";

    // Lo que hacía OrbMemes.Start(): nombre del orbe → carpeta y cantidad de imágenes
    static readonly Dictionary<string, (string folder, int count)> OrbImages = new Dictionary<string, (string, int)>
    {
        { "1Aa", ("Memes/Reli",    6) },
        { "1Ba", ("Memes/Consp",   5) },
        { "3Aa", ("Memes/Memes",   4) },
        { "4Aa", ("Memes/Fashion", 4) },
    };

    // ---------- Paso 1 ----------
    [MenuItem("Memetica/Resources/1. Asignar imágenes a los orbes")]
    static void AssignOrbImages()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Resources] Salí del Play."); return; }

        foreach (OrbMemes orb in Object.FindObjectsOfType<OrbMemes>(true))
        {
            if (!OrbImages.TryGetValue(orb.name, out var info)) { Debug.LogWarning("[Resources] No sé qué imágenes lleva el orbe '" + orb.name + "'.", orb); continue; }

            var mats = new List<Material>();
            for (int i = 0; i < info.count; i++)
            {
                string path = ResourcesRoot + "/" + info.folder + "/Materials/" + i + ".mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { Debug.LogError("[Resources] Falta " + path, orb); return; }
                mats.Add(m);
            }

            Undo.RecordObject(orb, "Asignar imágenes");
            orb.materials = mats;
            PrefabUtility.RecordPrefabInstancePropertyModifications(orb);
            Debug.Log("[Resources] " + orb.name + " ← " + info.count + " imágenes de " + info.folder, orb);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Resources] Listo. Guardá la escena (Ctrl+S) y corré el paso 2.");
    }

    // ---------- Paso 2 ----------
    [MenuItem("Memetica/Resources/2. Vaciar Resources")]
    static void EmptyResources()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Resources] Salí del Play."); return; }
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        {
            EditorUtility.DisplayDialog("Vaciar Resources", "Guardá la escena primero (Ctrl+S): el uso se calcula sobre los archivos guardados.", "OK");
            return;
        }

        // Qué usan las escenas del juego (directa o indirectamente)
        string[] buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var used = new HashSet<string>(AssetDatabase.GetDependencies(buildScenes, true));

        string[] assets = AssetDatabase.FindAssets("", new[] { ResourcesRoot })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct()
            .Where(p => !AssetDatabase.IsValidFolder(p)).OrderBy(p => p).ToArray();

        var toMove = assets.Where(used.Contains).ToList();
        var toDelete = assets.Where(p => !used.Contains(p)).ToList();
        long deleteBytes = toDelete.Sum(p => File.Exists(p) ? new FileInfo(p).Length : 0);

        foreach (string p in toMove) Debug.Log("[Resources] MOVER  " + p + "  →  " + Destination(p));
        foreach (string p in toDelete) Debug.Log("[Resources] BORRAR " + p);

        if (!EditorUtility.DisplayDialog("Vaciar Resources",
            $"Mover a Art: {toMove.Count} archivos\nBorrar (papelera): {toDelete.Count} archivos ({deleteBytes / (1024f * 1024f):0.0} MB)\n\nEl detalle está en la consola.",
            "Hacerlo", "Cancelar"))
            return;

        int moved = 0, deleted = 0;
        foreach (string p in toMove)
        {
            string dest = Destination(p);
            EnsureFolder(Path.GetDirectoryName(dest).Replace('\\', '/'));
            if (File.Exists(dest)) { Debug.LogWarning("[Resources] Ya existe " + dest + ", no lo muevo: " + p); continue; }
            string error = AssetDatabase.MoveAsset(p, dest);
            if (string.IsNullOrEmpty(error)) moved++;
            else Debug.LogError("[Resources] No pude mover " + p + ": " + error);
        }
        foreach (string p in toDelete)
            if (AssetDatabase.MoveAssetToTrash(p)) deleted++;

        DeleteEmptyFolders(ResourcesRoot);
        AssetDatabase.Refresh();
        Debug.Log($"[Resources] Listo: {moved} movidos a Art, {deleted} a la papelera. Probá en Play que los orbes cambien de imagen.");
    }

    // Resources/Memes/Reli/0.png → Art/Memes/Reli/0.png
    static string Destination(string path) => ArtRoot + path.Substring(ResourcesRoot.Length);

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    static void DeleteEmptyFolders(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;
        foreach (string sub in AssetDatabase.GetSubFolders(folder)) DeleteEmptyFolders(sub);
        if (AssetDatabase.FindAssets("", new[] { folder }).Length == 0) AssetDatabase.DeleteAsset(folder);
    }
}
