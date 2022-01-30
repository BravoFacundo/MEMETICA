using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideIslands : MonoBehaviour
{
    public bool Esconder = false;
    public bool unaVez = false;
    public bool unaVez2 = false;

    public bool hide = false;

    void Start()
    {
        
    }

    
    void Update()
    {
        if (Esconder && !unaVez)
        {
            Renderer[] rs = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in rs)
            {
                r.enabled = false;
            }
            Collider[] cs = GetComponentsInChildren<Collider>();
            foreach (Collider c in cs)
            {
                c.enabled = false;
            }
            
            unaVez = true;
            unaVez2 = false;
        } else if (!Esconder && !unaVez2)
        {
            Renderer[] rs = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in rs)
            {
                r.enabled = true;
            }
            Collider[] cs = GetComponentsInChildren<Collider>();
            foreach (Collider c in cs)
            {
                c.enabled = true;
            }
                    

            unaVez2 = true;
            unaVez = false;
        }

        if (hide)
        {
            Hide();
        }
        else
        {
            UnHide();
        }

    }

    public void Hide()
    {
        Esconder = true;
    }
    public void UnHide()
    {
        Esconder = false;
    }

}
