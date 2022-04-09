//In the project, the player moves on floating islands and bridges.
//This Script is used to relocate the Player to the last island they stepped on if they fall into the void.
//Since the need to relocate an Orb when it falls is no different from the player's, this script is reused on Orbs as well.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnPlayer : MonoBehaviour
{
    private Transform respawnPosition;

    private void Start()
    {
        respawnPosition = GameObject.Find("Respawn Pivot").GetComponent<Transform>();
    }

    private void Update()
    {
        if (Physics.Raycast(gameObject.transform.position, transform.TransformDirection(Vector3.down), out RaycastHit hit, 5f)
            && FindParentWithTag(hit.transform.gameObject, "Isla") != null)
        {
            switch (FindParentWithTag(hit.transform.gameObject, "Isla").transform.name)
            {
                case "AreaTuto":
                    respawnPosition = GameObject.Find("PivotAreaTuto").GetComponent<Transform>();
                    break;

                case "Area1":
                    respawnPosition = GameObject.Find("PivotArea1").GetComponent<Transform>();
                    break;
                case "Area2":
                    respawnPosition = GameObject.Find("PivotArea2").GetComponent<Transform>();
                    break;
                case "Area3":
                    respawnPosition = GameObject.Find("PivotArea3").GetComponent<Transform>();
                    break;
                case "Area4":
                    respawnPosition = GameObject.Find("PivotArea4").GetComponent<Transform>();
                    break;
                case "Area5":
                    respawnPosition = GameObject.Find("PivotArea5").GetComponent<Transform>();
                    break;
                case "Area6":
                    respawnPosition = GameObject.Find("PivotArea6").GetComponent<Transform>();
                    break;
                case "Area7":
                    respawnPosition = GameObject.Find("PivotArea7").GetComponent<Transform>();
                    break;

                case "AreaA":
                    respawnPosition = GameObject.Find("PivotAreaA").GetComponent<Transform>();
                    break;
                case "AreaB":
                    respawnPosition = GameObject.Find("PivotAreaB").GetComponent<Transform>();
                    break;
                case "AreaC":
                    respawnPosition = GameObject.Find("PivotAreaC").GetComponent<Transform>();
                    break;
                case "AreaD":
                    respawnPosition = GameObject.Find("PivotAreaD").GetComponent<Transform>();
                    break;

                case null:
                    break;
            }
        }
    }
    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Respawn"))
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