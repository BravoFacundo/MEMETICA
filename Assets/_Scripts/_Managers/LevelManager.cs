using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// Todo lo general del nivel. Va en Managers > LevelManager.
// - Drop Distance: one distance for heights and falls (before: Top Y / Bottom Y, Fall Distance; MasterPivot, LimiteCaida, Cage)
// - Subir y bajar islas, puentes y ascensores (antes: IslandsManager en "Scene Manager")
// Los demás scripts lo usan con LevelManager.Instance.
//
// Los pedestales piden cosas por ID y acá se buscan por nombre, con la convención Tipo_ID:
//   isla "3" → "Island_3"   |   puente "A" → "Bridge_A"   |   isla "0" → fin del juego
//   elevators: "Tutorial_02" → "Elevator_Tutorial_02" (an island ID that matches an elevator starts its ride)
// Leading zeros in numbers are ignored: "2", "02" and "002" all find Island_2 / Island_02 (no limit on how many islands).
// Cada isla tiene un hijo "RespawnPoint" donde reaparece el jugador.
// Para sumar una isla o un puente no hace falta tocar código: alcanza con respetar la convención.
// Los prefijos se pueden cambiar en el Inspector (sección NOMBRES).
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Section("Heights")]
    [Tooltip("One distance for the whole game, about down to the clouds: islands and bridges wait this far below their place " +
             "(invisible), falling this far below the last ground is a fall into the void, and you (and memes) reappear this far " +
             "above the ground of the RespawnPoint")]
    public float dropDistance = 30f;

    [Section("Lift (islands and bridges)")]
    [Tooltip("Seconds an island or bridge takes to rise, however far it travels")]
    public float liftRiseDuration = 3f;
    [Tooltip("Seconds a bridge takes to go back down")]
    public float liftLowerDuration = 2.5f;
    [Tooltip("Meters it passes the top before settling")]
    public float liftOvershoot = 0.4f;
    [Tooltip("Pedestals with In Order: seconds between one thing arriving and the next one starting")]
    public float sequenceGap = 0.3f;

    [Section("Debug", Closed = true)]
    [Tooltip("Scene view: a shadow of each island and bridge where it waits hidden, and the line you fall from above each RespawnPoint")]
    public bool showDropGizmos = true;

    [Section("Game")]
    [Tooltip("Where everything reappears at the start: the RespawnPoint of the tutorial island")]
    public Transform startRespawnPoint;
    [Tooltip("Objects you can turn off in the editor to see better (e.g. Clouds_HideIslands): they are turned on when the game starts")]
    public GameObject[] activateOnPlay;
    [Tooltip("Seconds the islands fall before the game closes")]
    public float endingDuration = 10f;

    [Section("Names (Type_ID)", Closed = true)]
    public string islandPrefix = "Island_";
    public string bridgePrefix = "Bridge_";
    public string elevatorPrefix = "Elevator_";
    [Tooltip("Name of the child of each island where things reappear")]
    public string respawnPointName = "RespawnPoint";

    // Se arman solas al empezar, buscando los scripts en la escena
    private readonly Dictionary<string, Island> islands = new Dictionary<string, Island>();
    private readonly Dictionary<string, Bridge> bridges = new Dictionary<string, Bridge>();
    private readonly Dictionary<string, Elevator> elevators = new Dictionary<string, Elevator>();

    void Awake()
    {
        Instance = this;   // antes de que las islas, puentes y pedestales lo usen en su Start
        foreach (GameObject o in activateOnPlay) if (o != null) o.SetActive(true);

        foreach (Island i in FindObjectsOfType<Island>()) islands[NormalizeName(i.name)] = i;
        foreach (Bridge b in FindObjectsOfType<Bridge>()) bridges[NormalizeName(b.name)] = b;
        foreach (Elevator e in FindObjectsOfType<Elevator>()) elevators[NormalizeName(e.name)] = e;
    }

    // ---------- Lo que piden los pedestales ----------

    public void SubirIsla(string id)
    {
        switch (NormalizeName(id))
        {
            case "0": StartCoroutine(GameComplete()); break;
            default:
                // Elevators share the pedestal IDs with islands: if one matches, it starts its ride
                if (elevators.TryGetValue(NormalizeName(elevatorPrefix + id), out Elevator elevator)) elevator.StartRide();
                else if (TryGet(islands, islandPrefix + id, out Island isla)) isla.Raise();
                break;
        }
    }

    public void SubirPuente(string id)
    {
        if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) puente.Raise();
    }

    // Pedestal with In Order: each island (or elevator) rises after the previous one arrived, then each bridge
    // appears after the previous one is in place. Lists in the order they should happen
    public void RaiseInOrder(string[] islandIds, string[] bridgeIds) => StartCoroutine(InOrder(islandIds, bridgeIds));

    IEnumerator InOrder(string[] islandIds, string[] bridgeIds)
    {
        foreach (string id in islandIds)
        {
            SubirIsla(id);
            yield return null;
            if (elevators.TryGetValue(NormalizeName(elevatorPrefix + id), out Elevator elevator)) while (elevator.IsRiding) yield return null;
            else if (TryGet(islands, islandPrefix + id, out Island isla)) while (isla.IsMoving) yield return null;
            yield return new WaitForSeconds(sequenceGap);
        }
        foreach (string id in bridgeIds)
        {
            SubirPuente(id);
            yield return null;
            if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) while (puente.IsAppearing || puente.IsMoving) yield return null;
            yield return new WaitForSeconds(sequenceGap);
        }
    }

    public void BajarPuente(string id)
    {
        if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) puente.Lower();
    }

    // Debug: up right away, no animation (DebugTools starts)
    public void SnapIslandUp(string id)
    {
        if (TryGet(islands, islandPrefix + id, out Island isla)) isla.SnapUp();
    }

    public void SnapBridgeUp(string id)
    {
        if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) puente.SnapUp();
    }

    // ---------- Consultas ----------

    // Bridges wait for this before appearing, so they connect to islands that are already there
    public bool AnyIslandMoving
    {
        get
        {
            foreach (Island isla in islands.Values) if (isla.IsMoving) return true;
            return false;
        }
    }

    // The island's respawn point (any child named RespawnPoint, at any depth), or null
    // Where the player reappears for a RespawnPoint: Drop Distance above the ground under it, falling down onto it
    public Vector3 RespawnPosition(Transform point, out float groundY)
    {
        bool hasGround = Physics.Raycast(point.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 200f, ~0, QueryTriggerInteraction.Ignore);
        groundY = hasGround ? ground.point.y : point.position.y;
        return new Vector3(point.position.x, groundY + dropDistance, point.position.z);
    }

#if UNITY_EDITOR
    // Above each RespawnPoint: the fall the player makes when reappearing
    void OnDrawGizmos()
    {
        if (!showDropGizmos) return;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        foreach (Transform t in FindObjectsOfType<Transform>())
        {
            if (t.name != respawnPointName) continue;
            Vector3 from = RespawnPosition(t, out float groundY);
            Gizmos.DrawLine(from, new Vector3(from.x, groundY, from.z));
            Gizmos.DrawWireSphere(from, 0.6f);
        }
    }
#endif

    public Transform RespawnPointOf(GameObject island)
    {
        if (island == null) return null;
        Island root = island.GetComponentInParent<Island>();
        Transform container = root != null ? root.transform : island.transform;
        foreach (Transform child in container.GetComponentsInChildren<Transform>(true))
            if (child.name == respawnPointName) return child;
        return null;
    }

    IEnumerator GameComplete()
    {
        GameObject.Find("Player").GetComponent<PlayerMove>().ascend = true;

        // Todas las islas caen (más adelante: primero los puentes, al azar, y que se desvanezcan)
        foreach (Island isla in islands.Values) isla.FallAway();

        yield return new WaitForSeconds(endingDuration);

        // Back to the desktop (Menu) instead of closing the game: the next visitor can play right away
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null) uiManager.GoToMenu();
        else FindObjectOfType<PauseMenu>().QuitGame();
    }

    // "Island_02" → "Island_2": numbers without leading zeros, so 1 and 01 are the same ID
    public static string NormalizeName(string objectName)
    {
        return Regex.Replace(objectName, @"\d+", match =>
        {
            string number = match.Value.TrimStart('0');
            return number.Length > 0 ? number : "0";
        });
    }

    // Busca y avisa en la consola si un pedestal pide algo que no existe
    bool TryGet<T>(Dictionary<string, T> table, string objectName, out T value)
    {
        if (table.TryGetValue(NormalizeName(objectName), out value)) return true;
        Debug.LogWarning("[LevelManager] No existe '" + objectName + "' en la escena (revisá el ID en el pedestal).", this);
        return false;
    }
}
