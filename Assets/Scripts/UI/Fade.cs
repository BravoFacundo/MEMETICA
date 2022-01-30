using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Fade : MonoBehaviour
{
    public Image Tuto;

    public void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Tuto = GameObject.Find("Fade").GetComponent<Image>();
            Tuto.gameObject.SetActive(false);
            GameObject.Find("Soltar").SetActive(false);
            Camera.main.GetComponent<FPPlayerLook>().canLook = true;
            GameObject.Find("Player").GetComponent<FPPlayerMove>().canMove = true;
        }
    }
}
