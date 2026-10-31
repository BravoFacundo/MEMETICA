using System.Collections;
using UnityEngine;

// Brings a meme onto this pedestal from the sky: once Elevator docks after its ride, the meme materializes above the
// pedestal (growing in, with Appear Clip and Appear Effect), hangs a moment and drops into the socket. The pedestal
// takes it as if you had placed it (a white one raises its bridge). The tutorial: the intro elevator arrives and the
// meme window falls onto the tutorial pedestal, so its bridge isn't there while you ride up.
// The meme is hidden until then. Without a ride (tutorial skipped, a DebugTools start) it isn't hidden, or it is
// seated on the pedestal at once.
// Goes on the pedestal.
[RequireComponent(typeof(Pedestal))]
public class MemeDelivery : MonoBehaviour
{
    [Tooltip("The meme that falls onto this pedestal (hidden until then)")]
    public Meme meme;
    [Tooltip("When it docks after its ride, the meme comes (the intro elevator)")]
    public Elevator afterElevator;

    [Header("Motion")]
    [Tooltip("Seconds after the elevator docks")]
    public float delay = 0.5f;
    [Tooltip("Meters above the socket where it appears")]
    public float height = 3f;
    [Tooltip("Seconds it takes to grow in")]
    public float appearTime = 0.4f;
    [Tooltip("Seconds it hangs there before falling")]
    public float hangTime = 0.3f;
    [Tooltip("Seconds the fall takes (speeding up)")]
    public float fallTime = 0.5f;

    [Header("Feel")]
    public AudioClip appearClip;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Optional: a particle prefab played where it appears")]
    public ParticleSystem appearEffect;

    private Pedestal pedestal;
    private Vector3 worldScale;
    private bool done;

    void Start()
    {
        pedestal = GetComponent<Pedestal>();
        if (meme == null || afterElevator == null) { Debug.LogWarning($"[MemeDelivery] '{name}': assign Meme and After Elevator.", this); done = true; return; }
        worldScale = meme.transform.lossyScale;
        if (LevelManager.SkippingTutorial) { done = true; return; }   // (LevelManager places it)
        meme.gameObject.SetActive(false);
    }

    void Update()
    {
        if (done || !afterElevator.Docked) return;
        done = true;
        if (afterElevator.RodeIn) StartCoroutine(Deliver());
        else SeatNow();   // docked at once (DebugTools): no show
    }

    void SeatNow()
    {
        Show(pedestal.socket.position);
        pedestal.Receive(meme.gameObject);
    }

    IEnumerator Deliver()
    {
        yield return new WaitForSeconds(delay);
        Vector3 top = pedestal.socket.position + Vector3.up * height;
        Transform t = meme.transform;
        Show(top);

        // Materializes: grows in, a little past its size
        if (appearClip != null) AudioSource.PlayClipAtPoint(appearClip, top, volume);
        if (appearEffect != null)
        {
            ParticleSystem effect = Instantiate(appearEffect, top, Quaternion.identity);
            Destroy(effect.gameObject, effect.main.duration + effect.main.startLifetime.constantMax);
        }
        for (float time = 0f; time < appearTime; time += Time.deltaTime)
        {
            float p = time / appearTime;
            float grow = 1f + 2.7f * Mathf.Pow(p - 1f, 3f) + 1.7f * Mathf.Pow(p - 1f, 2f);   // ease out back
            t.localScale = worldScale * grow;
            t.position = pedestal.socket.position + Vector3.up * height;   // (the island floats)
            yield return null;
        }
        t.localScale = worldScale;
        for (float time = 0f; time < hangTime; time += Time.deltaTime)
        {
            t.position = pedestal.socket.position + Vector3.up * height;
            yield return null;
        }

        // Drops into the socket, speeding up (the pedestal's zone may take it on the way)
        for (float time = 0f; time < fallTime; time += Time.deltaTime)
        {
            if (pedestal.HasMeme) yield break;   // (taken already)
            float p = time / fallTime;
            t.position = pedestal.socket.position + Vector3.up * height * (1f - p * p);
            yield return null;
        }
        if (!pedestal.HasMeme) pedestal.Receive(meme.gameObject);
    }

    // On, still, out of any parent, at its size, there
    void Show(Vector3 position)
    {
        Transform t = meme.transform;
        t.SetParent(null, true);
        t.localScale = worldScale;
        t.position = position;
        meme.gameObject.SetActive(true);
        if (meme.TryGetComponent(out Rigidbody body)) body.isKinematic = true;
    }
}
