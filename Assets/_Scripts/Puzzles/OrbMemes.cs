using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class OrbMemes: MonoBehaviour
{
    public bool isHolded = false;
    public bool isCrossing = false;
    public float distance;
    public GameObject Player;

    public bool changeLock = false;

    private bool canDo1 = true;
    private bool canDo2 = true;
    private bool canDo3 = true;
    private bool canDo4 = true;

    public List<AudioClip> Audios = new List<AudioClip>();
    public List<Material> materials = new List<Material>();
    private float cantidadAud;
    public float cantidadMat;
    public int matCount;
    public float min;
    public float max;

    public AudioSource AudioEmiter;
    public AudioClip Au1;
    public AudioClip Au2;
    public AudioClip Au3;

    void Start()
    {
        Player = GameObject.Find("Player");
        AudioEmiter = this.gameObject.GetComponent<AudioSource>();

        switch (this.gameObject.transform.name)
        {
            case "1Aa":
                cantidadAud = 2;
                //Au1 = Resources.Load<AudioClip>("Memes/Holy Sound 2"); Audios.Add(Au1);
                //Au2 = Resources.Load<AudioClip>("Memes/Hallelujah Chorus 2"); Audios.Add(Au2);

                cantidadMat = 6;
                for (int i = 0; i < cantidadMat; i++)
                {
                    materials.Add(Resources.Load("Memes/Reli/Materials/" +i, typeof(Material)) as Material);
                }
                break;

            case "1Ba":
                cantidadAud = 3;
                //Au1 = Resources.Load<AudioClip>("Memes/The eagle has landed");    Audios.Add(Au1);
                //Au2 = Resources.Load<AudioClip>("Memes/Un gran paso completo");   Audios.Add(Au2);
                //Au3 = Resources.Load<AudioClip>("Memes/Un gran paso incompleto"); Audios.Add(Au3);

                cantidadMat = 5;
                for (int i = 0; i < cantidadMat; i++)
                {
                    materials.Add(Resources.Load("Memes/Consp/Materials/" + i, typeof(Material)) as Material);
                }
                
                break;
            case "3Aa":
                cantidadAud = 3;
                //Au1 = Resources.Load<AudioClip>("Memes/The eagle has landed"); Audios.Add(Au1);
                //Au2 = Resources.Load<AudioClip>("Memes/Un gran paso completo"); Audios.Add(Au2);
                //Au3 = Resources.Load<AudioClip>("Memes/Un gran paso incompleto"); Audios.Add(Au3);

                cantidadMat = 4;
                for (int i = 0; i < cantidadMat; i++)
                {
                    materials.Add(Resources.Load("Memes/Memes/Materials/" + i, typeof(Material)) as Material);
                }
                break;
            case "4Aa":
                cantidadAud = 3;
                //Au1 = Resources.Load<AudioClip>("Memes/The eagle has landed"); Audios.Add(Au1);
                //Au2 = Resources.Load<AudioClip>("Memes/Un gran paso completo"); Audios.Add(Au2);
                //Au3 = Resources.Load<AudioClip>("Memes/Un gran paso incompleto"); Audios.Add(Au3);

                cantidadMat = 4;
                for (int i = 0; i < cantidadMat; i++)
                {
                    materials.Add(Resources.Load("Memes/Fashion/Materials/" + i, typeof(Material)) as Material);
                }
                break;
        }
        matCount = 0;
        gameObject.transform.Find("Meme").GetComponent<MeshRenderer>().material = materials[matCount];

        for (int i = 0; i < cantidadAud; i++)
        {
            
        }
        //Audios.Add();
        //Charge = Resources.Load<AudioClip>("Sounds/PedestalCharge");
    }

    void Update()
    {
        distance = Vector3.Distance(this.gameObject.transform.position, Player.transform.position);
        
        //Imagen
        if (isHolded && isCrossing && !changeLock) //tiene que checkear que el puente y la isla que se tocan no son iguales a las anteriores
        {
            StartCoroutine("Changing", 1f);
            //Resources.Load("Material/Night_Sky.mat", typeof(Material)) as Material;

            changeLock = true;
        }


        //Audio
        if (isHolded && canDo1)
        {
            StartCoroutine("Holding");
            canDo1 = false;
        }
        if (!isHolded && canDo2)
        {
            StartCoroutine("NotHolding");
            canDo2 = false;
        }
        /*if (!isHolded)
        {
            if (distance <= 10f && canDo3) //si es menor o igual a 10 (Se escucha desde esa isla)
            {
                StartCoroutine("Calling");
                canDo3 = false;
            }
            if (distance <= 10f && distance <= 35f && canDo4) //si es menor o igual a 35 (Se escucha en islas a la redonda)
            {
                //StartCoroutine("Calling2");
                canDo4 = false;
            }
        }*/
    }

    public IEnumerator Changing(float waitTime) 
    {
        yield return new WaitForSeconds(waitTime);
        if (isHolded && isCrossing)         
        {
            //Debug.Log("Pisando puente -> cambiar orbe");
            if (matCount != cantidadMat -1)
            {
                //Debug.Log("Esto deberia pasar solo 5 veces");
                matCount += 1;
                gameObject.transform.Find("Meme").GetComponent<MeshRenderer>().material = materials[matCount];
            }
        }

        //efecto bloom y todo blanco la textura del orbe, luego vuelve a la de cristal
        
    }


    public IEnumerator Calling() //Llamar la atencion del usuario //Falta agregar para que no repita el ultimo audio
    {        
        yield return new WaitForSeconds(Random.Range(min,max)); //Debug.Log("Se inicio y espero un tiempo random");

        int index = Random.Range(0, 2 ); // (int)Cantidad - 1 Pick random element from the list //no se si es solo cantidad o cantidad -1 :c
        AudioClip i = Audios[index]; //  i = the number that was randomly picked

        AudioEmiter.loop = false; AudioEmiter.clip = Audios[index]; AudioEmiter.Play(); //  Reproducir ese audio

        //yield return new WaitForSeconds(AudioEmiter.clip.length);
        //Debug.Log("Espero lo que dura el audio");
        //Audios.RemoveAt(index); //  Remove chosen element

        canDo3 = true; //Debug.Log("Se vuelve a activar");
        StopCoroutine("Calling");
    }

    public IEnumerator Holding()
    {
        //StopCoroutine("NotHolding");
        switch (this.gameObject.transform.name) //Activar filtros, Modificar valores (Gravedad, bloom, reverb, etc)
        {
            case "1Aa": //Dios
                //Aumentar el bloom
                this.gameObject.GetComponent<AudioReverbZone>().enabled = true; //efecto reverb

                break;

            case "1Ba": //Luna
                //Baja la gravedad
                
                break;
        }
        //Desmutear el loop de sonidos de holding
        gameObject.transform.Find("Audio").GetComponent<AudioSource>().mute = false;
        gameObject.GetComponent<AudioSource>().mute = true;      

        yield return new WaitForSeconds(1f);
        canDo1 = true;
    }    
    public IEnumerator NotHolding()
    {
        //Las modificaciones vuelven a la normalidad cuando ya no tenes el orbe
        //pero los sonidos se van cuando el pedestal termina de confirmar o denegar

        switch (this.gameObject.transform.name)
        {
            case "1Aa": //Dios
                //Quitar el bloom
                this.gameObject.GetComponent<AudioReverbZone>().enabled = false; //se va el efecto reverb

                break;

            case "1Ba": //Luna
                        //Baja la gravedad

                break;
            case "3Aa": //Internet
                        //Baja la gravedad

                break;
            case "4Aa": //conspiraciones
                        //Baja la gravedad

                break;
        }
        
        gameObject.transform.Find("Audio").GetComponent<AudioSource>().mute = true;
        gameObject.GetComponent<AudioSource>().mute = false;

        if (gameObject.GetComponentInChildren<VideoPlayer>() != null) 
        {
            gameObject.GetComponentInChildren<VideoPlayer>().frame = 0;
            gameObject.GetComponentInChildren<VideoPlayer>().Pause();
        }

        yield return new WaitForSeconds(1f);
        canDo2 = true;
    }
}
