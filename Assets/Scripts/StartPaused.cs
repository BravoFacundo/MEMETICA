using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class StartPaused : MonoBehaviour
{
    private VideoPlayer p;
    void Awake()
    {
        p = gameObject.GetComponent<VideoPlayer>();
        p.Pause();
    }
}
