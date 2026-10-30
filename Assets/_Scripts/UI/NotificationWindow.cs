using System.Collections;
using UnityEngine;

// A notice that slides down from above into its place (e.g. the top right corner) with a sound, stays a moment and
// goes back up. Used for "Use double click to open icons": a desktop icon (WindowButton with Double Click) clicked once.
// Goes on the notice's window (your design; its text with Localized for both languages), inside a canvas, placed where
// it should be seen. It can start off or on: it hides itself. Asked again while visible, it sounds again and stays
// longer. One per scene (WindowButton finds it).
public class NotificationWindow : MonoBehaviour
{
    [Header("Sound")]
    public AudioClip sound;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Timing")]
    [Tooltip("Seconds it stays in place")]
    public float showTime = 3f;
    [Tooltip("Seconds it takes to slide in and out")]
    public float slideTime = 0.25f;

    private static NotificationWindow instance;
    private RectTransform rect;
    private Vector2 shownPosition;
    private AudioSource source;
    private bool visible;

    void Awake()
    {
        instance = this;
        rect = (RectTransform)transform;
        shownPosition = rect.anchoredPosition;   // where you placed it
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        rect.anchoredPosition = HiddenPosition;   // it slides in from there
        if (!visible) gameObject.SetActive(false);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // Shows the scene's notice (nothing if it has none)
    public static void Notify()
    {
        if (instance == null) instance = FindObjectOfType<NotificationWindow>(true);
        if (instance != null) instance.Show();
    }

    public void Show()
    {
        visible = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        transform.SetAsLastSibling();   // over the windows
        if (sound != null) source.PlayOneShot(sound, volume);   // every single click
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    // Just above its place, out of the screen's top (its height and a bit)
    Vector2 HiddenPosition => shownPosition + Vector2.up * (rect.rect.height + 40f);

    IEnumerator Run()
    {
        yield return Slide(shownPosition);
        yield return new WaitForSecondsRealtime(showTime);
        yield return Slide(HiddenPosition);
        visible = false;
        gameObject.SetActive(false);
    }

    IEnumerator Slide(Vector2 to)
    {
        Vector2 from = rect.anchoredPosition;
        for (float t = 0f; t < slideTime; t += Time.unscaledDeltaTime)
        {
            rect.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(slideTime, 0.01f)));
            yield return null;
        }
        rect.anchoredPosition = to;
    }
}
