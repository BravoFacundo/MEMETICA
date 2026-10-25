using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The meme window's version on the HUD canvas (UI): what you see while you carry it. The 3D one (MemeWindow) flies
// toward this place and, a moment before arriving, hides and this one shows instead; letting go, this one hides and
// the 3D one comes out of its place and falls (MemeDock). The 3D one tells it what to show: its meme, empty, the
// change's cover (the meme fades) and flash.
// Its buttons are UI buttons (WindowButton) calling here: Minimize and Close let go of it, Resize maximizes it to
// Max Reference (any rect on the canvas, e.g. MaxReference: only its place and height are used, it is hidden while
// playing; the window keeps its proportion) and back, Denied is a locked button. Let go while maximized: the 3D one
// comes out that big, and this one is normal again.
// Made by Memetica > HUD > Make UI Meme Window (select the HUD canvas). Hierarchy:
//   MemeWindow (this, CanvasGroup)            its rect = the whole frame PNG
//   ├── Background (RawImage)                 the empty window's background, behind the hole
//   │   └── Margin
//   │       └── Meme (RawImage, AspectRatioFitter) the whole meme, as big as it fits
//   │           └── Flash (RawImage, white)       the flash after a change
//   ├── Frame (RawImage)                      your frame PNG
//   └── Buttons (WindowButton each)
[RequireComponent(typeof(CanvasGroup))]
public class HudWindow : MonoBehaviour
{
    public static HudWindow Instance { get; private set; }
    public static HudWindow Dex { get; private set; }

    [Tooltip("The optional memes' place on the HUD (DexCollectible, e.g. MemeWindow_Dex), not your meme window's")]
    public bool forDex;
    public RawImage meme;
    public AspectRatioFitter fitter;
    public Graphic flash;

    [Section("Maximize")]
    [Tooltip("Where and how tall it is maximized (any rect on the canvas, e.g. MaxReference). Hidden while playing")]
    public RectTransform maxReference;
    public float resizeTime = 0.18f;
    [Tooltip("A locked button")]
    public AudioClip deniedClip;

    public bool IsShown { get; private set; }

    // For Dex: its Minimize or Close button (the optional meme showing sinks away right then)
    public System.Action closed;

    private CanvasGroup group;
    private RectTransform rect;
    private Vector3 normalPosition;   // where its pivot is when normal (world: the canvas' space)
    private bool maximized;
    private Coroutine resizing;
    private AudioSource source;

    void Awake()
    {
        if (forDex) Dex = this; else Instance = this;
        group = GetComponent<CanvasGroup>();
        rect = (RectTransform)transform;
        if (maxReference != null)
            foreach (Graphic g in maxReference.GetComponentsInChildren<Graphic>(true)) g.enabled = false;   // only a reference
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        Show(false);
    }

    //---- Shown or not (MemeDock)

    public void Show(bool on)
    {
        IsShown = on;
        if (!on) SetMaximized(false, true);   // next time it is normal again
        group.alpha = on ? 1f : 0f;
        group.blocksRaycasts = on;
        group.interactable = on;
    }

    // Down off the bottom of the screen, then hidden and back in its place (an optional meme, gone: DexCollectible)
    public IEnumerator SinkAway(float seconds)
    {
        Vector3 from = rect.position;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float drop = corners[1].y + 20f;   // (Overlay canvas: screen pixels) its top below the bottom edge
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float p = t / Mathf.Max(seconds, 0.01f);
            rect.position = from + Vector3.down * drop * p * p;   // speeding up
            yield return null;
        }
        Show(false);
        rect.position = from;
    }

    // Where it is on the screen: its middle and height, 0-1 of the screen
    public void ViewportRect(out Vector2 center, out float height)
    {
        var corners = new Vector3[4];
        ((RectTransform)transform).GetWorldCorners(corners);   // Overlay canvas: screen pixels
        center = new Vector2((corners[0].x + corners[2].x) * 0.5f / Screen.width, (corners[0].y + corners[2].y) * 0.5f / Screen.height);
        height = (corners[2].y - corners[0].y) / Screen.height;
    }

    //---- What it shows (MemeWindow)

    public void SetTexture(Texture texture)
    {
        meme.texture = texture;
        meme.enabled = texture != null;
        if (texture != null && fitter != null) fitter.aspectRatio = (float)texture.width / Mathf.Max(texture.height, 1);
    }

    public void SetEmpty() => meme.enabled = false;

    // A change's cover: the meme fades out (0 clear, 1 gone)
    public void SetCover(float amount)
    {
        Color c = meme.color;
        c.a = 1f - amount;
        meme.color = c;
    }

    public void SetGlow(float amount)
    {
        if (flash != null) flash.color = new Color(1f, 1f, 1f, amount * 0.6f);
    }

    //---- Its buttons (WindowButton → On Click)

    // Minimize: let go of it
    public void Minimize() => LetGo();

    // Maximized ↔ normal (the gag)
    public void Resize()
    {
        if (maxReference == null) { Debug.LogWarning("[HudWindow] Resize: no Max Reference assigned.", this); return; }
        SetMaximized(!maximized, false);
    }

    // Close: for now, as Minimize (let go of it)
    public void Close() => LetGo();

    void LetGo()
    {
        if (forDex) { closed?.Invoke(); return; }
        if (MemeDock.Instance != null && MemeDock.Instance.Carried != null) Camera.main.GetComponent<PlayerLook>().DropObject();
    }

    // A locked button
    public void Denied() { if (deniedClip != null) source.PlayOneShot(deniedClip); }

    // Maximized: as tall as Max Reference, its pivot where Max Reference's would be (scaled, so it keeps its proportion)
    void SetMaximized(bool on, bool instant)
    {
        if (maxReference == null) return;
        if (resizing != null) { StopCoroutine(resizing); resizing = null; }
        if (!maximized) normalPosition = rect.position;   // (the canvas may have moved it since: read it now)
        maximized = on;

        Vector3 toPosition = normalPosition;
        float toScale = 1f;
        if (on)
        {
            var corners = new Vector3[4];
            maxReference.GetWorldCorners(corners);
            float tall = corners[1].y - corners[0].y;
            toScale = tall / Mathf.Max(rect.rect.height * rect.parent.lossyScale.y, 1f);
            float wide = rect.rect.width * rect.parent.lossyScale.x * toScale;
            toPosition = new Vector3(corners[0].x + rect.pivot.x * wide, corners[0].y + rect.pivot.y * tall, rect.position.z);
        }
        if (instant) { rect.position = toPosition; rect.localScale = Vector3.one * toScale; }
        else resizing = StartCoroutine(Resizing(toPosition, toScale));
    }

    IEnumerator Resizing(Vector3 toPosition, float toScale)
    {
        Vector3 fromPosition = rect.position;
        float fromScale = rect.localScale.y;
        for (float t = 0f; t < resizeTime; t += Time.unscaledDeltaTime)
        {
            float p = t / resizeTime - 1f;
            float k = 1f + 2.70158f * p * p * p + 1.70158f * p * p;   // ease out back
            rect.position = Vector3.LerpUnclamped(fromPosition, toPosition, k);
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, toScale, k);
            yield return null;
        }
        rect.position = toPosition;
        rect.localScale = Vector3.one * toScale;
        resizing = null;
    }
}
