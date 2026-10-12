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
    [Tooltip("Opcional: si está asignado, el puente sube hasta la altura de este objeto en vez de la general (lo usa el puente del tutorial)")]
    public Transform topOverride;

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
        // Alturas desde el LevelManager. Un puente con Top Override (el del tutorial) sube hasta ese objeto.
        float topY = topOverride != null ? topOverride.position.y : level.topY;
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
