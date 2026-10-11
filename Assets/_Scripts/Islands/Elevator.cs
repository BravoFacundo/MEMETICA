using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevator : MonoBehaviour
{
    private IslandsManager islandManager;
    private GameObject esteElevador;
    private float upSpeed = 20f; 
    private float fallSpeed; 
    public bool elevadorSube; 
    public bool oneLock = false; 

    private Transform elevadorPos;
    private Vector3 masterpivot;
    private Vector3 upperpivot;
    private Vector3 limiteCaida;
    public  Vector3 lowerpivot;
    private Vector3 offsetVector;
    public float offset;

    void Start()
    {
        islandManager = GameObject.Find("Scene Manager").GetComponent<IslandsManager>();
        esteElevador = gameObject;
        fallSpeed = islandManager.fallSpeed;

        elevadorPos = esteElevador.transform;
        masterpivot = GameObject.Find("MasterPivot").transform.position;
        upperpivot = new Vector3(elevadorPos.position.x, masterpivot.y, elevadorPos.position.z);
        limiteCaida = GameObject.Find("LimiteCaida").transform.position;
        lowerpivot = new Vector3(elevadorPos.position.x, limiteCaida.y, elevadorPos.position.z);
        offsetVector = new Vector3(0, offset, 0);
    }
    
    void Update()
    {        
        if (elevadorSube)
        {
            if (elevadorPos.position != upperpivot)
            {
                elevadorPos.position = Vector3.MoveTowards(elevadorPos.position, upperpivot, Time.deltaTime * upSpeed);
            }
            
        }
        else
        {
            if (elevadorPos.position.y != lowerpivot.y)
            {
                elevadorPos.position = Vector3.MoveTowards(elevadorPos.position, lowerpivot + offsetVector, Time.deltaTime * fallSpeed);
            }            
        }

        if(elevadorPos.position == upperpivot && !oneLock)
        {
            //GameObject.Find("1Ba").transform.SetParent(null);
            GameObject.Find("Player").GetComponent<PlayerMove>().playerCanMove = true;
            GameObject.Find("Player").transform.SetParent(null);
            Camera.main.GetComponent<PlayerHoldObjects>().canHold = true;
            GameObject.Find("ElevatorLocks").SetActive(false);
            GameObject.Find("Cage").GetComponent<Collider>().enabled = true;

            // FIX: antes guardaba PivotArea1 en una variable local que no usaba nadie, y el respawn seguía en el tutorial.
            // Ahora se lo pasa al Player y al orbe que viajó en el ascensor.
            Transform pivotArea1 = GameObject.Find("PivotArea1").transform;
            GameObject.Find("Player").GetComponent<PlayerRespawn>().SetRespawn(pivotArea1);
            GameObject orbeAscensor = GameObject.Find("1Ba");
            if (orbeAscensor != null && orbeAscensor.TryGetComponent(out PlayerRespawn orbRespawn))
                orbRespawn.SetRespawn(pivotArea1);
            GameObject.Find("ElevatorZ").transform.SetParent(GameObject.Find("Area1").transform);

            StartCoroutine(BreakPedestalWhenOrbTaken());

            oneLock = true;
        }
    }

    // Ya arriba: cuando el jugador saca el orbe del pedestal del ascensor, el pedestal se vuelca y queda roto
    IEnumerator BreakPedestalWhenOrbTaken()
    {
        GameObject orb = GameObject.Find("1Ba");
        GameObject pedestalObj = GameObject.Find("PedestalElevador");
        if (orb == null || pedestalObj == null) yield break;

        Pedestal pedestal = pedestalObj.GetComponent<Pedestal>();
        OrbMemes memes = orb.GetComponent<OrbMemes>();
        Vector3 startPos = orb.transform.position;

        // Espera a que lo agarre o lo mueva de su lugar
        while ((memes == null || !memes.isHolded) && Vector3.Distance(orb.transform.position, startPos) < 1f)
            yield return null;

        pedestal.Break(GameObject.Find("Player").transform.position);
    }
}
