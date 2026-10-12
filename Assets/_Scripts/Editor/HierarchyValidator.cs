// Herramienta de editor: revisa que la jerarquía siga cumpliendo lo que el código espera.
// Menú: Memetica > Validar jerarquía
// Usala después de mover o renombrar objetos. No modifica nada, solo avisa en la consola.
// Hacé clic en cada aviso de la consola para seleccionar el objeto con problemas.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HierarchyValidator
{
    // Objetos que el código busca con GameObject.Find("...") → deben existir, ser únicos y estar activos
    static readonly string[] RequiredNames =
    {
        // Jugador y UI
        "Player", "Main Camera", "GrabPivot",
        // Sistemas y pivotes
        "PivotPuenteZ",
        // Nivel
        "AreaTuto", "Area1", "Area2", "Area3", "Area4", "Area5", "Area6", "AreaA", "AreaB", "AreaC",
        "Puente1", "Puente2", "Puente3", "Puente4", "Puente5", "Puente6", "PuenteA", "PuenteC", "PuenteZ",
        "ElevatorZ", "ElevatorX", "ElevatorLocks", "PedestalElevador",
        "PivotAreaTuto", "PivotArea1", "PivotArea2", "PivotArea3", "PivotArea4", "PivotArea5", "PivotArea6",
        "PivotAreaA", "PivotAreaB", "PivotAreaC",
        // Orbes (los pedestales leen su nombre)
        "1Aa", "1Ba", "3Aa", "4Aa",
    };

    static readonly string[] RequiredTags = { "Isla", "Puente", "Pedestal", "Meme" };

    static int errors, warnings;

    [MenuItem("Memetica/Validar jerarquía")]
    public static void Validate()
    {
        errors = 0; warnings = 0;
        var all = AllObjectsInScene();

        // 1. Nombres buscados por código
        foreach (var name in RequiredNames)
        {
            var found = all.Where(g => g.name == name).ToList();
            if (found.Count == 0) Error($"No existe '{name}' (el código lo busca por nombre).");
            else
            {
                if (found.Count > 1) Error($"Hay {found.Count} objetos llamados '{name}'. GameObject.Find agarra uno cualquiera.", found[1]);
                if (!found[0].activeInHierarchy) Error($"'{name}' está desactivado (o su padre). GameObject.Find no encuentra objetos inactivos.", found[0]);
            }
        }

        // 2. Pedestales: hijos directos que busca el código + tener padre (busca "Pintura" como hermano)
        foreach (var ped in Object.FindObjectsOfType<Pedestal>(true))
        {
            foreach (var child in new[] { "PedTrigger", "PedPivot", "PedAudio", "PedParticles" })
                if (ped.transform.Find(child) == null) Error($"El pedestal '{ped.name}' perdió su hijo directo '{child}'.", ped);
            if (ped.transform.parent == null) Error($"El pedestal '{ped.name}' no tiene padre (busca 'Pintura' como hermano).", ped);
            if (ped.chargeClip == null || ped.correctClip == null || ped.incorrectClip == null || ped.snapClip == null)
                Warn($"El pedestal '{ped.name}' tiene sonidos sin asignar.", ped);
        }

        // 3. Orbes: hijos "Meme" y "Audio"
        foreach (var orb in Object.FindObjectsOfType<OrbMemes>(true))
            foreach (var child in new[] { "Meme", "Audio" })
                if (orb.transform.Find(child) == null) Error($"El orbe '{orb.name}' perdió su hijo directo '{child}'.", orb);

        // 4. Plataformas que caen: pivotes como hermanos
        foreach (var plat in Object.FindObjectsOfType<Platform>(true))
        {
            var parent = plat.transform.parent;
            if (parent == null || parent.Find("PlatformPivotSuperior") == null || parent.Find("PlatformPivotInferior") == null)
                Error($"La plataforma '{plat.name}' perdió sus pivotes hermanos (PlatformPivotSuperior/Inferior).", plat);
        }

        // 5. Player: hijo "GroundCheck"
        foreach (var move in Object.FindObjectsOfType<PlayerMove>(true))
            if (move.transform.Find("GroundCheck") == null) Error($"'{move.name}' perdió su hijo directo 'GroundCheck'.", move);

        // 6. Tags que usa el código
        foreach (var tag in RequiredTags)
            if (!all.Any(g => g.CompareTag(tag))) Error($"Ningún objeto tiene el tag '{tag}'.");

        // 7. Grupos de organización: objetos vacíos, sin tag, que contienen islas, puentes o ascensores.
        //    Son los únicos donde la posición del padre importa (esos objetos se mueven con posiciones de mundo).
        //    Las islas en sí (AreaTuto, Area1...) tienen tag "Isla" y quedan excluidas: NO se resetean.
        foreach (var go in all)
        {
            bool isEmpty = go.GetComponents<Component>().Length == 1 && go.transform.childCount > 0;
            bool untagged = go.CompareTag("Untagged");
            bool containsMovers = go.GetComponentsInChildren<Island>(true).Length > 0
                               || go.GetComponentsInChildren<Bridge>(true).Length > 0
                               || go.GetComponentsInChildren<Elevator>(true).Length > 0;
            bool isGroup = isEmpty && untagged && containsMovers && !RequiredNames.Contains(go.name);
            if (!isGroup) continue;
            var t = go.transform;
            if (t.localPosition != Vector3.zero || t.localRotation != Quaternion.identity || t.localScale != Vector3.one)
                Warn($"El grupo '{go.name}' no está en posición 0 / rotación 0 / escala 1. Sus hijos pueden quedar desplazados (Reset en el Transform).", go);
            if (!go.activeSelf)
                Error($"El grupo '{go.name}' está desactivado: todo lo de adentro deja de existir para GameObject.Find.", go);
        }

        // 8. Scripts faltantes ("The referenced script on this Behaviour is missing")
        foreach (var go in all)
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing > 0) Warn($"'{go.name}' tiene {missing} script(s) faltante(s) (Missing Script).", go);
        }

        string result = errors == 0 && warnings == 0
            ? "✔ Jerarquía OK: todo lo que el código necesita está en su lugar."
            : $"Validación terminada: {errors} errores, {warnings} avisos.";
        if (errors > 0) Debug.LogError(result); else if (warnings > 0) Debug.LogWarning(result); else Debug.Log(result);
    }

    static List<GameObject> AllObjectsInScene()
    {
        var list = new List<GameObject>();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
        return list;
    }

    static void Error(string msg, Object ctx = null) { errors++; Debug.LogError("[Jerarquía] " + msg, ctx); }
    static void Warn(string msg, Object ctx = null) { warnings++; Debug.LogWarning("[Jerarquía] " + msg, ctx); }
}
