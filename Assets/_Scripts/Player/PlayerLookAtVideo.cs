//In the project, there are some paintings that the player needs to look at to be able to solve the puzzles.
//This Script is used to play a video on the painting when the player is looking at it.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class PlayerLookAtVideo : MonoBehaviour
{
    private VideoPlayer paintingVideo;
    private AudioSource audioSource;

    [Header("Player Look At Video")]
    [SerializeField] private bool orbIsCorrect;
    [SerializeField] private float lookingRange = 10f;
    private bool looking = false;

    [Header("Lock bools")]
    private bool lookingLock = false;
    private bool rayHitLock = false;

    void Update()
    {        
        if (looking && !lookingLock)
        {
            paintingVideo.Play();
            audioSource.Play();
            lookingLock = true;
        }

        if (!looking && lookingLock)
        {
            if (!orbIsCorrect)
            {
                paintingVideo.frame = 0;
                audioSource.time = 0;
            }
            paintingVideo.Pause();
            audioSource.Pause();
            lookingLock = false;
        }

        if (!PauseMenu.GameIsPaused)
        {
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out RaycastHit hit, lookingRange))
            {                
                if (hit.transform.CompareTag("Pintura") && hit.transform.GetComponentInChildren<VideoPlayer>() != null)
                {                 
                    if (paintingVideo != null) paintingVideo.Play();
                    if (!rayHitLock)
                    {
                        paintingVideo = hit.transform.GetComponentInChildren<VideoPlayer>();
                        audioSource = hit.transform.GetComponentInChildren<AudioSource>();
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
        else if (paintingVideo != null) paintingVideo.Pause();
    }
}
