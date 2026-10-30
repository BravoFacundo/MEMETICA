using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// The MemeDex window: every meme there is, the ones not unlocked in black. On top the axes, a row of 7 each (Base,
// Fall, Leave, Move 1-4); below, a little apart, the single memes (the optional ones), also 7 per row. Vertical scroll
// on the right. Whose memes it shows (Mode):
//   Current   this run (the end screen; needs the scene's MemeDex)
//   Global    everyone's together: all memes found by the players who saved a name (ScoreBoard)
//   Player    one saved run: type its name and Show (or the scoreboard opens it: ShowRun)
// Its catalog (the memes and their order): the scene's MemeDex (Managers > MemeDex); in a scene without one (the
// Menu), its own Axes and Singles (the builder copies them).
// Click an unlocked one: it plays there, its video (if it has one) and its sound, once. Only one at a time (one video
// player for the whole window: light).
// Made by Memetica > UI > Make MemeDex Window (select a canvas). Change its look as you like: the cells are made here,
// on a dark gradient (stickers and silhouettes read on it), or on Cell Frame if you set one.
public class MemeDexView : MonoBehaviour
{
    public enum Mode { Current, Global, Player }

    [Section("Shows")]
    [Tooltip("What it shows when it opens: this run (end screen) or everyone's (menu)")]
    public Mode startMode = Mode.Global;

    [Section("Catalog (without a MemeDex in the scene)")]
    public List<MemeData> axes = new List<MemeData>();
    public List<MemeData> singles = new List<MemeData>();

    [Section("Layout")]
    [Tooltip("Cells per row")]
    public int columns = 7;
    [Tooltip("Space between cells (canvas units)")]
    public float spacing = 8f;
    [Tooltip("Space between the axes and the single memes")]
    public float sectionGap = 40f;
    [Tooltip("Behind each cell (optional)")]
    public Sprite cellFrame;
    [Tooltip("Behind each cell without Cell Frame: a gradient from top to bottom")]
    public Color cellTop = new Color(0.32f, 0.32f, 0.36f);
    public Color cellBottom = new Color(0.06f, 0.06f, 0.08f);
    [Tooltip("A meme not unlocked: its picture in this color (black: a silhouette)")]
    public Color lockedColor = Color.black;
    [Tooltip("Unlocked, but only a video (no picture): this color")]
    public Color noPictureColor = new Color(0.25f, 0.25f, 0.3f);

    [Section("References")]
    public ScrollRect scroll;
    public RectTransform content;
    public TMP_Text modeLabel;
    public TMP_InputField nameInput;
    public Button globalButton;
    public Button showButton;

    [Section("Playing")]
    [Range(0f, 1f)] public float volume = 1f;

    public Mode Current { get; private set; }

    private HashSet<string> unlocked = new HashSet<string>();
    private readonly List<RectTransform> grids = new List<RectTransform>();
    private float builtWidth = -1f;

    void Awake()
    {
        if (globalButton != null) globalButton.onClick.AddListener(ShowGlobal);
        if (showButton != null) showButton.onClick.AddListener(ShowTyped);
        if (nameInput != null) nameInput.onSubmit.AddListener(_ => ShowTyped());
    }

    void OnDisable() => StopPreview();

    void OnEnable()
    {
        if (startMode == Mode.Current) ShowCurrent();
        else ShowGlobal();
    }

    //---- What it shows

    // This run (the scene's MemeDex)
    public void ShowCurrent()
    {
        unlocked = new HashSet<string>();
        if (MemeDex.Instance != null) foreach (MemeDex.Entry entry in MemeDex.Instance.Order) unlocked.Add(entry.Key);
        Show(Mode.Current, "This run");
    }

    // Everyone's together
    public void ShowGlobal()
    {
        unlocked = new HashSet<string>(ScoreBoard.Global().Keys);
        Show(Mode.Global, "Everyone");
    }

    // One saved run (the scoreboard's rows open it)
    public void ShowRun(ScoreBoard.Run run)
    {
        if (run == null) { ShowGlobal(); return; }
        unlocked = new HashSet<string>(run.memes);
        Show(Mode.Player, run.name);
    }

    // The name typed in Name Input
    public void ShowTyped()
    {
        string typed = nameInput != null ? nameInput.text.Trim() : "";
        if (typed.Length == 0) { ShowGlobal(); return; }
        ScoreBoard.Run run = ScoreBoard.Load().Find(r => string.Equals(r.name, typed, System.StringComparison.OrdinalIgnoreCase));
        if (run != null) ShowRun(run);
        else if (modeLabel != null) modeLabel.text = $"'{typed}' not found";
    }

    void Show(Mode mode, string label)
    {
        Current = mode;
        Build(true);
        if (modeLabel != null) modeLabel.text = $"{label}   {Found()} / {TotalCells()}";
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;   // from the top
    }

    //---- The catalog

    List<MemeData> Axes => MemeDex.Instance != null ? MemeDex.Instance.axes : axes;
    List<MemeData> Singles => MemeDex.Instance != null ? MemeDex.Instance.optional : singles;

    // The 7 of an axis, in the dex's order (key and picture; a missing image: no picture)
    static IEnumerable<(string key, MemeImage image)> SevenOf(MemeData meme)
    {
        yield return ($"{meme.name}/Base", meme.baseImage);
        yield return ($"{meme.name}/Fall", meme.fall);
        yield return ($"{meme.name}/Leave", meme.leave);
        for (int m = 1; m <= 4; m++) yield return ($"{meme.name}/Move{m}", m <= meme.variants.Count ? meme.variants[m - 1] : null);
    }

    // How many of the catalog's it has (a saved run may hold memes this catalog doesn't have)
    int Found()
    {
        int found = 0;
        foreach (MemeData meme in Axes) if (meme != null) foreach (var (key, _) in SevenOf(meme)) if (unlocked.Contains(key)) found++;
        foreach (MemeData meme in Singles) if (meme != null && unlocked.Contains($"{meme.name}/Optional")) found++;
        return found;
    }

    int TotalCells()
    {
        int total = 0;
        foreach (MemeData meme in Axes) if (meme != null) total += 7;
        foreach (MemeData meme in Singles) if (meme != null) total++;
        return total;
    }

    //---- The cells

    // Rebuilt when what it shows changes, or its width does (the cells fill it, 7 per row)
    void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && content != null && Mathf.Abs(Width() - builtWidth) > 1f) Build(false);
    }

    float Width()
    {
        RectTransform viewport = scroll != null && scroll.viewport != null ? scroll.viewport : (RectTransform)content.parent;
        return viewport.rect.width;
    }

    void Build(bool force)
    {
        if (content == null) return;
        float width = Width();
        if (width <= 1f) return;
        if (!force && Mathf.Abs(width - builtWidth) <= 1f) return;
        builtWidth = width;

        StopPreview();
        foreach (Transform child in content) Destroy(child.gameObject);
        grids.Clear();

        float cell = Mathf.Floor((width - spacing * (columns + 1)) / columns);
        var top = Grid("Axes", cell);
        foreach (MemeData meme in Axes)
            if (meme != null) foreach (var (key, image) in SevenOf(meme)) Cell(top, key, image, cell);

        if (Singles.Exists(m => m != null))
        {
            var gap = new GameObject("Gap", typeof(RectTransform), typeof(LayoutElement));
            gap.transform.SetParent(content, false);
            gap.GetComponent<LayoutElement>().minHeight = sectionGap;
            var bottom = Grid("Singles", cell);
            foreach (MemeData meme in Singles) if (meme != null) Cell(bottom, $"{meme.name}/Optional", meme.baseImage, cell);
        }
    }

    RectTransform Grid(string gridName, float cell)
    {
        var go = new GameObject(gridName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        var rect = (RectTransform)go.transform;
        rect.SetParent(content, false);
        var grid = go.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = new Vector2(spacing, spacing);
        grid.padding = new RectOffset((int)spacing, (int)spacing, (int)spacing, (int)spacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        grids.Add(rect);
        return rect;
    }

    void Cell(RectTransform grid, string key, MemeImage image, float size)
    {
        var go = new GameObject(key, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(grid, false);
        var back = go.GetComponent<Image>();
        back.sprite = cellFrame != null ? cellFrame : Gradient();
        back.color = Color.white;
        back.raycastTarget = true;   // (its click)

        var pictureGo = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
        var rect = (RectTransform)pictureGo.transform;
        rect.SetParent(go.transform, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var picture = pictureGo.GetComponent<RawImage>();
        picture.raycastTarget = false;

        Texture2D texture = image != null ? image.picture : null;
        bool has = unlocked.Contains(key);
        picture.texture = texture;
        if (texture != null)   // filling the cell, centered (a wider or taller picture is cut)
        {
            float aspect = (float)texture.width / Mathf.Max(texture.height, 1);
            picture.uvRect = aspect >= 1f ? new Rect((1f - 1f / aspect) / 2f, 0f, 1f / aspect, 1f) : new Rect(0f, (1f - aspect) / 2f, 1f, aspect);
        }
        picture.color = !has ? lockedColor : texture != null ? Color.white : noPictureColor;
        if (has) go.AddComponent<Button>().onClick.AddListener(() => Preview(picture, image));
    }

    // The cells' background: a dark vertical gradient (made once)
    private Sprite gradient;
    Sprite Gradient()
    {
        if (gradient != null) return gradient;
        var texture = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 32; y++) texture.SetPixel(0, y, Color.Lerp(cellBottom, cellTop, y / 31f));
        texture.Apply();
        gradient = Sprite.Create(texture, new Rect(0, 0, 1, 32), new Vector2(0.5f, 0.5f));
        return gradient;
    }

    //---- Playing one (click)

    private VideoPlayer player;
    private AudioSource sound;
    private RenderTexture frame;
    private RawImage playing;
    private Texture playingPicture;
    private Rect playingUV;
    private Color playingColor;

    void Preview(RawImage picture, MemeImage image)
    {
        bool same = playing == picture;
        StopPreview();
        if (same || image == null) return;   // (clicked again: stops)
        if (sound == null)
        {
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.ignoreListenerPause = true;   // (it plays in the pause menu too)
        }
        sound.volume = volume;
        playing = picture; playingPicture = picture.texture; playingUV = picture.uvRect; playingColor = picture.color;

        if (image.video != null)
        {
            if (player == null)
            {
                player = gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.isLooping = false;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.loopPointReached += _ => StopPreview();
            }
            int w = Mathf.Max(2, (int)image.video.width), h = Mathf.Max(2, (int)image.video.height);
            if (frame == null || frame.width != w || frame.height != h)
            {
                if (frame != null) frame.Release();
                frame = new RenderTexture(w, h, 0);
            }
            player.targetTexture = frame;
            player.clip = image.video;
            player.audioOutputMode = image.videoSound ? VideoAudioOutputMode.AudioSource : VideoAudioOutputMode.None;
            if (image.videoSound) { player.EnableAudioTrack(0, true); player.SetTargetAudioSource(0, sound); }
            player.Play();
            picture.texture = frame;
            picture.color = Color.white;
            float aspect = (float)w / h;
            picture.uvRect = aspect >= 1f ? new Rect((1f - 1f / aspect) / 2f, 0f, 1f / aspect, 1f) : new Rect(0f, (1f - aspect) / 2f, 1f, aspect);
        }
        if (image.sound != null && !(image.video != null && image.videoSound)) sound.PlayOneShot(image.sound, volume);
    }

    // Back to its picture, quiet
    void StopPreview()
    {
        if (player != null && player.isPlaying) player.Stop();
        if (sound != null) sound.Stop();
        if (playing != null) { playing.texture = playingPicture; playing.uvRect = playingUV; playing.color = playingColor; }
        playing = null;
    }

    void OnDestroy()
    {
        if (frame != null) frame.Release();
    }
}
