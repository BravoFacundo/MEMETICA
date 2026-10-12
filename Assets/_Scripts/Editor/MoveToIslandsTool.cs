// Herramienta de editor: mete cada pivote de respawn y cada sonido de ambiente dentro de su isla.
// Menú: Memetica > Mover pivotes y ambiente a sus islas
// - PivotAreaX  → hijo de AreaX
// - AreaXAudio  → hijo de AreaX
// Las islas que suben (tienen el script Island) arrancan abajo: la herramienta calcula su posición
// final (a la altura Top Y del LevelManager) para que el pivote y el sonido queden bien cuando la isla suba.
// Lo que no tiene isla clara (por ejemplo PivotArea1-AreaA) no se toca y se avisa en la consola.
// Se deshace todo junto con Ctrl+Z.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MoveToIslandsTool
{
    [MenuItem("Memetica/Mover pivotes y ambiente a sus islas")]
    static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Islas] Salí del Play antes de usar la herramienta."); return; }

        List<Transform> all = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

        LevelManager level = Object.FindObjectOfType<LevelManager>();
        if (level == null) { Debug.LogError("[Islas] No encuentro el LevelManager en la escena."); return; }

        // Islas = objetos que tienen un pivote llamado "Pivot" + su nombre
        HashSet<string> names = new HashSet<string>(all.Select(t => t.name));
        Dictionary<string, Transform> islands = new Dictionary<string, Transform>();
        foreach (Transform t in all)
            if (names.Contains("Pivot" + t.name) && !islands.ContainsKey(t.name)) islands[t.name] = t;

        Undo.SetCurrentGroupName("Mover pivotes y ambiente a sus islas");
        int undoGroup = Undo.GetCurrentGroup();
        int moved = 0;

        foreach (KeyValuePair<string, Transform> kv in islands)
        {
            Transform island = kv.Value;
            Vector3 finalPos = island.GetComponent<Island>() != null
                ? new Vector3(island.position.x, level.topY, island.position.z)   // sube hasta Top Y
                : island.position;                                                         // no se mueve

            foreach (string childName in new[] { "Pivot" + kv.Key, kv.Key + "Audio" })
            {
                Transform child = all.FirstOrDefault(t => t.name == childName);
                if (child == null || child.parent == island) continue;

                // ¿Fue ubicado respecto de la isla arriba (lo normal) o de donde está ahora?
                bool placedForFinal = Mathf.Abs(child.position.y - finalPos.y) <= Mathf.Abs(child.position.y - island.position.y);
                Vector3 reference = placedForFinal ? finalPos : island.position;
                Vector3 local = Matrix4x4.TRS(reference, island.rotation, island.lossyScale).inverse.MultiplyPoint3x4(child.position);
                Quaternion worldRotation = child.rotation;

                Undo.SetTransformParent(child, island, "Mover " + childName);
                Undo.RecordObject(child, "Ubicar " + childName);
                child.localPosition = local;
                child.rotation = worldRotation;
                moved++;

                Debug.Log("[Islas] " + childName + " → dentro de " + kv.Key +
                          (placedForFinal ? " (calculado con la isla arriba)" : " (calculado con la isla donde está)"), child);
            }
        }

        // Avisar lo que quedó afuera
        foreach (Transform container in all.Where(t => t.name == "Respawn Pivots" || t.name == "Ambience" || t.name == "Audio Manager"))
            foreach (Transform leftover in container)
                Debug.LogWarning("[Islas] No se movió '" + leftover.name + "': no encontré su isla. Revisalo a mano.", leftover);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Islas] Listo: " + moved + " objetos movidos. Ctrl+Z deshace todo.");
    }
}
