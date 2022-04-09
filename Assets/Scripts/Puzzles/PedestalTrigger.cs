using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PedestalTrigger : MonoBehaviour
{
    private GameObject Pedestal;
    private GameObject[] Pedestals;
    private GameObject pedPivot;

    private IslandManager IslandManager;
    private HoldObjects HoldObjects;

    public GameObject OrbInPedestal;
    public GameObject[] LinkedPedestals;
    public GameObject Linked51;
    public float LinkedChecks;
    public bool puedeSumar = true;

    private string pedestalID;
    private string []ControlledIslands;
    private string []ControlledBridges;
    private string keyID;
    private bool keylessPedestal;
    private bool linkedPedestal;
    private bool orbInPedestal = false;

    private AudioSource pedAudio;
    private AudioClip Charge;
    private AudioClip Correct;
    private AudioClip Incorrect;
    private AudioClip Snap;

    private ParticleSystem pedParticles;

    void Start()
    {
        Pedestal = gameObject.transform.parent.gameObject;
        Pedestals = GameObject.FindGameObjectsWithTag("Pedestal");
        IslandManager = GameObject.Find("Scene Manager").GetComponent<IslandManager>();
        HoldObjects = GameObject.Find("Player").GetComponentInChildren<HoldObjects>();
        

        //Pedestal Variables
        keylessPedestal = Pedestal.GetComponent<Pedestal>().KeylessPedestal;
        linkedPedestal = Pedestal.GetComponent<Pedestal>().LinkedPedestal;
        
        for (int i = 0; i < Pedestals.Length; i++)
        {
            if (Pedestals[i].GetComponent<Pedestal>().pedestalID.EndsWith("1"))
            {
                Linked51 = Pedestals[i];
                LinkedPedestals = Pedestals[i].GetComponent<Pedestal>().LinkedPedestals;
            }
        }

        //ID Variables
        pedestalID = Pedestal.GetComponent<Pedestal>().pedestalID;
        keyID = Pedestal.GetComponent<Pedestal>().KeyID;
        //Control Variables
        ControlledIslands = Pedestal.GetComponent<Pedestal>().ControlledIslands;
        ControlledBridges = Pedestal.GetComponent<Pedestal>().ControlledBridges;
        //Status Variables
        OrbInPedestal = Pedestal.GetComponent<Pedestal>().OrbInPedestal;

        //Pedestal Childs
        pedPivot = Pedestal.GetComponent<Pedestal>().pedPivot;
        pedParticles = Pedestal.GetComponent<Pedestal>().pedParticles;
        pedAudio = Pedestal.GetComponent<Pedestal>().pedAudio;
        //Audio Sources
        Charge = Resources.Load<AudioClip>("Sounds/PedestalCharge");
        Correct     = Resources.Load<AudioClip>("Sounds/PedestalWin");
        Incorrect    = Resources.Load<AudioClip>("Sounds/PedestalLoss");
        Snap = Resources.Load<AudioClip>("Sounds/PedestalSnap");
    }

    public void Update()
    {
        
    }


    private void OnTriggerEnter(Collider target) //evaluar bien cuando pasa cada cosa y bajo que condiciones.
    {
        if (target.tag == "Meme" && !orbInPedestal)
        {
            OrbSnap(target.transform.gameObject); pedAudio.clip = Snap; pedAudio.Play();
            StartCoroutine("CheckOrb", target.transform.gameObject);
        }
    }

    IEnumerator CheckOrb(GameObject orbe)
    {        
        //OrbSnap(orbe); pedAudio.clip = Snap; pedAudio.Play();
        yield return new WaitForSeconds(pedAudio.clip.length);
        if (!linkedPedestal)
        {
            pedAudio.loop = false;
            pedAudio.clip = Charge; pedAudio.Play();
            if (!keylessPedestal) //SI EL PEDESTAL NECESITA LLAVE
            {
                yield return new WaitForSeconds(pedAudio.clip.length);
                if (orbe.name.StartsWith(keyID))
                {
                    CorrectKey();
                }              
                else if (orbe.name.StartsWith(keyID) == false)
                {
                    IncorrectKey(); 
                    OrbReject();
                }
            }
            else if (keylessPedestal) CorrectKey();
        }
        else if (linkedPedestal)
        {
            pedAudio.loop = true;
            pedAudio.clip = Charge; pedAudio.Play();
            if (keylessPedestal) //SI EL PEDESTAL NO NECESITA LLAVE
            {
                yield return new WaitForSeconds(pedAudio.clip.length);
                for (int i = 0; i < LinkedPedestals.Length; i++) //este for esta contando los linked pedestals de el pedestal 51, deberia contar todos
                {                    
                    if (LinkedPedestals[i].GetComponent<Pedestal>().OrbInPedestal && puedeSumar)
                    {
                        Linked51.GetComponent<Pedestal>().LinkedChecks += 1;
                        puedeSumar = false;
                    }
                }
                if (Linked51.GetComponent<Pedestal>().LinkedChecks == LinkedPedestals.Length)
                {
                    if (OrbInPedestal != null)
                    {
                        for (int i = 0; i < LinkedPedestals.Length; i++)
                        {
                            LinkedPedestals[i].GetComponentInChildren<PedestalTrigger>().pedAudio.loop = false;
                            LinkedPedestals[i].GetComponentInChildren<PedestalTrigger>().pedAudio.clip = Correct;
                            LinkedPedestals[i].GetComponentInChildren<PedestalTrigger>().pedAudio.Play();
                        }
                    }
                    IslandManager.SubirIsla("0");
                }                
            }
        }
    }

    private void OnTriggerExit(Collider target)
    {
        if (target.tag == "Meme")
        {
            
            if (OrbInPedestal != null && target.transform.gameObject == OrbInPedestal)
            {
                orbInPedestal = false;
                OrbInPedestal = null; Pedestal.GetComponent<Pedestal>().OrbInPedestal = null;
                StopCoroutine("CheckOrb");
                pedAudio.loop = false; pedAudio.clip = Incorrect; pedAudio.Play();
                Pedestal.GetComponent<Pedestal>().OrbInPedestalisCorrect = false;
            }

            if (linkedPedestal )
            {
                if (keylessPedestal)
                {
                    for (int i = 0; i < LinkedPedestals.Length; i++) //este for esta contando los linked pedestals de el pedestal 51, deberia contar todos
                    {
                        if (LinkedPedestals[i].GetComponent<Pedestal>().OrbInPedestal == null && puedeSumar == false)
                        {
                            Linked51.GetComponent<Pedestal>().LinkedChecks -= 1;
                            puedeSumar = true;
                        }
                    }
                }
            }
            
            if (!keylessPedestal)
            {
                if (target.name.StartsWith(keyID))
                {
                    for (int i = 0; i < ControlledBridges.Length; i++)
                        IslandManager.BajarPuente(ControlledBridges[i]); //Debug.Log("se baja: Puente " + ControlledBridges[i]);
                }
            }
            else
            {
                for (int i = 0; i < ControlledBridges.Length; i++)
                    IslandManager.BajarPuente(ControlledBridges[i]); //Debug.Log("se baja: Puente " + ControlledBridges[i]);           
            } 
        }
    }

    public void CorrectKey()
    {
        //GameObject.Find(OrbInPedestal.ToString()).GetComponent<Memes>().StopCoroutine("Holding");

        Pedestal.GetComponent<Pedestal>().OrbInPedestalisCorrect = true;

        if (OrbInPedestal != null)
        {
            pedAudio.clip = Correct; pedAudio.Play();
        }

        for (int i = 0; i < ControlledIslands.Length; i++)
            IslandManager.SubirIsla(ControlledIslands[i]); //Debug.Log("se levanta: Area " + ControlledIslands[i]);

        for (int i = 0; i < ControlledBridges.Length; i++)
            IslandManager.SubirPuente(ControlledBridges[i]); //Debug.Log("se levanta: Puente " + ControlledBridges[i]);
    }
    public void IncorrectKey()
    {
        if (OrbInPedestal != null)
        {
            pedAudio.clip = Incorrect; pedAudio.Play();
        }

        for (int i = 0; i < ControlledBridges.Length; i++)
            IslandManager.BajarPuente(ControlledBridges[i]); //Debug.Log("se baja: Puente " + ControlledBridges[i]);
    }

    public void OrbSnap(GameObject orbe) 
    {     
        HoldObjects.SnapObject(orbe);
        orbe.transform.position = pedPivot.transform.position;
        pedParticles.Play();

        OrbInPedestal = orbe; Pedestal.GetComponent<Pedestal>().OrbInPedestal = orbe;
        orbInPedestal = true;
    }
    public void OrbReject() 
    {        
        if (OrbInPedestal != null)
        {
            Rigidbody rb; rb = OrbInPedestal.GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.None;
            rb.isKinematic = false;
            rb.AddForce(transform.forward * 100f); //luego hacer random entre valores
            rb.AddForce(transform.up * 300f); //cambiar 300f por algun valor almacenado en algun manager 
        }
    }
    public void OrbTaken() 
    {
        if (HoldObjects.FueAgarrada && HoldObjects.heldObject == null)
        {

        }

        IncorrectKey();
    }

}

//       ((((
//      ((((
//       ))))
//    _.-- -.     __ 
//   ( |`---'|  <(o )___
//    \|     |   ( ._> /
//    : .___, :   `---'  
//     `-----'
