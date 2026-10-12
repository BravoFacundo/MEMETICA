// Herramienta de editor (usar UNA vez): pasa los pedestales a la estructura nueva.
// Menú: Memetica > Migrar pedestales
//   1. Prefab Pedestal.prefab:
//        PedAudio     → su AudioSource pasa a la raíz
//        PedTrigger   → su SphereCollider pasa a la raíz (centrado en el Socket)
//        PedParticles → su ParticleSystem pasa al Socket (misma posición visual)
//        PedPivot → Socket, Marmol → Visual_Keyed, Piedra → Visual_Keyless
//   2. Escena: lo mismo en los pedestales que no son instancias del prefab, y Marmol/Piedra según KeylessPedestal
//   3. Cada Pintura (Atril) queda apuntando a su pedestal
//   4. Crea el PedestalGroup de los pedestales enlazados (51, 52, 53) con onComplete → LevelManager.SubirIsla("0")
// Después: Memetica > Validar jerarquía, probar, y borrar PedestalTrigger.cs y este archivo.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class MigratePedestalsTool
{
    const string PrefabPath = "Assets/_Prefabs/Levels/Pedestal.prefab";
    static bool useUndo;   // en el prefab abierto por código no hace falta (ni conviene) registrar Undo

    [MenuItem("Memetica/Migrar pedestales")]
    static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Pedestales] Salí del Play antes de usar la herramienta."); return; }

        // 1. Prefab
        useUndo = false;
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        Pedestal prefabPed = root.GetComponent<Pedestal>();
        if (prefabPed != null && MigrateStructure(prefabPed))
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[Pedestales] Prefab migrado: " + PrefabPath);
        }
        PrefabUtility.UnloadPrefabContents(root);

        // 2. Escena
        useUndo = true;
        Undo.SetCurrentGroupName("Migrar pedestales");
        int undoGroup = Undo.GetCurrentGroup();
        Pedestal[] all = Object.FindObjectsOfType<Pedestal>(true);

        foreach (Pedestal p in all)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(p))
            {
                if (MigrateStructure(p)) Debug.Log("[Pedestales] Migrado en escena: " + p.name, p);
            }
            else if (p.transform.Find("PedTrigger") != null)
                Debug.LogWarning("[Pedestales] '" + p.name + "' es instancia de otro prefab y sigue con la estructura vieja. Revisalo a mano.", p);

            p.ApplyVisual();
            p.SyncTrigger();
        }

        // 3. Pinturas → pedestal (primero los activos, para que no gane uno apagado)
        foreach (Pedestal p in all.OrderByDescending(x => x.gameObject.activeInHierarchy))
        {
            Transform pintura = p.transform.parent != null ? p.transform.parent.Find("Pintura") : null;
            Atril atril = pintura != null ? pintura.GetComponentInChildren<Atril>(true) : null;
            if (atril == null) continue;
            if (atril.pedestal != null && atril.pedestal != p)
            {
                Debug.LogWarning("[Pedestales] La Pintura de '" + p.transform.parent.name + "' ya apunta a otro pedestal. Elegí a mano.", atril);
                continue;
            }
            SetRef(atril, "pedestal", p);
            Debug.Log("[Pedestales] Pintura de " + p.transform.parent.name + " → " + p.name, atril);
        }

        // 4. Grupo enlazado
        CreateGroup(all);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Pedestales] Listo. Corré Memetica > Validar jerarquía.");
    }

    // ---------- Estructura de un pedestal ----------
    static bool MigrateStructure(Pedestal p)
    {
        Transform t = p.transform;
        Transform pivot = t.Find("PedPivot");
        Transform pedAudio = t.Find("PedAudio");
        Transform pedTrigger = t.Find("PedTrigger");
        Transform pedParticles = t.Find("PedParticles");
        Transform marmol = t.Find("Marmol");
        Transform piedra = t.Find("Piedra");
        if (pivot == null && pedAudio == null && pedTrigger == null && pedParticles == null && marmol == null && piedra == null)
            return false; // ya migrado

        // Nombres
        Rename(pivot, "Socket");
        Rename(marmol, "Visual_Keyed");
        Rename(piedra, "Visual_Keyless");
        Transform socket = t.Find("Socket");

        // Audio → raíz
        if (pedAudio != null && pedAudio.TryGetComponent(out AudioSource srcAudio))
        {
            AudioSource dst = GetOrAdd<AudioSource>(p.gameObject);
            EditorUtility.CopySerialized(srcAudio, dst);
        }

        // Trigger → raíz (mismo tamaño, centrado en el Socket)
        if (pedTrigger != null && pedTrigger.TryGetComponent(out SphereCollider srcCol))
        {
            SphereCollider dst = GetOrAdd<SphereCollider>(p.gameObject);
            if (useUndo) Undo.RecordObject(dst, "Trigger");
            dst.isTrigger = true;
            dst.radius = srcCol.radius * pedTrigger.localScale.x;
            dst.center = socket != null ? t.InverseTransformPoint(socket.position) : pedTrigger.localPosition;
        }

        // Partículas → Socket (corridas para que salgan del mismo lugar que antes)
        if (pedParticles != null && socket != null && pedParticles.TryGetComponent(out ParticleSystem srcPS))
        {
            ParticleSystem dstPS = GetOrAdd<ParticleSystem>(socket.gameObject);
            EditorUtility.CopySerialized(srcPS, dstPS);
            if (pedParticles.TryGetComponent(out ParticleSystemRenderer srcR))
                EditorUtility.CopySerialized(srcR, socket.GetComponent<ParticleSystemRenderer>());

            Vector3 offset = socket.InverseTransformPoint(pedParticles.position);
            var shape = dstPS.shape;
            if (shape.enabled) shape.position += offset;
            else Debug.LogWarning("[Pedestales] Las partículas de '" + p.name + "' no usan Shape: revisá su posición en el Socket.", socket);
        }

        // Borrar los hijos viejos
        foreach (Transform old in new[] { pedAudio, pedTrigger, pedParticles })
            if (old != null) { if (useUndo) Undo.DestroyObjectImmediate(old.gameObject); else Object.DestroyImmediate(old.gameObject); }

        // Referencias
        SerializedObject so = new SerializedObject(p);
        so.FindProperty("socket").objectReferenceValue = socket;
        so.FindProperty("visualKeyed").objectReferenceValue = t.Find("Visual_Keyed")?.gameObject;
        so.FindProperty("visualKeyless").objectReferenceValue = t.Find("Visual_Keyless")?.gameObject;
        so.ApplyModifiedProperties();
        return true;
    }

    // ---------- Grupo enlazado ----------
    static void CreateGroup(Pedestal[] all)
    {
        if (all.Any(p => p.group != null)) { Debug.Log("[Pedestales] El grupo enlazado ya existe, lo salteo."); return; }

        foreach (Pedestal leader in all)
        {
            SerializedProperty legacy = new SerializedObject(leader).FindProperty("LinkedPedestals");
            if (legacy == null || legacy.arraySize == 0) continue;

            var members = new List<Pedestal> { leader };
            for (int i = 0; i < legacy.arraySize; i++)
            {
                GameObject go = legacy.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (go != null && go.TryGetComponent(out Pedestal m) && !members.Contains(m)) members.Add(m);
            }

            // Va dentro del mismo Level que el pedestal líder
            Transform level = leader.transform;
            while (level.parent != null && level.parent.name != "Levels") level = level.parent;
            Transform parent = level.parent != null ? level : null;

            GameObject groupGO = new GameObject("PedestalGroup_Final");
            Undo.RegisterCreatedObjectUndo(groupGO, "Crear grupo");
            if (parent != null) Undo.SetTransformParent(groupGO.transform, parent, "Crear grupo");
            groupGO.transform.localPosition = Vector3.zero;

            PedestalGroup group = groupGO.AddComponent<PedestalGroup>();
            if (group.onComplete == null) group.onComplete = new UnityEvent();
            group.pedestals = members.OrderBy(m => m.pedestalID).ToArray();

            LevelManager lm = Object.FindObjectOfType<LevelManager>(true);
            if (lm != null) UnityEventTools.AddStringPersistentListener(group.onComplete, lm.SubirIsla, "0");
            else Debug.LogWarning("[Pedestales] No encontré el LevelManager: asigná a mano el onComplete del grupo.", group);

            foreach (Pedestal m in members) SetRef(m, "group", group);

            Debug.Log("[Pedestales] Grupo creado con " + string.Join(", ", group.pedestals.Select(m => m.pedestalID)) + " → SubirIsla(\"0\")", group);
            return;
        }
        Debug.LogWarning("[Pedestales] No encontré pedestales enlazados (LinkedPedestals vacío).");
    }

    // ---------- Utilidades ----------
    static void Rename(Transform t, string name)
    {
        if (t == null) return;
        if (useUndo) Undo.RecordObject(t.gameObject, "Renombrar");
        t.gameObject.name = name;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c != null) return c;
        return useUndo ? Undo.AddComponent<T>(go) : go.AddComponent<T>();
    }

    static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
