using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProvisionalOrbImageChange : MonoBehaviour
{
    public GameObject Borrado;
    
    void Start()
    {
        
    }

    
    void Update()
    {
        if (Input.GetKeyDown("c"))
        {
            Borrado.SetActive(false);

        }
    }
}
