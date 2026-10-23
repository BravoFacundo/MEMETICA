// Editor tool: checks that the hierarchy still matches what the code expects.
// Menu: Memetica > Validar jerarquía
// Use it after moving or renaming things. It changes nothing, it only reports in the console.
// Click each message in the console to select the object with the problem.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HierarchyValidator
{
    // Objects the code finds by name (GameObject.Find) → must exist, be unique and active
    static readonly string[] RequiredNames = { "Player", "Main Camera", "GrabPivot" };

    static readonly string[] RequiredTags = { "Isla", "Puente", "Pedestal", "Meme" };

    static int errors, warnings;

    [MenuItem("Memetica/Validar jerarquía")]
    public static void Validate()
    {
        errors = 0; warnings = 0;
        var all = AllObjectsInScene();

        // 1. Names the code looks for
        foreach (var name in RequiredNames)
        {
            var found = all.Where(g => g.name == name).ToList();
            if (found.Count == 0) Error($"There is no '{name}' (the code finds it by name).");
            else
            {
                if (found.Count > 1) Error($"There are {found.Count} objects named '{name}'. GameObject.Find takes any of them.", found[1]);
                if (!found[0].activeInHierarchy) Error($"'{name}' (or a parent) is off. GameObject.Find doesn't find inactive objects.", found[0]);
            }
        }

        // 1b. LevelManager references, naming convention, islands and bridges
        LevelManager level = Object.FindObjectOfType<LevelManager>(true);
        if (level == null) Error("There is no LevelManager in the scene.");
        else
        {
            if (level.startRespawnPoint == null) Error("LevelManager: Start Respawn Point not assigned.", level);

            foreach (var isla in Object.FindObjectsOfType<Island>(true))
            {
                if (!isla.name.StartsWith(level.islandPrefix)) Warn($"Island '{isla.name}' does not follow the '{level.islandPrefix}ID' convention.", isla);
                else if (level.RespawnPointOf(isla.gameObject) == null) Error($"Island '{isla.name}' has no '{level.respawnPointName}' inside it.", isla);
                if (!isla.CompareTag("Isla")) Error($"Island '{isla.name}' is not tagged 'Isla' (respawn and meme crossings look for it).", isla);

                // Floating: on the island itself (new) or on one direct child (old structure)
                bool floatsItself = isla.GetComponent<FloatMotion>() != null;
                int floatingChildren = isla.transform.Cast<Transform>().Count(c => c.GetComponent<FloatMotion>() != null);
                if (!floatsItself && floatingChildren == 0) Warn($"Island '{isla.name}' doesn't float (no FloatMotion on it or on a direct child).", isla);
                if (floatsItself && floatingChildren > 0) Warn($"Island '{isla.name}' floats twice (FloatMotion on it and on a child).", isla);
            }
            foreach (var puente in Object.FindObjectsOfType<Bridge>(true))
            {
                if (!puente.name.StartsWith(level.bridgePrefix)) Warn($"Bridge '{puente.name}' does not follow the '{level.bridgePrefix}ID' convention.", puente);
                bool bends = puente.TryGetComponent(out BridgeBend _);
                if (puente.appearance == Bridge.Appearance.ExtendFromA && !bends) Error($"Bridge '{puente.name}' uses Extend From A but has no BridgeBend.", puente);
                if (bends && (puente.anchorA == null || puente.anchorB == null)) Error($"Bridge '{puente.name}' has a BridgeBend but is missing an anchor (right click on Bridge > Create Anchors).", puente);
                foreach (Transform anchor in new[] { puente.anchorA, puente.anchorB })
                {
                    if (anchor == null) continue;
                    if (anchor.GetComponentInParent<Island>() == null) Warn($"Bridge '{puente.name}': anchor '{anchor.name}' is not inside an island.", anchor);
                    else if (anchor.GetComponentInParent<FloatMotion>() == null) Warn($"Bridge '{puente.name}': anchor '{anchor.name}' is not inside the floating part of its island (that end won't follow it).", anchor);
                }
            }
            foreach (var elevator in Object.FindObjectsOfType<Elevator>(true))
            {
                if (!elevator.name.StartsWith(level.elevatorPrefix)) Warn($"Elevator '{elevator.name}' does not follow the '{level.elevatorPrefix}ID' convention.", elevator);
                if (elevator.dockPoint == null) Error($"Elevator '{elevator.name}' has no Dock Point.", elevator);
                else if (elevator.dockPoint.GetComponentInParent<Island>() == null) Warn($"Elevator '{elevator.name}': its Dock Point is not inside an island (no respawn or docking on arrival).", elevator);
            }
        }

        // 2. Pedestals: parts assigned, trigger and audio on the root, sounds, inside an island
        foreach (var ped in Object.FindObjectsOfType<Pedestal>(true))
        {
            if (ped.socket == null) Error($"Pedestal '{ped.name}' has no Socket assigned.", ped);
            if (ped.visualKeyed == null || ped.visualKeyless == null) Warn($"Pedestal '{ped.name}' is missing Visual_Keyed / Visual_Keyless.", ped);
            if (!ped.TryGetComponent(out SphereCollider col) || !col.isTrigger) Error($"Pedestal '{ped.name}' has no trigger SphereCollider on its root.", ped);
            if (!ped.TryGetComponent(out AudioSource _)) Error($"Pedestal '{ped.name}' has no AudioSource on its root.", ped);
            if (ped.chargeClip == null || ped.correctClip == null || ped.incorrectClip == null || ped.snapClip == null)
                Warn($"Pedestal '{ped.name}' has sounds not assigned.", ped);
            if (ped.GetComponentInParent<Island>() != null && ped.GetComponentInParent<FloatMotion>() == null)
                Warn($"Pedestal '{ped.name}' is not inside the floating part of its island (it won't float with it).", ped);
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
                Error($"'{go.name}' is tagged Meme but has no Meme component.", go);
        }

        // 3. Orbs: their sticker and their meme
        foreach (var orb in Object.FindObjectsOfType<MemeOrb>(true))
        {
            if (orb.sticker == null) Error($"Orb '{orb.name}' has no Sticker reference.", orb);
            if (orb.data == null && orb.reserveGroup == null) Warn($"Orb '{orb.name}' has no meme (Data or Reserve Group).", orb);
        }

        // 4. Falling platforms: pivots anywhere inside their bridge
        foreach (var plat in Object.FindObjectsOfType<FallingPlatform>(true))
        {
            var bridge = plat.GetComponentInParent<Bridge>(true);
            var root = bridge != null ? bridge.transform : plat.transform.parent;
            var inside = root != null ? root.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList() : new List<string>();
            if (!inside.Contains("PlatformPivotSuperior") || !inside.Contains("PlatformPivotInferior"))
                Error($"Platform '{plat.name}' lost its pivots (PlatformPivotSuperior/Inferior) inside its bridge.", plat);
        }

        // 5. Player: "GroundCheck" child
        foreach (var move in Object.FindObjectsOfType<PlayerMove>(true))
            if (move.transform.Find("GroundCheck") == null) Error($"'{move.name}' lost its direct child 'GroundCheck'.", move);

        // 6. Tags the code uses
        foreach (var tag in RequiredTags)
            if (!all.Any(g => g.CompareTag(tag))) Error($"No object is tagged '{tag}'.");

        // 7. Organizing groups: empty, untagged objects holding islands, bridges or elevators.
        //    Their transform should be the identity, so what is inside sits where it was placed.
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
                Warn($"Group '{go.name}' is not at position 0 / rotation 0 / scale 1. What is inside may end up shifted (Reset its Transform).", go);
            if (!go.activeSelf)
                Error($"Group '{go.name}' is off: everything inside stops existing for the game.", go);
        }


        // 8. Missing scripts ("The referenced script on this Behaviour is missing")
        foreach (var go in all)
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing > 0) Warn($"'{go.name}' has {missing} missing script(s).", go);
        }

        string result = errors == 0 && warnings == 0
            ? "✔ Hierarchy OK: everything the code needs is in place."
            : $"Check finished: {errors} errors, {warnings} warnings.";
        if (errors > 0) Debug.LogError(result); else if (warnings > 0) Debug.LogWarning(result); else Debug.Log(result);
    }

    static List<GameObject> AllObjectsInScene()
    {
        var list = new List<GameObject>();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
        return list;
    }

    static void Error(string msg, Object ctx = null) { errors++; Debug.LogError("[Hierarchy] " + msg, ctx); }
    static void Warn(string msg, Object ctx = null) { warnings++; Debug.LogWarning("[Hierarchy] " + msg, ctx); }
}
