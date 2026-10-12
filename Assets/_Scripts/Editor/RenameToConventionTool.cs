// Herramienta de editor: renombra el nivel a la convención en inglés Tipo_ID y conecta las referencias.
// Menú: Memetica > Renombrar nivel a convención (Tipo_ID)
//   Area3        → Island_3         (y AreaTuto → Island_Tuto)
//   PivotArea3   → RespawnPoint     (hijo de su isla)
//   Area3Audio   → WindAudio        (hijo de su isla)
//   Puente3      → Bridge_3
//   ElevatorZ    → Elevator_Z
//   ElevatorLocks    → Elevator_Z_Locks    (y se asigna en el LevelManager)
//   PedestalElevador → Pedestal_Elevator   (y se asigna en el LevelManager)
//   PivotPuenteZ     → Bridge_Z_Top        (y se asigna como Top Override del Bridge_Z)
// Al final asigna Start Respawn Point del LevelManager. Ctrl+Z deshace todo.
// Usa los prefijos que tenga configurados el LevelManager.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RenameToConventionTool
{
    [MenuItem("Memetica/Renombrar nivel a convención (Tipo_ID)")]
    static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Convención] Salí del Play antes de usar la herramienta."); return; }

        LevelManager level = Object.FindObjectOfType<LevelManager>(true);
        if (level == null) { Debug.LogError("[Convención] No encuentro el LevelManager en la escena."); return; }

        List<Transform> all = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

        Undo.SetCurrentGroupName("Renombrar nivel a convención");
        int undoGroup = Undo.GetCurrentGroup();
        int renamed = 0;

        // Islas, puentes y ascensores
        foreach (Transform t in all)
        {
            string newName = null;
            Match m;
            if ((m = Regex.Match(t.name, @"^Area([0-9]+|[A-Z]|Tuto)$")).Success) newName = level.islandPrefix + m.Groups[1].Value;
            else if ((m = Regex.Match(t.name, @"^Puente([0-9]+|[A-Z])$")).Success) newName = level.bridgePrefix + m.Groups[1].Value;
            else if ((m = Regex.Match(t.name, @"^Elevator([A-Z])$")).Success) newName = level.elevatorPrefix + m.Groups[1].Value;
            if (newName != null) renamed += Rename(t, newName);
        }

        // Hijos de cada isla: pivote y viento
        foreach (Transform t in all)
        {
            if (t.parent == null || !t.parent.name.StartsWith(level.islandPrefix)) continue;
            string id = t.parent.name.Substring(level.islandPrefix.Length);
            if (t.name == "PivotArea" + id) renamed += Rename(t, level.respawnPointName);
            else if (t.name == "Area" + id + "Audio") renamed += Rename(t, "WindAudio");
        }

        // Casos especiales + referencias
        Transform locks = all.FirstOrDefault(t => t.name == "ElevatorLocks");
        Transform pedestal = all.FirstOrDefault(t => t.name == "PedestalElevador");
        Transform bridgeTop = all.FirstOrDefault(t => t.name == "PivotPuenteZ");
        if (locks != null) renamed += Rename(locks, level.elevatorPrefix + "Z_Locks");
        if (pedestal != null) renamed += Rename(pedestal, "Pedestal_Elevator");
        if (bridgeTop != null) renamed += Rename(bridgeTop, level.bridgePrefix + "Z_Top");

        SerializedObject so = new SerializedObject(level);
        Transform tuto = all.FirstOrDefault(t => t.name == level.islandPrefix + "Tuto");
        Transform startPoint = tuto != null ? tuto.Find(level.respawnPointName) : null;
        if (startPoint != null) so.FindProperty("startRespawnPoint").objectReferenceValue = startPoint;
        if (locks != null) so.FindProperty("elevatorZLocks").objectReferenceValue = locks.gameObject;
        if (pedestal != null) so.FindProperty("elevatorZPedestal").objectReferenceValue = pedestal.gameObject;
        so.ApplyModifiedProperties();

        Transform bridgeZ = all.FirstOrDefault(t => t.name == level.bridgePrefix + "Z");
        if (bridgeZ != null && bridgeTop != null && bridgeZ.TryGetComponent(out Bridge bridge))
        {
            SerializedObject bso = new SerializedObject(bridge);
            bso.FindProperty("topOverride").objectReferenceValue = bridgeTop;
            bso.ApplyModifiedProperties();
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Convención] Listo: " + renamed + " objetos renombrados. Corré Memetica > Validar jerarquía. Ctrl+Z deshace todo.");
    }

    static int Rename(Transform t, string newName)
    {
        if (t.name == newName) return 0;
        Undo.RecordObject(t.gameObject, "Renombrar " + t.name);
        Debug.Log("[Convención] " + t.name + " → " + newName, t);
        t.gameObject.name = newName;
        return 1;
    }
}
