using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bridge : MonoBehaviour
{
    [Header ("References")]
    private GameObject estePuente;
    private IslandsManager islandManager;
    
    [Header("Speed Variables")]
    private float upSpeed; 
    private float fallSpeed; 

    public bool bridgeGoUp; 
    public bool exclusion; 

    private Transform bridgePos;
    private Vector3 masterpivot;
    private Vector3 upperPivot;
    public Vector3 lowerpivot;
    private Vector3 fallLimit;
    private Vector3 offsetVector;
    public float offset;
    

    void Start()
    {
        islandManager = GameObject.Find("Scene Manager").GetComponent<IslandsManager>();
        estePuente = gameObject;
        upSpeed = islandManager.speed;
        fallSpeed = islandManager.fallSpeed;

        bridgePos = estePuente.transform;
        if (!exclusion) masterpivot = GameObject.Find("MasterPivot").transform.position;
        else masterpivot = GameObject.Find("PivotPuenteZ").transform.position;
        upperPivot = new Vector3(bridgePos.position.x, masterpivot.y, bridgePos.position.z);
        fallLimit = GameObject.Find("LimiteCaida").transform.position;
        lowerpivot = new Vector3(bridgePos.position.x, fallLimit.y, bridgePos.position.z);
        offsetVector = new Vector3(0, offset, 0);
    }

    
    void Update()
    {        
        if (bridgeGoUp)
        {
            if (bridgePos.position != upperPivot)
            {
                bridgePos.position = Vector3.MoveTowards(bridgePos.position, upperPivot, Time.deltaTime * upSpeed);
            }
        }
        else
        {
            if (bridgePos.position.y != lowerpivot.y)
            {
                bridgePos.position = Vector3.MoveTowards(bridgePos.position, lowerpivot + offsetVector, Time.deltaTime * fallSpeed);
            }            
        }
    }


}
