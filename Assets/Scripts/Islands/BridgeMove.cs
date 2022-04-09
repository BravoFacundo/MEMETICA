using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BridgeMove : MonoBehaviour
{
    private IslandManager islandManager;
    private GameObject estePuente;
    private float upSpeed; 
    private float fallSpeed; 
    public bool puenteSube; 
    public bool exclusion; 

    private Transform puentePos;
    private Vector3 masterpivot;
    private Vector3 upperpivot;
    private Vector3 limiteCaida;
    public Vector3 lowerpivot;
    private Vector3 offsetVector;
    public float offset;

    void Start()
    {
        islandManager = GameObject.Find("Scene Manager").GetComponent<IslandManager>();
        estePuente = gameObject;
        upSpeed = islandManager.speed;
        fallSpeed = islandManager.fallSpeed;

        puentePos = estePuente.transform;
        if (!exclusion) masterpivot = GameObject.Find("MasterPivot").transform.position;
        else masterpivot = GameObject.Find("PivotPuenteZ").transform.position;
        upperpivot = new Vector3(puentePos.position.x, masterpivot.y, puentePos.position.z);
        limiteCaida = GameObject.Find("LimiteCaida").transform.position;
        lowerpivot = new Vector3(puentePos.position.x, limiteCaida.y, puentePos.position.z);
        offsetVector = new Vector3(0, offset, 0);
    }

    
    void Update()
    {        
        if (puenteSube)
        {
            if (puentePos.position != upperpivot)
            {
                puentePos.position = Vector3.MoveTowards(puentePos.position, upperpivot, Time.deltaTime * upSpeed);
            }
        }
        else
        {
            if (puentePos.position.y != lowerpivot.y)
            {
                puentePos.position = Vector3.MoveTowards(puentePos.position, lowerpivot + offsetVector, Time.deltaTime * fallSpeed);
            }            
        }
    }


}
