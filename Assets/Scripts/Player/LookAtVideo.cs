//In the project, there are some paintings that the player needs to look at to be able to solve the puzzles.
//This Script is used to play a video on the painting when the player is looking at it.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class LookAtVideo : MonoBehaviour
{
    [Header("Player Look")]
    public VideoPlayer Pintura;
    public AudioSource Audio;

    public bool orbIsCorrect;
    private bool looking = false;
    public float lookingRange = 10f;

    [Header("Lock bools")]
    private bool lookingLock = false;
    private bool rayHitLock = false;

    void Update()
    {        
        if (looking && !lookingLock)
        {
            //Debug.Log("esta mirando");
            Pintura.Play();
            Audio.Play();
            lookingLock = true;
        }

        if (!looking && lookingLock)
        {
            //Debug.Log("no esta mirando");
            if (!orbIsCorrect)
            {
                Pintura.frame = 0;
                Audio.time = 0;
            }
            Pintura.Pause();
            Audio.Pause();
            lookingLock = false;
        }

        if (!PauseMenu.GameIsPaused)
        {
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out RaycastHit hit, lookingRange))
            {                
                if (hit.transform.CompareTag("Pintura") && hit.transform.GetComponentInChildren<VideoPlayer>() != null)
                {                 
                    if (Pintura != null) Pintura.Play();
                    if (!rayHitLock)
                    {
                        Pintura = hit.transform.GetComponentInChildren<VideoPlayer>();
                        Audio = hit.transform.GetComponentInChildren<AudioSource>();
                        orbIsCorrect = hit.transform.GetComponentInChildren<Atril>().orbInPedestalIsCorrect;
                    }                 
                    looking = true;
                    rayHitLock = true;
                }
            }
            else 
            {
                looking = false;
                rayHitLock = false;
            }
        }
        else if (Pintura !=null) Pintura.Pause();

    }
}
