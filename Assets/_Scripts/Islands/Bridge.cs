using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bridge : MonoBehaviour
{
    [Header ("References")]
    private GameObject estePuente;
    private LevelManager level;
    
    [Header("Speed Variables")]
    private float upSpeed; 
    private float fallSpeed; 

    public bool bridgeGoUp; 
    public bool exclusion; 

    private Transform bridgePos;
    private Vector3 upperPivot;
    public Vector3 lowerpivot;
    private Vector3 offsetVector;
    public float offset;
    

    void Start()
    {
        level = LevelManager.Instance;
        estePuente = gameObject;
        upSpeed = level.speed;
        fallSpeed = level.fallSpeed;

        bridgePos = estePuente.transform;
        // Alturas desde el LevelManager (antes: MasterPivot y LimiteCaida). El puente del tutorial (exclusion) sube hasta PivotPuenteZ.
        float topY = exclusion ? GameObject.Find("PivotPuenteZ").transform.position.y : level.topY;
        upperPivot = new Vector3(bridgePos.position.x, topY, bridgePos.position.z);
        lowerpivot = new Vector3(bridgePos.position.x, level.bottomY, bridgePos.position.z);
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
