using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RespawnPlayer : MonoBehaviour
{
    //Para que este script ande se tiene que activar el autoSyncTransforms en las project settings

    public Transform RespawnPos;
    public Transform PlayerPos;
    public float RespawnFall = 6;

    public GameObject Prev;

    private void Start()
    {
        RespawnPos = GameObject.Find("Respawn Pivot").GetComponent<Transform>();
        PlayerPos = GameObject.Find("Player").GetComponent<Transform>();
    }

    private void Update()
    {              
        RaycastHit hit; //raycast hacia abajo para saber que estoy pisando
        if (Physics.Raycast(this.gameObject.transform.position, transform.TransformDirection(Vector3.down), out hit, 5f) 
            && ParentWithTag(hit.transform.gameObject, "Isla") != null) //si algun padre tiene el tag isla
        {
            switch (ParentWithTag(hit.transform.gameObject, "Isla").transform.name) //hacer algo segun el nombre del objeto con el tag isla
            {                
                                                 case "AreaTuto":
                              //Debug.Log("Respawn Set: Area 1");
                    RespawnPos = GameObject.Find("PivotAreaTuto").GetComponent<Transform>();
                    break;
                                                 case "Area1":
                              //Debug.Log("Respawn Set: Area 1");
                    RespawnPos = GameObject.Find("PivotArea1").GetComponent<Transform>();
                    break;
                                                 case "Area2":
                              //Debug.Log("Respawn Set: Area 2");
                    RespawnPos = GameObject.Find("PivotArea2").GetComponent<Transform>();
                    break;
                                                 case "Area3":
                              //Debug.Log("Respawn Set: Area 3");
                    RespawnPos = GameObject.Find("PivotArea3").GetComponent<Transform>();
                    break;
                                                 case "Area4":
                              //Debug.Log("Respawn Set: Area 4");
                    RespawnPos = GameObject.Find("PivotArea4").GetComponent<Transform>();
                    break;
                                                 case "Area5":
                              //Debug.Log("Respawn Set: Area 5");
                    RespawnPos = GameObject.Find("PivotArea5").GetComponent<Transform>();
                    break;
                                                 case "Area6":
                              //Debug.Log("Respawn Set: Area 6");
                    RespawnPos = GameObject.Find("PivotArea6").GetComponent<Transform>();
                    break;
                                                 case "Area7":
                              //Debug.Log("Respawn Set: Area 7");
                    RespawnPos = GameObject.Find("PivotArea7").GetComponent<Transform>();
                    break;

                                                 case "AreaA":
                              //Debug.Log("Respawn Set: Area A");
                    RespawnPos = GameObject.Find("PivotAreaA").GetComponent<Transform>();
                    break;
                                                 case "AreaB":
                              //Debug.Log("Respawn Set: Area B");
                    RespawnPos = GameObject.Find("PivotAreaB").GetComponent<Transform>();
                    break;
                                                 case "AreaC":
                              //Debug.Log("Respawn Set: Area C");
                    RespawnPos = GameObject.Find("PivotAreaC").GetComponent<Transform>();
                    break;
                                                 case "AreaD":
                              //Debug.Log("Respawn Set: Area D");
                    RespawnPos = GameObject.Find("PivotAreaD").GetComponent<Transform>();
                    break;

                case null:
                    break;

            }
        }
    }

    public GameObject ParentWithTag(GameObject childObject, string tag)
    {
        Transform t = childObject.transform;
        while (t.parent != null )
        {
            if (t.parent.tag == tag) 
            {
                //Debug.Log(t.parent.gameObject.name);
                //Prev = t.parent.gameObject;
                return t.parent.gameObject;
            }
            t = t.parent.transform;
        }
        return null; // no se pudo encontrar un padre con el Tag.
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.tag == "Respawn")
        {            
            //new Vector3(Respawn.transform.position.x, Respawn.transform.position.y + RespawnFall, Respawn.transform.position.z);
            this.gameObject.transform.position = RespawnPos.transform.position;
            //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
