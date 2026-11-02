using UnityEngine;
using UnityEngine.UI;

// The monitor's frame, always on: a border drawn over the edges of the game's view (Frame Image). Two tones: OS Grey (#313131, the frame image as it is: matches the
// retro OS windows) or Black (blends with a CRT's glass, the exhibition). The bars of the Classic 4:3 display
// (DisplayManager) take the same tone, so the frame and the bars always match. Goes on Canvas_Frame (the prefab).
[RequireComponent(typeof(Canvas))]
public class FrameEffect : MonoBehaviour
{
    public enum Tone { OSGrey, Black }

    [Tooltip("The border image (inside View): its sprite is #313131. OS Grey shows it as it is, Black darkens it")]
    public Image frameImage;
    [Tooltip("Frame and bars. OS Grey: #313131, like the retro OS windows. Black: blends with a CRT's glass")]
    public Tone tone = Tone.OSGrey;

    static readonly Color OSGrey = new Color32(0x31, 0x31, 0x31, 0xFF);

    private Canvas canvas;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.sortingOrder = 1000;   // over everything, the cursor (999) too: it is the monitor's real edge
    }

    void OnEnable() => Apply();
    void OnValidate() { if (frameImage != null) frameImage.color = ImageTint; }

    Color ImageTint => tone == Tone.Black ? Color.black : Color.white;   // (white: the sprite's own #313131)
    Color BarColor => tone == Tone.Black ? Color.black : OSGrey;


    void Apply()
    {
        if (frameImage != null) frameImage.color = ImageTint;
        DisplayManager.BarColor = BarColor;
    }
}
