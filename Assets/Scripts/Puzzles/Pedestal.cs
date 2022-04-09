using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pedestal : MonoBehaviour
{
    [HideInInspector] public GameObject pedTrigger;
    [HideInInspector] public GameObject pedPivot;
    [HideInInspector] public AudioSource pedAudio;
    [HideInInspector] public ParticleSystem pedParticles;

    public GameObject[] Pedestals;
    public GameObject[] LinkedPedestals;

    [Header("Pedestal")]
    public bool KeylessPedestal = false;
    public bool LinkedPedestal = false;
    public float LinkedChecks = 0;

    [Header("ID Variables")]
    public string KeyID;
    public string pedestalID;

    [Header("Control Variables")]
    public string[] ControlledIslands;
    public string[] ControlledBridges;
    public string[] ControlledElevators;

    [Header("Status Variables")]
    public GameObject OrbInPedestal;
    public bool OrbInPedestalisCorrect = false;
    private bool checkLock = false;

    private void Awake()
    {
        pedTrigger = gameObject.transform.Find("PedTrigger").gameObject;
        pedPivot = gameObject.transform.Find("PedPivot").gameObject;
        pedAudio = gameObject.transform.Find("PedAudio").gameObject.GetComponent<AudioSource>();
        pedParticles = gameObject.transform.Find("PedParticles").gameObject.GetComponent<ParticleSystem>();

        if (LinkedPedestal && pedestalID.EndsWith("1"))
        {
            Pedestals = GameObject.FindGameObjectsWithTag("Pedestal");
            for (int i = 0; i < Pedestals.Length; i++)
            {
                if (Pedestals[i].GetComponent<Pedestal>().LinkedPedestal &&
                    Pedestals[i].GetComponent<Pedestal>().pedestalID.StartsWith("5"))
                {
                    //LinkedPedestals = Pedestals[i];
                }
            }
        }
    }

    private void Update()
    {
        if (OrbInPedestalisCorrect && !checkLock)
        {
            if (gameObject.transform.parent.Find("Pintura") != null)
            {
                gameObject.transform.parent.Find("Pintura").GetComponentInChildren<Atril>().orbInPedestalIsCorrect = true;
                checkLock = true;
            }
        }
        else if(!OrbInPedestalisCorrect && checkLock)
        {
            if (gameObject.transform.parent.Find("Pintura") != null)
            {
                gameObject.transform.parent.Find("Pintura").GetComponentInChildren<Atril>().orbInPedestalIsCorrect = false;
                checkLock = false;
            }
        }
    }
}
