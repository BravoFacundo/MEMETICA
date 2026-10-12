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
    private float lastGroundY;   // altura del último piso pisado: caer muy por debajo = cayó al vacío

    private void Start()
    {
        // FIX: el juego arranca en el tutorial, pero "Respawn Pivot" está arriba del Área 1.
        // AreaTuto no tiene el tag "Isla", así que el raycast nunca lo registraba y caerse en el tutorial te mandaba al Área 1.
        respawnPosition = GameObject.Find("PivotAreaTuto").transform;
        lastGroundY = transform.position.y;
    }

    private void Update()
    {
        // ¿Cayó al vacío? (antes lo detectaba la Cage; ahora es la distancia al último piso)
        if (LevelManager.Instance != null && transform.position.y < lastGroundY - LevelManager.Instance.fallDistance)
        {
            Respawn();
            return;
        }

        if (!Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out RaycastHit hit, 5f)) return;
        lastGroundY = hit.point.y;                          // el piso de abajo (sigue a islas y ascensores que se mueven)
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

    void Respawn()
    {
        // El CharacterController del jugador ignora el cambio de posición si está prendido
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = respawnPosition.position;
        if (cc != null) cc.enabled = true;

        // Los orbes reaparecen quietos
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
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