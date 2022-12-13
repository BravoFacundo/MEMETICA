using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartTutorialScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerLook playerLook;

    private void Start()
    {
        GetComponent<Image>().enabled = true;
    }
    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            gameObject.SetActive(false);

            playerLook.playerCanLook = true;
            playerMove.playerCanMove = true;
        }
    }
}
