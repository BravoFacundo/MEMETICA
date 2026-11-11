using System.Collections;
using UnityEngine;

// The Menu's entrance: its desktop things (icons, windows…) appear one after another, each one popping in with a
// sound, like an old computer starting up. Goes on Canvas_Desktop (or Managers).
// Elements: drag them in the order they appear. Each one is hidden at the start (left on or off in the scene, it
// doesn't matter) and pops in its turn: from small, a little past its size, and settles, fading in.
// Sound: one of Appear Sounds each time (a little higher each step: Pitch Step), or the element's own Clip.
// A click or a key meanwhile: the rest appear at once (whoever knows the menu doesn't wait).
// Windows that wait for a button (Configuration, Recycle Bin…) don't go here: UIManager > Start Closed.
[DefaultExecutionOrder(-50)]   // (after UIManager arranged everything, before anything shows)
public class MenuIntro : MonoBehaviour
{
    [System.Serializable]
    public class Element
    {
        public GameObject target;
        [Tooltip("Its own sound. Empty: one of Appear Sounds")]
        public AudioClip clip;
        [Tooltip("Extra seconds before this one (on top of Interval): a pause before a window, say")]
        public float extraDelay;
    }

    [Tooltip("In the order they appear")]
    public Element[] elements;
    [Tooltip("Seconds before the first one")]
    public float startDelay = 0.5f;
    [Tooltip("Seconds between one and the next")]
    public float interval = 0.12f;
    [Tooltip("Seconds each one takes to pop in")]
    public float popTime = 0.25f;

    [Header("Sound")]
    public SoundSet appearSounds = new SoundSet();
    [Tooltip("Each one a little higher than the one before (0.03 = +3% each). 0: all the same")]
    [Range(0f, 0.1f)] public float pitchStep = 0.03f;
    [Range(0f, 1f)] public float clipVolume = 0.8f;
    [Tooltip("A click or a key during it: the rest appear at once")]
    public bool skipOnInput = true;

    private CanvasGroup[] groups;
    private Vector3[] scales;
    private AudioSource own;
    private bool skipped;

    void Awake()
    {
        if (elements == null) elements = new Element[0];
        groups = new CanvasGroup[elements.Length];
        scales = new Vector3[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            GameObject g = elements[i]?.target;
            if (g == null) continue;
            groups[i] = g.GetComponent<CanvasGroup>();
            if (groups[i] == null) groups[i] = g.AddComponent<CanvasGroup>();
            scales[i] = g.transform.localScale;
            groups[i].alpha = 0f;
            groups[i].blocksRaycasts = groups[i].interactable = false;
            g.SetActive(false);   // (its turn turns it on)
        }
        own = gameObject.AddComponent<AudioSource>();
        own.playOnAwake = false;
        own.spatialBlend = 0f;
        AudioManager.Route(own);
    }

    void Start() => StartCoroutine(Run());

    void Update()
    {
        if (skipOnInput && !skipped && (Input.anyKeyDown || Input.GetMouseButtonDown(0))) skipped = true;
    }

    IEnumerator Run()
    {
        yield return Wait(startDelay);
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i]?.target == null) continue;
            if (!skipped) yield return Wait(elements[i].extraDelay);
            if (skipped) { Shown(i); continue; }
            StartCoroutine(Pop(i));
            PlaySound(i);
            yield return Wait(interval);
        }
    }

    IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds && !skipped; t += Time.unscaledDeltaTime) yield return null;
    }

    // From small, a little past its size, and back; fading in
    IEnumerator Pop(int i)
    {
        Transform t = elements[i].target.transform;
        elements[i].target.SetActive(true);
        float time = Mathf.Max(popTime, 0.01f);
        for (float e = 0f; e < time && !skipped; e += Time.unscaledDeltaTime)
        {
            float p = e / time;
            float q = p - 1f;
            float k = 1f + 2.70158f * q * q * q + 1.70158f * q * q;   // (past 1 a little, then settles)
            t.localScale = scales[i] * k;
            groups[i].alpha = Mathf.Clamp01(p * 2f);
            yield return null;
        }
        Shown(i);
    }

    // As it is in the scene, clickable
    void Shown(int i)
    {
        GameObject g = elements[i].target;
        if (g == null) return;
        g.SetActive(true);
        g.transform.localScale = scales[i];
        groups[i].alpha = 1f;
        groups[i].blocksRaycasts = groups[i].interactable = true;
    }

    void PlaySound(int i)
    {
        float pitch = 1f + pitchStep * i;
        if (elements[i].clip != null)
        {
            own.pitch = pitch;
            own.PlayOneShot(elements[i].clip, clipVolume);
        }
        else appearSounds.Play(gameObject, pitch);
    }
}
