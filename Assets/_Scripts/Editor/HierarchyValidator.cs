// Editor tool: checks that the hierarchy still matches what the code expects.
// Menu: Memetica > Check > Validate Hierarchy
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

    static int errors, warnings;

    [MenuItem("Memetica/Check/Validate Hierarchy", false, 400)]
    public static void Validate()
    {
        errors = 0; warnings = 0;
        var all = AllObjectsInScene();

        // 1. LevelManager references, islands and bridges (names are free: everything goes by reference)
        LevelManager level = Object.FindObjectOfType<LevelManager>(true);
        if (level == null) Error("There is no LevelManager in the scene.");
        else
        {
            if (level.startRespawnPoint == null) Error("LevelManager: Start Respawn Point not assigned.", level);

            var noRespawn = new List<string>();
            foreach (var island in Object.FindObjectsOfType<Island>(true))
            {
                if (level.RespawnPointOf(island.gameObject) == null) noRespawn.Add(island.name);   // (fine: you reappear where you last stood on it)

                // Floating: on the island itself (new) or on one direct child (old structure)
                bool floatsItself = island.GetComponent<FloatMotion>() != null;
                int floatingChildren = island.transform.Cast<Transform>().Count(c => c.GetComponent<FloatMotion>() != null);
                if (!floatsItself && floatingChildren == 0) Warn($"Island '{island.name}' doesn't float (no FloatMotion on it or on a direct child).", island);
                if (floatsItself && floatingChildren > 0) Warn($"Island '{island.name}' floats twice (FloatMotion on it and on a child).", island);
            }
            if (noRespawn.Count > 0)
                Debug.Log($"[Hierarchy] Info: {noRespawn.Count} island(s) without a RespawnPoint, falling you reappear where you last stood on them: {string.Join(", ", noRespawn)}.");
            foreach (var bridge in Object.FindObjectsOfType<Bridge>(true))
            {
                bool bends = bridge.TryGetComponent(out BridgeBend _);
                if (bridge.appearance == Bridge.Appearance.ExtendFromA && !bends) Error($"Bridge '{bridge.name}' uses Extend From A but has no BridgeBend.", bridge);
                if (bends && (bridge.anchorA == null || bridge.anchorB == null)) Error($"Bridge '{bridge.name}' has a BridgeBend but is missing an anchor (right click on Bridge > Create Anchors).", bridge);
                foreach (Transform anchor in new[] { bridge.anchorA, bridge.anchorB })
                {
                    if (anchor == null) continue;
                    // Inside an island or an elevator, or at least something floating (the tutorial island, not a Lift): it follows it
                    bool floats = anchor.GetComponentInParent<FloatMotion>() != null;
                    if (Island.Of(anchor) == null && !floats) Warn($"Bridge '{bridge.name}': anchor '{anchor.name}' is not inside an island.", anchor);
                    else if (!floats) Warn($"Bridge '{bridge.name}': anchor '{anchor.name}' is not inside the floating part of its island (that end won't follow it).", anchor);
                }
            }
            foreach (var elevator in Object.FindObjectsOfType<Elevator>(true))
            {
                if (elevator.dockPoint == null) Error($"Elevator '{elevator.name}' has no Dock Point.", elevator);
                else if (!elevator.rideOnStart && elevator.dockPoint.GetComponentInParent<Island>() == null) Warn($"Elevator '{elevator.name}': its Dock Point is not inside an island (no respawn or docking on arrival).", elevator);
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
        // 2b. Pedestal kinds: a white one has its key and clue, a randomizer its axes
        foreach (var ped in Object.FindObjectsOfType<Pedestal>(true))
        {
            if (ped is KeyPedestal key)
            {
                if (key.key == null) Error($"Key pedestal '{ped.name}' has no Key (the meme it waits for).", ped);
                // A clue only where the scene uses them (OriginalGame's easels; Game's end-of-level pedestals have none)
                bool usesClues = Object.FindObjectsOfType<Painting>(true).Length > 0;
                if (usesClues && !Object.FindObjectsOfType<PedestalGroup>(true).Any(g => g.members.Contains(ped)) && key.Hint == null && Island.Of(ped.transform)?.GetComponentInChildren<Painting>(true) == null)
                    Warn($"Key pedestal '{ped.name}' has no Hint (the easel with its clue), and there is no painting on its island.", ped);
            }
            if (ped is RandomizerPedestal randomizer)
                foreach (var axis in randomizer.axes)
                    if (axis.meme == null || axis.way.Count == 0)
                        Warn($"Randomizer '{ped.name}' has an axis without a meme or without anything to raise.", ped);
            if (ped.raises.Exists(t => t == null || !Pedestal.IsRaisable(t)))
                Warn($"Pedestal '{ped.name}' has empty slots, or something that isn't an island, elevator, bridge or portal, in Raises.", ped);
        }
        foreach (var group in Object.FindObjectsOfType<PedestalGroup>(true))
            if (group.members.Count(m => m != null) < 2) Warn($"Pedestal group '{group.name}' has fewer than 2 pedestals: they complete alone.", group);

        // 3. Orbs: their sticker and their meme
        foreach (var orb in Object.FindObjectsOfType<MemeOrb>(true))
        {
            if (orb.sticker == null) Error($"Orb '{orb.name}' has no Sticker reference.", orb);
            if (orb.data == null) Warn($"Orb '{orb.name}' has no meme (Data).", orb);
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

        // 5. Player: one PlayerMove with its PlayerRespawn (the ground check is PlayerMove > Ground Check Height now)
        var moves = Object.FindObjectsOfType<PlayerMove>(true);
        if (moves.Length == 0) Error("There is no Player (PlayerMove).");
        foreach (var move in moves)
            if (move.GetComponent<PlayerRespawn>() == null) Warn($"'{move.name}' has no PlayerRespawn (Memetica > Check > Migrate Managers).", move);

        // 6. Tags the code uses

        // 7. Organizing groups: empty, untagged objects holding islands, bridges or elevators.
        //    Their transform should be the identity, so what is inside sits where it was placed.
        foreach (var go in all)
        {
            bool isEmpty = go.GetComponents<Component>().Length == 1 && go.transform.childCount > 0;
            bool untagged = go.CompareTag("Untagged");
            bool containsMovers = go.GetComponentsInChildren<Island>(true).Length > 0
                               || go.GetComponentsInChildren<Bridge>(true).Length > 0
                               || go.GetComponentsInChildren<Elevator>(true).Length > 0;
            bool isGroup = isEmpty && untagged && containsMovers ;
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
