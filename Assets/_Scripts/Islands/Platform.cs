using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Platform : MonoBehaviour
{
    private float waitTime;

    public Transform platform;
    public Transform pivotSuperior;
    public Transform pivotInferior;
    public bool onTop = false;
    private bool onTopLock = false;
    public bool fall = false;
    public bool inferiorLock = false;
    public bool superiorLock = false;

    private float wait = 0.04f; //0.04f
    private float returnWait = 2.5f;
    private float speed = 20f;
    private AudioSource platformAudio;
    private AudioClip platformCrack;
    private AudioClip platformBreak;
    private AudioClip platformFall;

    void Start()
    {
        platform = gameObject.transform;
        pivotSuperior = gameObject.transform.parent.Find("PlatformPivotSuperior");
        pivotInferior = gameObject.transform.parent.Find("PlatformPivotInferior");

        //platformAudio = gameObject.GetComponent<AudioSource>();
        //platformCrack = Resources.Load<AudioClip>("Sounds/platformCrack");
        //platformBreak = Resources.Load<AudioClip>("Sounds/platformBreak");
        //platformFall = Resources.Load<AudioClip>("Sounds/platformFall");
        //platformAudio.loop = false;
    }
    
    void Update()
    {
        //Debug.Log("Esta el jugador arriba? " + onTop);
        //Debug.Log("Esta el lock activado? " + onTopLock);
        if (onTop && !onTopLock)
        {
            onTopLock = true;
            StartCoroutine("Fall");
        }
        
        if (fall) //si "caete" es true: Se mueve hacia abajo
        {
            gameObject.transform.position = Vector3.MoveTowards( platform.position,
                new Vector3(platform.position.x, pivotInferior.position.y, platform.position.z), Time.deltaTime * speed);            
        }
        else      //si "caete" es false: Se mueve hacia arriba
        {
            gameObject.transform.position = Vector3.MoveTowards(platform.position,
                new Vector3(platform.position.x, pivotSuperior.position.y, platform.position.z), Time.deltaTime * speed);
        }

        //si la plataforma toca abajo
        if (gameObject.transform.position.y == pivotInferior.position.y )
        {
            StartCoroutine("ReturnTime", returnWait);
        }
        //si la plataforma toca arriba
        if (gameObject.transform.position.y == pivotSuperior.position.y)
        {
            onTopLock = false;
            onTop = false;
        }
        //if (Posicion actual = pivot superior) //activar para bajar
        //if (Posicion actual = pivot inferior) //activar para subir

    }

    public IEnumerator Fall()
    { 
        //platformAudio.clip = platformCrack; platformAudio.Play();
        yield return new WaitForSeconds(wait);
        fall = true;
        //platformAudio.clip = platformBreak; platformAudio.Play();
        //yield return new WaitForSeconds(platformAudio.clip.length);
        //platformAudio.clip = platformFall; platformAudio.Play();
    }
    public IEnumerator ReturnTime(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        fall = false;
    }
    public void Up()
    {
        platformAudio.Stop();
        //Sonido de subida
    }

}
