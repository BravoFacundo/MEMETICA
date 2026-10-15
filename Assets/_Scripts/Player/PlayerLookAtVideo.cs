//In the project, there are some paintings that the player needs to look at to be able to solve the puzzles.
//This Script is used to play a video on the painting when the player is looking at it.

using UnityEngine;
using UnityEngine.Video;

public class PlayerLookAtVideo : MonoBehaviour
{
    [Header("Player Look At Video")]
    [SerializeField] private float lookingRange = 10f;

    [Header("Debug")]
    [SerializeField] private bool orbIsCorrect;

    private Transform currentPainting;
    private VideoPlayer paintingVideo;
    private AudioSource audioSource;
    private bool wasPaused;

    void Update()
    {
        if (PauseMenu.GameIsPaused)
        {
            if (!wasPaused && paintingVideo != null) paintingVideo.Pause();
            if (!wasPaused && audioSource != null) audioSource.Pause();
            wasPaused = true;
            return;
        }
        if (wasPaused)
        {
            wasPaused = false;
            if (paintingVideo != null) paintingVideo.Play();
            if (audioSource != null) audioSource.Play();
        }

        Transform lookedPainting = GetLookedPainting();
        if (lookedPainting == currentPainting) return;

        StopPainting();
        if (lookedPainting != null) PlayPainting(lookedPainting);
    }

    // Before: looking from a painting to the ground kept the video playing, and a second painting played the first one's video
    private Transform GetLookedPainting()
    {
        if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, lookingRange)) return null;
        if (!hit.transform.CompareTag("Pintura") || hit.transform.GetComponentInChildren<VideoPlayer>() == null) return null;
        return hit.transform;
    }

    private void PlayPainting(Transform painting)
    {
        currentPainting = painting;
        paintingVideo = painting.GetComponentInChildren<VideoPlayer>();
        audioSource = painting.GetComponentInChildren<AudioSource>();
        Atril atril = painting.GetComponentInChildren<Atril>();
        orbIsCorrect = atril != null && atril.IsCorrect;

        paintingVideo.Play();
        if (audioSource != null) audioSource.Play();
    }

    // A solved painting keeps where it was; an unsolved one starts over next time
    private void StopPainting()
    {
        if (paintingVideo != null)
        {
            if (!orbIsCorrect) paintingVideo.frame = 0;
            paintingVideo.Pause();
        }
        if (audioSource != null)
        {
            if (!orbIsCorrect) audioSource.time = 0;
            audioSource.Pause();
        }
        currentPainting = null;
        paintingVideo = null;
        audioSource = null;
    }
}
