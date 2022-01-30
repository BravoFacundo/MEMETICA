using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class LookAtVideo : MonoBehaviour
{    
    public bool orbeCorrecto;
    public float range = 10f;
    public VideoPlayer Pintura;
    public AudioSource Audio;

    public bool looking = false;
    public bool lookLock = false;
    public bool hitLock = false;
    
    void Start()
    {

    }

    void Update()
    {        
        if (looking && !lookLock)
        {
            Debug.Log("esta mirando");
            Pintura.Play();
            Audio.Play();
            lookLock = true;
        }

        if (!looking && lookLock)
        {
            Debug.Log("no esta mirando");
            if (!orbeCorrecto)
            {
                Pintura.frame = 0;
                Audio.time = 0;
            }
            Pintura.Pause();
            Audio.Pause();
            lookLock = false;
        }


        if (!PauseMenu.GameIsPaused)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, range))
            {
                
                if (hit.transform.tag == "Pintura" && hit.transform.GetComponentInChildren<VideoPlayer>() != null)
                {                 
                    if (Pintura != null) Pintura.Play();
                    if (!hitLock)
                    {
                        Pintura = hit.transform.GetComponentInChildren<VideoPlayer>();
                        Audio = hit.transform.GetComponentInChildren<AudioSource>();
                        orbeCorrecto = hit.transform.GetComponentInChildren<Atril>().orbInPedestalIsCorrect;
                    }                 
                    looking = true;
                    hitLock = true;
                }
            }
            else 
            {
                looking = false;
                hitLock = false;
            }
        }
        else if (Pintura !=null) Pintura.Pause();

    }


    //recibir desde el script de pedestal si el orbe colocado es correcto o no

    //detectar que el jugador esta mirando con un raycast que avanza 5 metros y solo busca la capa pintura //eventualmente intentar detectar que se este mirando solo de frente

    //Si no hay orbe, La pintura muestra otras referencias al orbe correcto y al dejar de mirar vuelve a la pintura original
    //Si hay un orbe y es correcto, La pintura reproduce los memes y al dejar de mirar se queda en el ultimo meme.

}
