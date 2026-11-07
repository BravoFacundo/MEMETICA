using TMPro;
using UnityEngine;

// The virus' countdown on the HUD (top right): a small window, hidden until the countdown starts (GameManager), then
// the time left. The last Warn Seconds the time blinks in Warn Color. Optional: the virus itself (its image).
// Made by Memetica > HUD > Make Virus Timer (select the HUD canvas); replace its look with your design, keep Label.
[RequireComponent(typeof(CanvasGroup))]
public class VirusClock : MonoBehaviour
{
    [Tooltip("The time (TextMeshPro)")]
    public TMP_Text label;
    [Tooltip("Shown with the clock (the virus' image)")]
    public GameObject virus;
    [Tooltip("The last seconds: it blinks")]
    public float warnSeconds = 60f;
    public Color warnColor = new Color(1f, 0.25f, 0.25f);
    [Tooltip("Blinks per second at the end")]
    public float blinkRate = 2f;
    [Tooltip("Seconds it takes to pop in when the countdown starts")]
    public float popTime = 0.25f;

    private CanvasGroup group;
    private Color normal;
    private bool shown;
    private float shownAt;
    private int shownSeconds = -1;   // what the label says now (rewritten only when it changes: no new text every frame)

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (label != null) normal = label.color;
        Show(false);
    }

    void Update()
    {
        GameManager game = GameManager.Instance;
        bool on = game != null && game.Current != GameManager.State.NotStarted;
        if (on != shown) Show(on);
        if (!on) return;

        // Pops in (a little past its size, and settles)
        float p = Mathf.Clamp01((Time.unscaledTime - shownAt) / Mathf.Max(popTime, 0.01f)) - 1f;
        transform.localScale = Vector3.one * (1f + 2.70158f * p * p * p + 1.70158f * p * p);

        if (label == null) return;
        int seconds = Mathf.CeilToInt(Mathf.Max(game.TimeLeft, 0f));
        if (seconds != shownSeconds) { shownSeconds = seconds; label.text = GameManager.Format(seconds); }
        bool warn = game.Running && game.TimeLeft <= warnSeconds;
        bool blinkOn = Mathf.Repeat(Time.unscaledTime * blinkRate, 1f) < 0.5f;
        Color color = warn && blinkOn ? warnColor : normal;
        if (label.color != color) label.color = color;
    }

    void Show(bool on)
    {
        shown = on;
        shownAt = Time.unscaledTime;
        group.alpha = on ? 1f : 0f;
        group.blocksRaycasts = false;   // (nothing to click on it)
        if (virus != null) virus.SetActive(on);
    }
}
