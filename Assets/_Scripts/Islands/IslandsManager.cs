using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandsManager : MonoBehaviour
{
    public Collider[] cols;

    [Header("VARIABLES")]
    public float speed = 5;
    public float fallSpeed = 6;
    private Transform LimiteCaida;

    [Header("ISLAS PRIMARIAS")]
    public Transform Isla1; 
    public Transform Isla2;
    public Transform Isla3;
    public Transform Isla4;
    public Transform Isla5;
    public Transform Isla6;
    public Transform IslaTuto;

    [Header("ISLAS SECUNDARIAS")]    
    public Transform IslaA;
    public Transform IslaB;
    public Transform IslaC;
    public Transform IslaD;

    [Header("ISLAS ELEVADORES")]
    public Transform ElevadorZ;
    public Transform ElevadorX;

    [Header("PUENTES PRIMARIOS")] 
    public Transform Puente1;
    public Transform Puente2;
    public Transform Puente3;
    public Transform Puente4;
    public Transform Puente5;
    public Transform Puente6;

    public Transform PuenteZ;

    [Header("PUENTES SECUNDARIOS")]
    public Transform PuenteA;
    public Transform PuenteB;
    public Transform PuenteC;

    [Header("BOOLEANAS DE ACTIVACION")]
    public bool IslasBajan = false;
    public bool elevatorLock = false;

    public GameObject[] islands;
    public GameObject[] bridges;

    void Start()
    {
        LimiteCaida = GameObject.Find("LimiteCaida").transform; //Transform del limite de caida de los puentes 
        
        IslaTuto = GameObject.Find("AreaTuto").transform;
        Isla1 = GameObject.Find("Area1").transform;    
        Isla2 = GameObject.Find("Area2").transform;    
        Isla3 = GameObject.Find("Area3").transform;    
        Isla4 = GameObject.Find("Area4").transform;    
        Isla5 = GameObject.Find("Area5").transform;    
        Isla6 = GameObject.Find("Area6").transform;  

        IslaA = GameObject.Find("AreaA").transform;    
        IslaB = GameObject.Find("AreaB").transform;    
        IslaC = GameObject.Find("AreaC").transform;
        //IslaD = GameObject.Find("AreaD").transform;

        ElevadorZ = GameObject.Find("ElevatorZ").transform;
        ElevadorX = GameObject.Find("ElevatorX").transform;

        Puente1 = GameObject.Find("Puente1").transform;   
        Puente2 = GameObject.Find("Puente2").transform;   
        Puente3 = GameObject.Find("Puente3").transform;
        Puente4 = GameObject.Find("Puente4").transform;
        Puente5 = GameObject.Find("Puente5").transform;
        Puente6 = GameObject.Find("Puente6").transform;

        PuenteA = GameObject.Find("PuenteA").transform;
        //PuenteB = GameObject.Find("PuenteB").transform;
        PuenteC = GameObject.Find("PuenteC").transform;

        PuenteZ = GameObject.Find("PuenteZ").transform;

        islands = GameObject.FindGameObjectsWithTag("Isla");
        bridges = GameObject.FindGameObjectsWithTag("Puente");
    }

    public IEnumerator ShakeCamera()
    {
        yield return new WaitForSeconds(0.2f);
        StartCoroutine(Camera.main.GetComponent<CameraShake>().ShakeTheCamera(0.1f, .1f));
        yield return null;
    }

    public IEnumerator GameComplete()
    {
        GameObject.Find("Player").GetComponent<PlayerMove>().ascend = true;
        //GameObject.Find("White").GetComponent<Color>().a = 1; //falta el fade progresivo a blanco

        //Primero caen los puentes randomente en intervalos de unos segundos, luego las islas
        //Al caer se van ocultando antes de llegar a donde frenan, de ser posible con un shader que las va haciendo invisibles
        for (int i = 0; i < islands.Length; i++)
        {
            if (islands[i].GetComponent<Island>() != null)
            {
                islands[i].GetComponent<Island>().islasBajan = true;
                islands[i].GetComponent<Island>().islaSube  = false;
                //IslaisMoving = true; //Igual creo que no hace falta porque se puede checkear desde los scripts individuales
            }
        }
        yield return new WaitForSeconds(10f);
        GameObject.Find("Canvas").GetComponent<PauseMenu>().QuitGame();
    }

    void Update()
    {   
        //if (Input.GetKeyDown("o")) StartCoroutine("GameComplete");
    }

    public void SubirIsla(string ControlledIslands) //cambiar por switch cases
    {        
        switch (ControlledIslands)
        {
            case "2":
                Isla2.GetComponent<Island>().islaSube = true;
                break;
            case "3":
                Isla3.GetComponent<Island>().islaSube = true;
                break;
            case "4":
                Isla4.GetComponent<Island>().islaSube = true;
                break;
            case "5":
                Isla5.GetComponent<Island>().islaSube = true;
                break;
            case "6":
                Isla6.GetComponent<Island>().islaSube = true;
                break;

            case "A":
                IslaA.GetComponent<Island>().islaSube = true;
                break;
            case "B":
                IslaB.GetComponent<Island>().islaSube = true;
                break;
            case "C":
                IslaC.GetComponent<Island>().islaSube = true;
                break;

            case "0":
                StartCoroutine("GameComplete");
                break;

            case "Z":
                //This case only can happend once
                if (!elevatorLock)
                {
                    ElevadorZ.GetComponent<Elevator>().elevadorSube = true;
                    GameObject.Find("1Ba").transform.SetParent(ElevadorZ);
                    GameObject.Find("Player").GetComponent<PlayerMove>().playerCanMove = false;
                    GameObject.Find("Player").transform.SetParent(ElevadorZ);
                    Camera.main.GetComponent<PlayerHoldObjects>().canHold = false;
                    cols = GameObject.Find("ElevatorLocks").GetComponentsInChildren<Collider>();
                    for (int i = 0; i < cols.Length; i++)
                    {
                        cols[i].enabled = true;
                    }
                    GameObject.Find("Cage").GetComponent<Collider>().enabled = false;
                    GameObject.Find("PedestalElevador").GetComponentInChildren<SphereCollider>().enabled = false; //Desactiva el pedestal (apagando el collider trigger)
                    elevatorLock = true;
                }          

                //Transform respawnPos = GameObject.Find("Respawn Pivot").GetComponent<Transform>();
                //respawnPos = GameObject.Find("PivotArea1").GetComponent<Transform>();

                //queda un error que si el jugador se cae del elevador (que ya lelgo a la isla 1) sin tocar la isla 1 entonces respawnea abajo

                break;
            case "X":
                ElevadorX.GetComponent<Elevator>().elevadorSube = true;
                //congelar al player
                break;
        }
    }
    public void SubirPuente(string ControlledBridges)
    {
        switch (ControlledBridges)
        {
            case "1":
                Puente1.GetComponent<Bridge>().bridgeGoUp = true;
                break;
            case "2":
                Puente2.GetComponent<Bridge>().bridgeGoUp = true;
                break;
            case "3":
                Puente3.GetComponent<Bridge>().bridgeGoUp = true;
                break;
            case "4":
                Puente4.GetComponent<Bridge>().bridgeGoUp = true;
                break;
            case "5":
                Puente5.GetComponent<Bridge>().bridgeGoUp = true;
                break;
            case "6":
                Puente6.GetComponent<Bridge>().bridgeGoUp = true;
                break;

            case "A":
                PuenteA.GetComponent<Bridge>().bridgeGoUp = true;
                break;
            case "B":
                PuenteB.GetComponent<Bridge>().bridgeGoUp = true;
                break;
            case "C":
                PuenteC.GetComponent<Bridge>().bridgeGoUp = true;
                break;

            case "Z":
                PuenteZ.GetComponent<Bridge>().bridgeGoUp = true;
                break;
        }
    }
    public void BajarPuente(string ControlledBridges)
    {
        switch (ControlledBridges)
        {
            case "1":
                Puente1.GetComponent<Bridge>().bridgeGoUp = false;
                break;
            case "2":
                Puente2.GetComponent<Bridge>().bridgeGoUp = false;
                break;
            case "3":
                Puente3.GetComponent<Bridge>().bridgeGoUp = false;
                break;
            case "4":
                Puente4.GetComponent<Bridge>().bridgeGoUp = false;
                break;
            case "5":
                Puente5.GetComponent<Bridge>().bridgeGoUp = false;
                break;
            case "6":
                Puente6.GetComponent<Bridge>().bridgeGoUp = false;
                break;

            case "A":
                PuenteA.GetComponent<Bridge>().bridgeGoUp = false;
                break;
            case "B":
                PuenteB.GetComponent<Bridge>().bridgeGoUp = false;
                break;
            case "C":
                PuenteC.GetComponent<Bridge>().bridgeGoUp = false;
                break;

            case "Z":
                PuenteZ.GetComponent<Bridge>().bridgeGoUp = false;
                break;

        }
    }
}