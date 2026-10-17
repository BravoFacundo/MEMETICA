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
        // Jugador
        "Player", "Main Camera", "GrabPivot",
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

        // 1b. LevelManager: referencias y convención de nombres
        LevelManager level = Object.FindObjectOfType<LevelManager>(true);
        if (level == null) Error("No hay LevelManager en la escena.");
        else
        {
            if (level.startRespawnPoint == null) Error("LevelManager: falta asignar Start Respawn Point.", level);

            foreach (var isla in Object.FindObjectsOfType<Island>(true))
            {
                if (!isla.name.StartsWith(level.islandPrefix)) Warn($"La isla '{isla.name}' no sigue la convención '{level.islandPrefix}ID'.", isla);
                else if (level.RespawnPointOf(isla.gameObject) == null) Error($"Island '{isla.name}' has no '{level.respawnPointName}' inside it.", isla);
            }
            foreach (var puente in Object.FindObjectsOfType<Bridge>(true))
                if (!puente.name.StartsWith(level.bridgePrefix)) Warn($"El puente '{puente.name}' no sigue la convención '{level.bridgePrefix}ID'.", puente);
            foreach (var elevator in Object.FindObjectsOfType<Elevator>(true))
            {
                if (!elevator.name.StartsWith(level.elevatorPrefix)) Warn($"Elevator '{elevator.name}' does not follow the '{level.elevatorPrefix}ID' convention.", elevator);
                if (elevator.dockPoint == null) Error($"Elevator '{elevator.name}' has no Dock Point.", elevator);
                else if (elevator.dockPoint.GetComponentInParent<Island>() == null) Warn($"Elevator '{elevator.name}': its Dock Point is not inside an island (no respawn or docking on arrival).", elevator);
            }
        }

        // 2. Pedestales: partes asignadas, trigger y audio en la raíz, sonidos. Pinturas con su pedestal.
        foreach (var ped in Object.FindObjectsOfType<Pedestal>(true))
        {
            if (ped.transform.Find("PedTrigger") != null) Error($"El pedestal '{ped.name}' tiene la estructura vieja (Memetica > Migrar pedestales).", ped);
            if (ped.socket == null) Error($"El pedestal '{ped.name}' no tiene Socket asignado.", ped);
            if (ped.visualKeyed == null || ped.visualKeyless == null) Warn($"El pedestal '{ped.name}' no tiene asignados Visual_Keyed / Visual_Keyless.", ped);
            if (!ped.TryGetComponent(out SphereCollider col) || !col.isTrigger) Error($"El pedestal '{ped.name}' no tiene SphereCollider trigger en la raíz.", ped);
            if (!ped.TryGetComponent(out AudioSource _)) Error($"El pedestal '{ped.name}' no tiene AudioSource en la raíz.", ped);
            if (ped.chargeClip == null || ped.correctClip == null || ped.incorrectClip == null || ped.snapClip == null)
                Warn($"El pedestal '{ped.name}' tiene sonidos sin asignar.", ped);
        }
        var allPedestals = Object.FindObjectsOfType<Pedestal>(true);
        foreach (var ped in allPedestals)
        {
            if (!ped.KeylessPedestal && !ped.IsGrouped && ped.cluePainting == null) Warn($"Keyed pedestal '{ped.name}' has no Clue Painting (the easel with its clue).", ped);
            if (ped.IsGrouped && allPedestals.Count(p => p.groupID == ped.groupID) < 2) Warn($"Pedestal '{ped.name}' is the only one in group '{ped.groupID}': it will complete alone.", ped);
        }

        // 2b. Pedestal ↔ orb names and controlled IDs (renaming orbs, islands or bridges breaks these)
        if (level != null)
        {
            var memes = Object.FindObjectsOfType<Meme>(true);
            var pedestals = Object.FindObjectsOfType<Pedestal>(true);
            var names = new HashSet<string>(all.Select(g => LevelManager.NormalizeName(g.name)));

            foreach (var ped in pedestals)
            {
                if (!ped.KeylessPedestal)
                {
                    if (string.IsNullOrEmpty(ped.KeyID)) Error($"Keyed pedestal '{ped.name}' has no Key ID.", ped);
                    else if (!memes.Any(m => m.ID.StartsWith(ped.KeyID))) Error($"Pedestal '{ped.name}' expects a meme whose ID starts with '{ped.KeyID}' (name '{Meme.NamePrefix}{ped.KeyID}...'), and there is none.", ped);
                }
                foreach (string id in ped.ControlledIslands)
                    if (LevelManager.NormalizeName(id) != "0" && !names.Contains(LevelManager.NormalizeName(level.islandPrefix + id)) && !names.Contains(LevelManager.NormalizeName(level.elevatorPrefix + id)))
                        Error($"Pedestal '{ped.name}' raises '{id}', but there is no '{level.islandPrefix}{id}' or '{level.elevatorPrefix}{id}'.", ped);
                foreach (string id in ped.ControlledBridges)
                    if (!names.Contains(LevelManager.NormalizeName(level.bridgePrefix + id)))
                        Error($"Pedestal '{ped.name}' raises bridge '{id}', but there is no '{level.bridgePrefix}{id}'.", ped);
            }

            var keyIDs = pedestals.Where(p => !p.KeylessPedestal && !string.IsNullOrEmpty(p.KeyID)).Select(p => p.KeyID).ToList();
            foreach (var meme in memes)
            {
                if (!meme.name.StartsWith(Meme.NamePrefix)) Warn($"Meme '{meme.name}' should be named '{Meme.NamePrefix}<ID>' (e.g. {Meme.NamePrefix}3Aa).", meme);
                if (!keyIDs.Any(k => meme.ID.StartsWith(k)))
                    Warn($"Meme '{meme.name}' matches no pedestal Key ID: it only works on keyless (black) pedestals (fine if that's intended).", meme);
            }
            foreach (var go in all.Where(g => g.CompareTag("Meme") && g.GetComponentInParent<Meme>(true) == null))
                Error($"'{go.name}' is tagged Meme but has no Meme component (Memetica > Migrar a Meme).", go);
        }

        // 3. Orbes: hijos "Meme" y "Audio"
        foreach (var orb in Object.FindObjectsOfType<MemeOrb>(true))
            foreach (var child in new[] { "Meme", "Audio" })
                if (orb.transform.Find(child) == null) Error($"El orbe '{orb.name}' perdió su hijo directo '{child}'.", orb);

        // 4. Falling platforms: pivots anywhere inside their bridge
        foreach (var plat in Object.FindObjectsOfType<Platform>(true))
        {
            var bridge = plat.GetComponentInParent<Bridge>(true);
            var root = bridge != null ? bridge.transform : plat.transform.parent;
            var inside = root != null ? root.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList() : new List<string>();
            if (!inside.Contains("PlatformPivotSuperior") || !inside.Contains("PlatformPivotInferior"))
                Error($"Platform '{plat.name}' lost its pivots (PlatformPivotSuperior/Inferior) inside its bridge.", plat);
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
