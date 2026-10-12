using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevator : MonoBehaviour
{
    private LevelManager level;
    private GameObject esteElevador;
    private float upSpeed = 20f; 
    private float fallSpeed; 
    public bool elevadorSube; 
    public bool oneLock = false; 

    private Transform elevadorPos;
    private Vector3 upperpivot;
    public  Vector3 lowerpivot;
    private Vector3 offsetVector;
    public float offset;

    void Start()
    {
        level = LevelManager.Instance;
        esteElevador = gameObject;
        fallSpeed = level.fallSpeed;

        elevadorPos = esteElevador.transform;
        // Alturas desde el LevelManager (antes: objetos MasterPivot y LimiteCaida)
        upperpivot = new Vector3(elevadorPos.position.x, level.topY, elevadorPos.position.z);
        lowerpivot = new Vector3(elevadorPos.position.x, level.bottomY, elevadorPos.position.z);
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
            if (level.elevatorZLocks != null) level.elevatorZLocks.SetActive(false);

            // El jugador y el orbe que viajaron llegan al Área 1 sin pisarla: se les fija ahí el respawn
            GameObject island1 = level.GetIsland("1");
            Transform island1Respawn = level.RespawnPointOf(island1);
            GameObject.Find("Player").GetComponent<PlayerRespawn>().SetRespawn(island1Respawn);
            GameObject orbeAscensor = GameObject.Find("1Ba");
            if (orbeAscensor != null && orbeAscensor.TryGetComponent(out PlayerRespawn orbRespawn))
                orbRespawn.SetRespawn(island1Respawn);
            if (island1 != null) transform.SetParent(island1.transform);   // el ascensor queda como parte del Área 1

            StartCoroutine(BreakPedestalWhenOrbTaken());

            oneLock = true;
        }
    }

    // Ya arriba: cuando el jugador saca el orbe del pedestal del ascensor, el pedestal se vuelca y queda roto
    IEnumerator BreakPedestalWhenOrbTaken()
    {
        GameObject orb = GameObject.Find("1Ba");
        GameObject pedestalObj = level.elevatorZPedestal;
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
