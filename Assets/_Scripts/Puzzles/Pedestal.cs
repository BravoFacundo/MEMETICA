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

    [Header("Sounds")] // Se asignan en el prefab Pedestal (antes se cargaban desde Resources)
    public AudioClip chargeClip;
    public AudioClip correctClip;
    public AudioClip incorrectClip;
    public AudioClip snapClip;

    [Header("Romperse (solo el pedestal del ascensor)")]
    public AudioClip breakClip;       // golpe al tocar el piso
    public float toppleTime = 0.7f;   // cuánto tarda en volcarse
    private bool broken = false;

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

    // Vuelca el pedestal hacia el lado contrario a 'pushedFrom' y lo deja sin funcionar
    public void Break(Vector3 pushedFrom)
    {
        if (broken) return;
        broken = true;

        foreach (PedestalTrigger trig in GetComponentsInChildren<PedestalTrigger>()) trig.enabled = false;
        foreach (Collider col in GetComponentsInChildren<Collider>()) if (col.isTrigger) col.enabled = false;
        if (pedParticles != null) pedParticles.Stop();

        StartCoroutine(Topple(pushedFrom));
    }

    IEnumerator Topple(Vector3 pushedFrom)
    {
        // Tamaño del pedestal (solo mallas, sin partículas) para encontrar el borde de la base sobre el que gira
        MeshRenderer[] rends = GetComponentsInChildren<MeshRenderer>();
        if (rends.Length == 0) yield break;
        Bounds b = rends[0].bounds;
        foreach (MeshRenderer r in rends) b.Encapsulate(r.bounds);

        Vector3 dir = b.center - pushedFrom; dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        dir.Normalize();
        Vector3 axis = Vector3.Cross(Vector3.up, dir);
        float edge = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
        Vector3 pivot = new Vector3(b.center.x, b.min.y, b.center.z) + dir * edge;

        // Cae acelerando, como con gravedad
        float t = 0f, angle = 0f;
        while (t < toppleTime)
        {
            t += Time.deltaTime;
            float target = 90f * Mathf.Pow(Mathf.Clamp01(t / toppleTime), 2f);
            transform.RotateAround(pivot, axis, target - angle);
            angle = target;
            yield return null;
        }

        if (breakClip != null) AudioSource.PlayClipAtPoint(breakClip, pivot);

        // Rebote chico
        yield return RotateBy(pivot, axis, -6f, 0.08f);
        yield return RotateBy(pivot, axis, 6f, 0.10f);
    }

    IEnumerator RotateBy(Vector3 pivot, Vector3 axis, float degrees, float duration)
    {
        float t = 0f, done = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float target = degrees * Mathf.Clamp01(t / duration);
            transform.RotateAround(pivot, axis, target - done);
            done = target;
            yield return null;
        }
    }
}
