using UnityEngine;
using UnityEngine.Video;

// Shows the first frame of the video instead of black, without playing it (before: VideoStartPaused).
// Pause() on a stopped VideoPlayer prepares it and leaves the first frame visible.
[RequireComponent(typeof(VideoPlayer))]
public class VideoFirstFrame : MonoBehaviour
{
    void Awake()
    {
        GetComponent<VideoPlayer>().Pause();
    }
}
