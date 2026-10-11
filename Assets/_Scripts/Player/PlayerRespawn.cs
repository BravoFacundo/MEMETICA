//In the project, the player moves on floating islands and bridges.
//This Script is used to relocate the Player to the last island they stepped on if they fall into the void.
//Since the need to relocate an Orb when it falls is no different from the player's, this script is reused on Orbs as well.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    private Transform respawnPosition;
    private Transform lastHit;
    private GameObject lastIsland;

    private void Start()
    {
        // FIX: el juego arranca en el tutorial, pero "Respawn Pivot" está arriba del Área 1.
        // AreaTuto no tiene el tag "Isla", así que el raycast nunca lo registraba y caerse en el tutorial te mandaba al Área 1.
        respawnPosition = GameObject.Find("PivotAreaTuto").transform;
    }

    private void Update()
    {
        if (!Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out RaycastHit hit, 5f)) return;
        if (hit.transform == lastHit) return;              // sigue parado sobre lo mismo: nada que hacer
        lastHit = hit.transform;

        GameObject isla = FindParentWithTag(hit.transform.gameObject, "Isla");
        if (isla == null || isla == lastIsland) return;    // un puente, o la misma isla
        lastIsland = isla;

        // Cada isla tiene su pivote llamado "Pivot" + nombre (AreaTuto -> PivotAreaTuto, Area3 -> PivotArea3...)
        GameObject pivot = GameObject.Find("Pivot" + isla.name);
        if (pivot != null) respawnPosition = pivot.transform;
    }
    // Fija el respawn a mano. Lo usa el ascensor: el jugador llega al Área 1 sin haberla pisado.
    public void SetRespawn(Transform pivot)
    {
        respawnPosition = pivot;
        lastHit = null;   // obliga a volver a revisar el piso en el próximo frame
    }

    public void OnTriggerExit(Collider other)
    {
        // FIX: solo reaparecer si salió de la jaula por ABAJO (se cayó al vacío).
        // Los orbes de las islas 3 y 4 empiezan DENTRO de la jaula y salen por ARRIBA cuando su isla sube:
        // antes eso los teletransportaba al pivote de respawn y quedaban flotando sobre la isla.
        if (other.CompareTag("Respawn") && transform.position.y < other.bounds.center.y)
            gameObject.transform.position = respawnPosition.transform.position;
    }

    //In the project, the objects tagged "island" may be too far up the hierarchy from the object the raycast hits.
    //This function returns the parent object with the "Island" tag regardless of how far up the hierarchy it is.
    public GameObject FindParentWithTag(GameObject childObject, string tag)
    {
        Transform t = childObject.transform;
        while (t.parent != null)
        {
            if (t.parent.CompareTag(tag))
            {               
                return t.parent.gameObject;
            }
            t = t.parent.transform;
        }
        return null;
    }
}