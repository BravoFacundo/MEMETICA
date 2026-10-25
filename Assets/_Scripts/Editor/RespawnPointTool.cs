// Memetica > Level > Respawn: placing respawn points (RespawnPoint).
//   New Respawn Point     one on the ground where the Scene view's middle points (the island under it), facing the way
//                         the Scene view looks (look at the course from where it starts and run it). Inside its
//                         island, called RespawnPoint. Made from the prefab Assets/_Prefabs/RespawnPoint (made the
//                         first time)
//   Make Start Respawn    the selected one is where everything starts and where the hub's respawn is
//                         (LevelManager > Start Respawn Point)
// Ctrl+Z undoes each one. Save the scene after.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RespawnPointTool
{
    const string Menu = "Memetica/Level/Respawn/";
    const string PrefabPath = "Assets/_Prefabs/RespawnPoint.prefab";

    [MenuItem(Menu + "New Respawn Point", false, 340)]
    static void NewPoint()
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) { Debug.LogError("[Respawn] Open a Scene view first."); return; }
        Ray ray = view.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
        { Debug.LogError("[Respawn] No ground in the middle of the Scene view: point it at an island."); return; }

        Transform island = IslandOf(hit.transform);
        if (island == null) Debug.LogWarning($"[Respawn] '{hit.transform.name}' isn't inside an island (Island, tag Isla): it won't be used until you move it into one.", hit.transform);

        var point = (GameObject)PrefabUtility.InstantiatePrefab(Prefab());
        Undo.RegisterCreatedObjectUndo(point, "New Respawn Point");
        point.name = FindLevel() != null ? FindLevel().respawnPointName : "RespawnPoint";
        if (island != null) point.transform.SetParent(island, true);
        Vector3 facing = Vector3.ProjectOnPlane(view.camera.transform.forward, Vector3.up);
        point.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward, Vector3.up));

        EditorSceneManager.MarkSceneDirty(point.scene);
        Selection.activeGameObject = point;
        Debug.Log($"[Respawn] Respawn point on '{hit.transform.name}'" + (island != null ? $" (island '{island.name}')" : "") + ", facing the way the Scene view looks.", point);
    }

    [MenuItem(Menu + "Make Start Respawn", false, 341)]
    static void MakeStart()
    {
        LevelManager level = FindLevel();
        if (level == null) { Debug.LogError("[Respawn] No LevelManager in the scene."); return; }
        Undo.RecordObject(level, "Make Start Respawn");
        level.startRespawnPoint = Selection.activeTransform;
        EditorUtility.SetDirty(level);
        EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        Debug.Log($"[Respawn] '{Selection.activeTransform.name}' is the start (and hub) respawn.", Selection.activeTransform);
    }

    [MenuItem(Menu + "Make Start Respawn", true)]
    static bool CanMakeStart() => Selection.activeTransform != null && Selection.activeTransform.GetComponent<RespawnPoint>() != null;

    // Where it goes: the floating part of the island (it floats with it), else the island
    static Transform IslandOf(Transform t)
    {
        FloatMotion floating = t.GetComponentInParent<FloatMotion>();
        if (floating != null) return floating.transform;
        Island island = t.GetComponentInParent<Island>();
        if (island != null) return island.transform;
        for (Transform p = t; p != null; p = p.parent) if (p.CompareTag("Isla")) return p;
        return null;
    }

    static GameObject Prefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) return prefab;
        var temp = new GameObject("RespawnPoint");
        temp.AddComponent<RespawnPoint>();
        prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        Object.DestroyImmediate(temp);
        Debug.Log($"[Respawn] Prefab made: {PrefabPath}", prefab);
        return prefab;
    }

    static LevelManager FindLevel() => Object.FindObjectOfType<LevelManager>();
}
