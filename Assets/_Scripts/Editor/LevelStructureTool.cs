// Herramienta de editor: ordena la jerarquía de Levels a la estructura estándar.
// Menú: Memetica > Ordenar estructura de niveles
//   1. Orbes sueltos en un Level → pasan a ser hijos de la isla que tienen debajo
//   2. En cada isla y puente crea "Decor" (en 0,0,0) y mueve ahí todo lo que no es jugable
//   3. Lista en la consola los objetos desactivados (no los borra: decidís vos)
// Los nombres de los Level_ los manejás vos (orden de progresión): la herramienta no los toca.
// Se puede correr varias veces: lo que ya está hecho lo saltea. Ctrl+Z deshace todo.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelStructureTool
{
    const string LevelsRoot = "Levels";
    public const string DecorName = "Decor";

    // Hijos que el código busca por nombre → nunca van a Decor
    static readonly string[] GameplayNames =
        { "Pintura", "WindAudio", "PlatformPivotSuperior", "PlatformPivotInferior", DecorName };

    [MenuItem("Memetica/Ordenar estructura de niveles")]
    static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Estructura] Salí del Play antes de usar la herramienta."); return; }

        LevelManager level = Object.FindObjectOfType<LevelManager>(true);
        if (level == null) { Debug.LogError("[Estructura] No encuentro el LevelManager en la escena."); return; }

        Transform levels = SceneManager.GetActiveScene().GetRootGameObjects()
            .Select(g => g.transform).FirstOrDefault(t => t.name == LevelsRoot);
        if (levels == null) { Debug.LogError("[Estructura] No encuentro el objeto raíz '" + LevelsRoot + "'."); return; }

        Undo.SetCurrentGroupName("Ordenar estructura de niveles");
        int undoGroup = Undo.GetCurrentGroup();

        MoveLooseOrbs(levels);
        int moved = GroupDecor(levels, level);
        ListInactive(levels);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Estructura] Listo: " + moved + " props movidos a Decor. Corré Memetica > Validar jerarquía. Ctrl+Z deshace todo.");
    }

    // ---------- 1. Orbes sueltos → hijos de la isla que tienen debajo ----------
    static void MoveLooseOrbs(Transform levels)
    {
        Physics.SyncTransforms();
        foreach (OrbMemes orb in levels.GetComponentsInChildren<OrbMemes>(true))
        {
            if (orb.GetComponentInParent<Island>(true) != null) continue; // ya está en una isla

            // Lo primero que hay debajo (sin contar el propio orbe) tiene que ser parte de una isla
            RaycastHit ground = Physics.RaycastAll(orb.transform.position, Vector3.down, 200f)
                .Where(h => !h.transform.IsChildOf(orb.transform))
                .OrderBy(h => h.distance)
                .FirstOrDefault();
            Island below = ground.transform != null ? ground.transform.GetComponentInParent<Island>(true) : null;

            if (below == null)
            {
                string on = ground.transform != null ? ground.transform.name : "nada";
                Debug.LogWarning("[Estructura] El orbe '" + orb.name + "' está sobre '" + on + "', no sobre una isla. Lo dejo donde está.", orb);
                continue;
            }

            Undo.SetTransformParent(orb.transform, below.transform, "Mover " + orb.name);
            Debug.Log("[Estructura] Orbe " + orb.name + " → " + below.name, orb);
        }
    }

    // ---------- 2. Decor en cada isla y puente ----------
    static int GroupDecor(Transform levels, LevelManager level)
    {
        int moved = 0;
        var containers = levels.GetComponentsInChildren<Island>(true).Select(c => c.transform)
            .Concat(levels.GetComponentsInChildren<Bridge>(true).Select(c => c.transform));

        foreach (Transform container in containers)
        {
            List<Transform> props = container.Cast<Transform>()
                .Where(c => c.gameObject.activeSelf && !IsGameplay(c, level)).ToList();
            if (props.Count == 0) continue;

            Transform decor = GetOrCreateChild(container, DecorName);
            foreach (Transform p in props)
            {
                Undo.SetTransformParent(p, decor, "Mover a Decor");
                moved++;
            }
            Undo.RecordObject(decor, "Orden Decor");
            decor.SetAsLastSibling();
            Debug.Log("[Estructura] " + container.name + ": " + props.Count + " props → Decor", decor);
        }
        return moved;
    }

    // Jugable = el código lo busca, tiene tag, tiene sonido o tiene algún script nuestro (salvo FloatBob)
    public static bool IsGameplay(Transform t, LevelManager level)
    {
        string n = t.name;
        if (GameplayNames.Contains(n) || n == level.respawnPointName) return true;
        if (n.StartsWith(level.islandPrefix) || n.StartsWith(level.bridgePrefix) || n.StartsWith(level.elevatorPrefix) || n.StartsWith("Pedestal")) return true;
        if (!t.CompareTag("Untagged")) return true;
        if (t.GetComponentInChildren<AudioSource>(true) != null) return true;
        return t.GetComponentsInChildren<MonoBehaviour>(true)
            .Any(m => m != null && !(m is FloatBob) && m.GetType().Assembly.GetName().Name == "Assembly-CSharp");
    }

    // ---------- 3. Desactivados ----------
    static void ListInactive(Transform levels)
    {
        var inactive = levels.GetComponentsInChildren<Transform>(true)
            .Where(t => !t.gameObject.activeSelf).ToList();
        foreach (Transform t in inactive)
            Debug.LogWarning("[Estructura] Desactivado: " + PathOf(t) + " (¿lo borramos?)", t);
    }

    // ---------- Utilidades ----------
    static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
        Undo.SetTransformParent(go.transform, parent, "Crear " + name);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        Debug.Log("[Estructura] Creado " + parent.name + "/" + name, go);
        return go.transform;
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
