using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Island : MonoBehaviour
{
    private IslandsManager islandManager;
    private GameObject estaIsla;
    private float upSpeed;
    private float fallSpeed;
    public bool islaSube;
    public bool islasBajan;
    public bool islaIsMoving;

    private Transform islaPos;
    private Vector3 masterpivot;
    private Vector3 upperpivot;
    private Vector3 limiteCaida;
    private Vector3 lowerpivot;

    private CameraShake camShake;
    private Coroutine shakeRoutine;

    void Start()
    {
        islandManager = GameObject.Find("Scene Manager").GetComponent<IslandsManager>();
        estaIsla = gameObject;
        upSpeed = islandManager.speed;
        fallSpeed = islandManager.fallSpeed;

        islaPos = estaIsla.transform;
        masterpivot = GameObject.Find("MasterPivot").transform.position;
        upperpivot = new Vector3(islaPos.position.x, masterpivot.y, islaPos.position.z);
        limiteCaida = GameObject.Find("LimiteCaida").transform.position;
        lowerpivot = new Vector3(islaPos.position.x, limiteCaida.y, islaPos.position.z);

        camShake = Camera.main.GetComponent<CameraShake>();
    }


    void Update()
    {
        if (islaSube)
        {
            if (islaPos.position != upperpivot)
            {
                islaPos.position = Vector3.MoveTowards(islaPos.position, upperpivot, Time.deltaTime * upSpeed);
                islaIsMoving = true;
            }
            else islaIsMoving = false;
        }
        if (!islaSube && islasBajan)
        {
            islaPos.position = Vector3.MoveTowards(islaPos.position, new Vector3(islaPos.position.x, islaPos.position.y -1, islaPos.position.z), Time.deltaTime * fallSpeed);
        }
        
        // Un solo temblor por movimiento: arranca cuando la isla empieza a moverse y se apaga solo al llegar.
        if (islaIsMoving && shakeRoutine == null)
            shakeRoutine = StartCoroutine(ShakeCamera());

    }

    public IEnumerator ShakeCamera()
    {
        yield return new WaitForSeconds(0.2f);
        while (islaIsMoving)
            yield return camShake.ShakeTheCamera(0.1f, 0.1f);   // temblores en fila, nunca superpuestos
        shakeRoutine = null;
    }
}
