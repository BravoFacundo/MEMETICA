using TMPro;
using UnityEngine;

// The virus' countdown on the HUD (top right): hidden until the countdown starts (GameManager), then the time left.
// The last Warn Seconds it blinks in Warn Color. Optional: the virus itself (its image) shown with it.
// Goes on a TextMeshPro text in the HUD canvas.
[RequireComponent(typeof(TMP_Text))]
public class VirusClock : MonoBehaviour
{
    [Tooltip("Shown with the clock (the virus' image), hidden before the countdown")]
    public GameObject virus;
    [Tooltip("The last seconds: it blinks")]
    public float warnSeconds = 60f;
    public Color warnColor = new Color(1f, 0.25f, 0.25f);
    [Tooltip("Blinks per second at the end")]
    public float blinkRate = 2f;

    private TMP_Text text;
    private Color normal;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        normal = text.color;
        Show(false);
    }

    void Update()
    {
        GameManager game = GameManager.Instance;
        bool shown = game != null && game.Current != GameManager.State.NotStarted;
        Show(shown);
        if (!shown) return;

        text.text = GameManager.Format(game.TimeLeft);
        bool warn = game.Running && game.TimeLeft <= warnSeconds;
        bool blinkOn = Mathf.Repeat(Time.unscaledTime * blinkRate, 1f) < 0.5f;
        text.color = warn && blinkOn ? warnColor : normal;
    }

    void Show(bool on)
    {
        if (text.enabled != on) text.enabled = on;
        if (virus != null && virus.activeSelf != on) virus.SetActive(on);
    }
}
