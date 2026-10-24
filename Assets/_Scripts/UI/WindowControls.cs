using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The buttons of the meme window: where each one is on its frame PNG and what it does. Make UI Meme Window copies this
// list to its HUD version (HudWindow: real UI buttons). On the 3D window in the world they only work with World
// Buttons on (free cursor, Ctrl held: RetroCursor); off by default:
//   Minimize / Close   in the hand: let go of it. In the world: nothing
//   Resize             only on the HUD version (maximize)
//   Locked             does nothing: the forbidden cursor and Denied Clip
// Button areas in the picture's pixels, as Figma gives them: x, y from its top left corner, width, height
// (the yellow frames in the Scene view). Goes on the window's root.
public class WindowControls : MonoBehaviour
{
    public static readonly List<WindowControls> All = new List<WindowControls>();

    public enum Action { Minimize, Resize, Close, Locked }

    [System.Serializable]
    public class Button
    {
        public string name = "Close";
        [Tooltip("In the picture's pixels from its top left: x, y, width, height (as in Figma)")]
        public Rect area = new Rect(0, 0, 32, 32);
        public Action action = Action.Close;
        [Tooltip("Its click sound (empty: none, Denied Clip if it is locked)")]
        public AudioClip sound;
    }

    [Tooltip("The quad whose picture has the buttons (its texture gives the pixel size)")]
    public Transform picture;
    public List<Button> buttons = new List<Button>();
    [Tooltip("A locked button, or something that can't be done")]
    public AudioClip deniedClip;

    [Tooltip("Its buttons also work on the 3D window in the world (free cursor). Off: only on its HUD version")]
    public bool worldButtons = false;

    private Meme meme;
    private Vector2 pixels = new Vector2(1024, 1024);
    private AudioSource source;

    public bool Clickable => worldButtons && isActiveAndEnabled && picture != null && picture.gameObject.activeInHierarchy
                             && (meme == null || meme.CurrentPedestal == null && (MemeDock.Instance == null || !MemeDock.Instance.OnScreen(meme)));   // on the screen: its HUD version's buttons

    void Awake()
    {
        meme = GetComponent<Meme>();
        if (picture == null && transform.childCount > 0) picture = transform.GetChild(0);
        pixels = PictureSize(picture);
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    //---- What the cursor asks (RetroCursor)

    public CursorState Hover(Vector2 uv)
    {
        Button button = At(uv);
        if (button != null) return Works(button) ? CursorState.Point : CursorState.NotAllowed;
        return CursorState.Default;
    }

    public void Click(Vector2 uv)
    {
        Button button = At(uv);
        if (button == null) return;
        if (!Works(button)) { Play(button.sound != null ? button.sound : deniedClip); return; }

        Play(button.sound);
        switch (button.action)
        {
            case Action.Minimize:
            case Action.Close: LetGo(); break;
        }
    }

    bool Works(Button button)
    {
        switch (button.action)
        {
            case Action.Minimize:
            case Action.Close: return meme != null && Look != null && Look.heldObject == meme.gameObject;
            default: return false;
        }
    }

    PlayerLook Look => Camera.main != null ? Camera.main.GetComponent<PlayerLook>() : null;

    void LetGo() => Look.DropObject();

    //---- Where the buttons are

    // The button under that point of the picture (uv: 0,0 bottom left), or null
    Button At(Vector2 uv)
    {
        var pixel = new Vector2(uv.x * pixels.x, (1f - uv.y) * pixels.y);   // from the top left, as in Figma
        foreach (Button b in buttons) if (b.area.Contains(pixel)) return b;
        return null;
    }

    static Vector2 PictureSize(Transform picture)
    {
        Texture texture = picture != null && picture.TryGetComponent(out Renderer r) && r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null;
        return texture != null ? new Vector2(texture.width, texture.height) : new Vector2(1024, 1024);
    }

    void Play(AudioClip clip) { if (clip != null) source.PlayOneShot(clip); }

#if UNITY_EDITOR
    // Each button's area on the picture, with its name
    void OnDrawGizmosSelected()
    {
        if (picture == null) return;
        Vector2 size = PictureSize(picture);
        Gizmos.matrix = picture.localToWorldMatrix;
        foreach (Button b in buttons)
        {
            Gizmos.color = b.action == Action.Locked ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.2f);
            var center = new Vector3((b.area.center.x / size.x) - 0.5f, 0.5f - (b.area.center.y / size.y), 0f);
            Gizmos.DrawWireCube(center, new Vector3(b.area.width / size.x, b.area.height / size.y, 0f));
            UnityEditor.Handles.Label(picture.TransformPoint(center), b.name);
        }
    }
#endif
}
