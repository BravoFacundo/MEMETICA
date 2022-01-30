using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    private GameObject Player;
    void Start()
    {
        Player = GameObject.Find("CameraHolder");
    }

    void Update()
    {
        transform.LookAt(Player.transform);
    }
}
