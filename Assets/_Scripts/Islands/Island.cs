using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Island : MonoBehaviour
{
    private LevelManager level;
    private GameObject estaIsla;
    private float upSpeed;
    private float fallSpeed;
    public bool islaSube;
    public bool islasBajan;
    public bool islaIsMoving;

    private Transform islaPos;
    private Vector3 upperpivot;
    private Vector3 lowerpivot;

    private PlayerLook camShake;
    private Coroutine shakeRoutine;

    void Start()
    {
        level = LevelManager.Instance;
        estaIsla = gameObject;
        upSpeed = level.speed;
        fallSpeed = level.fallSpeed;

        islaPos = estaIsla.transform;
        // Alturas desde el LevelManager (antes: objetos MasterPivot y LimiteCaida)
        upperpivot = new Vector3(islaPos.position.x, level.topY, islaPos.position.z);
        lowerpivot = new Vector3(islaPos.position.x, level.bottomY, islaPos.position.z);

        camShake = Camera.main.GetComponent<PlayerLook>();
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
