using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// The end screen (win or lose): this run's stats and the last meme you unlocked. Goes on Canvas_HUD (always on): it
// keeps Screen hidden until the run ends; GameManager > End Screen (the same Screen) turns it on then. The scoreboard
// and the MemeDex are screens of their own (UIManager > Screens, Other): their buttons call UIManager.OpenWindow /
// CloseWindow. Its contents (each text optional). With Labels:
// "Time Played: 03:12" (its label in English or Spanish, as the game's language); without: only the value.
// Its other buttons call UIManager: Play Again → UIManager.PlayAgain, Menu → UIManager.GoToMenu.
public class EndScreen : MonoBehaviour
{
    [System.Serializable]
    public class Label
    {
        public string english;
        public string spanish;
        public Label(string english, string spanish) { this.english = english; this.spanish = spanish; }
        public override string ToString() => SettingsManager.Language == GameLanguage.Spanish ? spanish : english;
    }

    [Header("Screen")]
    [Tooltip("The end screen's root (also GameManager > End Screen): hidden until the run ends")]
    public GameObject screen;

    [Header("Labels")]
    [Tooltip("On: each stat reads 'Time Played: 03:12'. Off: only the value")]
    public bool withLabels = true;
    public Label timePlayedLabel = new Label("Time Played", "Tiempo jugado");
    public Label timeLeftLabel = new Label("Time Left", "Tiempo restante");
    public Label memesLabel = new Label("Memes", "Memes");
    public Label secretMemesLabel = new Label("Secret Memes", "Memes secretos");
    public Label totalMemesLabel = new Label("Total Memes", "Memes totales");
    public Label fallsLabel = new Label("Falls", "Caídas");

    [Header("Stats (User Score)")]
    [Tooltip("Time played: 03:12")]
    public TMP_Text timePlayed;
    [Tooltip("Time left: 01:48 (00:00 when time ran out)")]
    public TMP_Text timeLeft;
    [Tooltip("Memes of the axes (not the secret ones): 15/28")]
    public TMP_Text memes;
    [Tooltip("Secret memes: 2/5")]
    public TMP_Text secretMemes;
    [Tooltip("Both together: 17/33")]
    public TMP_Text totalMemes;
    [Tooltip("Falls into the void: 4")]
    public TMP_Text falls;

    [Header("Last Meme")]
    [Tooltip("The last meme you unlocked: its picture, or its video (looping, silent). An Aspect Ratio Fitter on it keeps its shape")]
    public RawImage lastMemePicture;
    [Tooltip("Its name (its window title, else its axis)")]
    public TMP_Text lastMemeTitle;
    [Tooltip("Hidden when there is none (nothing unlocked yet)")]
    public GameObject lastMemeGroup;

    void Awake()
    {
        if (screen != null) screen.SetActive(false);
    }

    void OnEnable()
    {
        GameManager.StateChanged += OnState;
        SettingsManager.Changed += Fill;   // (the language key: the language changes, the labels too)
    }

    void OnDisable()
    {
        GameManager.StateChanged -= OnState;
        SettingsManager.Changed -= Fill;
    }

    // The run ended: its stats, its first page (GameManager shows it, after the blue screen if you lost)
    void OnState(GameManager.State state)
    {
        if (state != GameManager.State.Won && state != GameManager.State.Lost) return;
        Fill();
    }

    void Set(TMP_Text text, Label label, string value)
    {
        if (text != null) text.text = withLabels ? $"{label}: {value}" : value;
    }

    public void Fill()
    {
        GameManager game = GameManager.Instance;
        MemeDex dex = MemeDex.Instance;
        Set(timePlayed, timePlayedLabel, game != null ? GameManager.Format(game.Elapsed) : "--:--");
        Set(timeLeft, timeLeftLabel, game != null ? GameManager.Format(game.TimeLeft) : "--:--");
        Set(falls, fallsLabel, RunStats.Falls.ToString());

        int secretsFound = 0, secretsTotal = 0;
        if (dex != null)
        {
            foreach (MemeDex.Entry entry in dex.Order) if (entry.kind == MemeDex.Kind.Optional) secretsFound++;
            foreach (MemeDex.Entry entry in dex.All()) if (entry.kind == MemeDex.Kind.Optional) secretsTotal++;
        }
        int found = dex != null ? dex.Count : 0, total = dex != null ? dex.Total : 0;
        Set(memes, memesLabel, $"{found - secretsFound}/{total - secretsTotal}");
        Set(secretMemes, secretMemesLabel, $"{secretsFound}/{secretsTotal}");
        Set(totalMemes, totalMemesLabel, $"{found}/{total}");

        // The last one unlocked that has something to show (a picture or a video)
        MemeImage image = null;
        string title = "";
        if (dex != null)
            for (int i = dex.Count - 1; i >= 0 && image == null; i--)
            {
                MemeDex.Entry entry = dex.Order[i];
                MemeImage candidate = ImageOf(entry);
                if (!MemeImage.Exists(candidate)) continue;
                image = candidate;
                title = !string.IsNullOrEmpty(image.windowTitle) ? image.windowTitle : entry.meme != null ? entry.meme.title : "";
            }
        Debug.Log($"[EndScreen] {(dex != null ? dex.Count : 0)} memes unlocked; last one shown: " +
                  (image == null ? "none" : $"'{title}' ({(image.picture != null ? "picture" : "video")})"), this);
        if (lastMemeGroup != null) lastMemeGroup.SetActive(image != null);
        if (lastMemeTitle != null) lastMemeTitle.text = title;
        if (lastMemePicture == null) return;
        lastMemePicture.enabled = image != null;
        StopVideo();
        if (image == null) return;
        fill = image.fill;
        if (image.picture != null) { lastMemePicture.texture = image.picture; Fit(image.picture.width, image.picture.height); }
        else PlayVideo(image.video);
    }

    //---- A video meme: on a texture of its size, looping, silent

    private VideoPlayer player;
    private RenderTexture frame;

    void PlayVideo(VideoClip clip)
    {
        if (player == null)
        {
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.None;
        }
        int w = Mathf.Max((int)clip.width, 16), h = Mathf.Max((int)clip.height, 16);
        if (frame == null || frame.width != w || frame.height != h)
        {
            if (frame != null) frame.Release();
            frame = new RenderTexture(w, h, 0);
        }
        player.targetTexture = frame;
        player.clip = clip;
        player.Play();
        lastMemePicture.texture = frame;
        Fit(w, h);
    }

    void StopVideo()
    {
        if (player != null && player.isPlaying) player.Stop();
    }

    // Its shape: whole (the Aspect Ratio Fitter keeps it), or filling its place cut to fit (its MemeImage's Fill, as on
    // the HUD). Again every frame while shown (the layout may not be ready when it is filled)
    private bool fill;
    private Vector2 size;

    void Fit(int width, int height)
    {
        size = new Vector2(width, Mathf.Max(height, 1));
        ApplyFit();
    }

    void LateUpdate()
    {
        if (screen != null && screen.activeInHierarchy && lastMemePicture != null && lastMemePicture.enabled) ApplyFit();
    }

    void ApplyFit()
    {
        if (size.x <= 0f) return;
        float aspect = size.x / size.y;
        lastMemePicture.TryGetComponent(out AspectRatioFitter fitter);
        RectTransform own = lastMemePicture.rectTransform;
        RectTransform area = fitter != null && own.parent is RectTransform parent ? parent : own;   // where it has to fit
        float areaAspect = area.rect.height > 1f ? area.rect.width / area.rect.height : aspect;
        if (fill && Mathf.Abs(areaAspect - aspect) > 0.001f)
        {
            lastMemePicture.uvRect = aspect > areaAspect
                ? new Rect((1f - areaAspect / aspect) / 2f, 0f, areaAspect / aspect, 1f)   // wider: its sides cut
                : new Rect(0f, (1f - aspect / areaAspect) / 2f, 1f, aspect / areaAspect);  // taller: top and bottom cut
            if (fitter != null) fitter.aspectRatio = areaAspect;
        }
        else
        {
            lastMemePicture.uvRect = new Rect(0f, 0f, 1f, 1f);
            if (fitter != null) fitter.aspectRatio = aspect;
        }
    }

    void OnDestroy()
    {
        if (frame != null) frame.Release();
    }

    static MemeImage ImageOf(MemeDex.Entry entry)
    {
        MemeData meme = entry.meme;
        if (meme == null) return null;
        switch (entry.kind)
        {
            case MemeDex.Kind.Fall: return meme.fall;
            case MemeDex.Kind.Leave: return meme.leave;
            case MemeDex.Kind.Move: return entry.move >= 1 && entry.move <= meme.variants.Count ? meme.variants[entry.move - 1] : null;
            default: return meme.baseImage;   // Base, Optional
        }
    }
}
