using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Todo lo general del nivel. Va en Managers > LevelManager.
// - Alturas y caídas (antes: objetos MasterPivot, LimiteCaida y las Cage)
// - Subir y bajar islas, puentes y ascensores (antes: IslandsManager en "Scene Manager")
// Los demás scripts lo usan con LevelManager.Instance.
//
// Los pedestales piden cosas por ID y acá se buscan por nombre, con la convención Tipo_ID:
//   isla "3" → "Island_3"   |   puente "A" → "Bridge_A"   |   isla "0" → fin del juego
//   elevators: "Tutorial_02" → "Elevator_Tutorial_02" (an island ID that matches an elevator starts its ride)
// Cada isla tiene un hijo "RespawnPoint" donde reaparece el jugador.
// Para sumar una isla o un puente no hace falta tocar código: alcanza con respetar la convención.
// Los prefijos se pueden cambiar en el Inspector (sección NOMBRES).
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("ALTURAS")]
    [Tooltip("Hasta dónde suben islas, puentes y ascensores (antes: MasterPivot)")]
    public float topY = 0f;
    [Tooltip("Hasta dónde bajan al caer (antes: LimiteCaida)")]
    public float bottomY = -30f;

    [Header("CAÍDAS")]
    [Tooltip("Metros por debajo del último piso pisado para considerar que cayó al vacío (antes: Cage)")]
    public float fallDistance = 20f;

    [Header("VELOCIDADES")]
    public float speed = 5;
    public float fallSpeed = 6;

    [Header("NOMBRES (convención Tipo_ID)")]
    public string islandPrefix = "Island_";
    public string bridgePrefix = "Bridge_";
    public string elevatorPrefix = "Elevator_";
    [Tooltip("Nombre del hijo de cada isla donde se reaparece")]
    public string respawnPointName = "RespawnPoint";

    [Header("REFERENCIAS")]
    [Tooltip("Donde reaparece todo al empezar: el RespawnPoint de la isla del tutorial")]
    public Transform startRespawnPoint;

    [Header("FIN DEL JUEGO")]
    [Tooltip("Segundos que caen las islas antes de cerrar el juego")]
    public float endingDuration = 10f;

    // Se arman solas al empezar, buscando los scripts en la escena
    private readonly Dictionary<string, Island> islands = new Dictionary<string, Island>();
    private readonly Dictionary<string, Bridge> bridges = new Dictionary<string, Bridge>();
    private readonly Dictionary<string, Elevator> elevators = new Dictionary<string, Elevator>();

    void Awake()
    {
        Instance = this;   // antes de que las islas, puentes y pedestales lo usen en su Start

        foreach (Island i in FindObjectsOfType<Island>()) islands[i.name] = i;
        foreach (Bridge b in FindObjectsOfType<Bridge>()) bridges[b.name] = b;
        foreach (Elevator e in FindObjectsOfType<Elevator>()) elevators[e.name] = e;
    }

    // ---------- Lo que piden los pedestales ----------

    public void SubirIsla(string id)
    {
        switch (id)
        {
            case "0": StartCoroutine(GameComplete()); break;
            default:
                // Elevators share the pedestal IDs with islands: if one matches, it starts its ride
                if (elevators.TryGetValue(elevatorPrefix + id, out Elevator elevator)) elevator.StartRide();
                else if (TryGet(islands, islandPrefix + id, out Island isla)) isla.islaSube = true;
                break;
        }
    }

    public void SubirPuente(string id)
    {
        if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) puente.bridgeGoUp = true;
    }

    public void BajarPuente(string id)
    {
        if (TryGet(bridges, bridgePrefix + id, out Bridge puente)) puente.bridgeGoUp = false;
    }

    // ---------- Consultas ----------

    // La isla con ese ID (por ejemplo "1" → Island_1), o null
    public GameObject GetIsland(string id)
    {
        return TryGet(islands, islandPrefix + id, out Island isla) ? isla.gameObject : null;
    }

    // El punto de reaparición de una isla (su hijo RespawnPoint), o null
    public Transform RespawnPointOf(GameObject island)
    {
        return island != null ? island.transform.Find(respawnPointName) : null;
    }

    IEnumerator GameComplete()
    {
        GameObject.Find("Player").GetComponent<PlayerMove>().ascend = true;

        // Todas las islas caen (más adelante: primero los puentes, al azar, y que se desvanezcan)
        foreach (Island isla in islands.Values)
        {
            isla.islasBajan = true;
            isla.islaSube = false;
        }

        yield return new WaitForSeconds(endingDuration);
        FindObjectOfType<PauseMenu>().QuitGame();
    }

    // Busca y avisa en la consola si un pedestal pide algo que no existe
    bool TryGet<T>(Dictionary<string, T> table, string objectName, out T value)
    {
        if (table.TryGetValue(objectName, out value)) return true;
        Debug.LogWarning("[LevelManager] No existe '" + objectName + "' en la escena (revisá el ID en el pedestal).", this);
        return false;
    }
}
