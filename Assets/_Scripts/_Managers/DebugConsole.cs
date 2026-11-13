using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The debug console: a green screen over the game (it doesn't pause it), under the cursor and the monitor's frame.
// F1:
//   Locked     a prompt top left asks for DebugLock's password (its digits, top row or numpad): right, it unlocks
//   Unlocked   F1 shows / hides the console. Every debug key works with it hidden too: the console is their hint, and
//              where a command asks for a value (it opens by itself then)
// Who fills it: each one adds a Section of lines (DebugTools: the F keys, only in Game; ScoreConsole: the scores) and
// uses Pressed for its keys: that key's line lights up once. Ask: a line to type a value (Enter: done, Esc: cancel;
// meanwhile the game doesn't read the keyboard: GameInput). Log: the last lines of what happened (hidden, the newest
// shows a moment at the bottom). Its own keys: 0 locks it, 9 English / Spanish.
// Works in every scene (it makes itself, nothing to place).
public class DebugConsole : MonoBehaviour
{
    public enum Column { Left, Right }

    // A line of a section: its key (lights up when used), what it does, and Off (doesn't work in this build: dimmed)
    public struct Line
    {
        public string key, text;
        public bool off;
        public Line(string key, string text, bool off = false) { this.key = key; this.text = text; this.off = off; }
    }

    class Section
    {
        public object owner;
        public string title;
        public Column column;
        public int order;
        public Func<List<Line>> lines;   // Left: lines with keys
        public Func<string> text;        // Right: free text (a list)
    }

    // Game Boy greens
    static readonly Color BackColor = new Color32(0x0F, 0x38, 0x0F, 0xF5);   // (~96%: the game barely shows behind)
    const string Light = "#9BBC0F", Mid = "#8BAC0F", Dim = "#5A7A1A", Bright = "#E8F8A0";
    const string FontPath = "Fonts & Materials/impact SDF";   // TextMesh Pro's Resources

    const float FlashTime = 0.4f;     // a used key's line, lit
    const float PromptTime = 6f;      // the password prompt waits this long for a digit
    const float MessageTime = 2.5f;   // "unlocked" / "wrong password"
    const float ToastTime = 4f;       // hidden: the newest log line shows this long
    const int LogLines = 6;
    const int MaxTyped = 24;

    static DebugConsole instance;
    static readonly List<Section> sections = new List<Section>();
    static readonly List<string> log = new List<string>();

    // Showing (only unlocked)
    public static bool Open { get; private set; }
    // A value is being typed: the game doesn't read the keyboard (and the frame it ended, so Esc / Enter don't leak)
    public static bool Typing => instance != null && (instance.asking != null || instance.typingEndedFrame == Time.frameCount);

    // Asking
    Action<string> asking;
    string askPrompt, typed = "";
    bool digitsOnly;
    int askFrame = -1, typingEndedFrame = -1, unlockedFrame = -1;
    float nextRefresh;
    const float RefreshTime = 0.2f;   // open: its text again (the scores file is read then)

    // Password prompt (locked)
    float promptUntil, messageUntil, toastUntil;
    string code = "", message;

    readonly Dictionary<string, float> flashes = new Dictionary<string, float>();

    // Drawn with
    Canvas canvas;
    GameObject panel, promptBox, toastBox;
    TMP_Text left, right, bottom, promptText, toastText, corner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        sections.Clear();
        log.Clear();
        Open = false;
        GameObject go = new GameObject("DebugConsole");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DebugConsole>();
    }

    //---- For the others

    // Adds lines with keys (Left column). Owner: who removes them (RemoveSections). Order: lower goes first
    public static void AddSection(object owner, string title, Func<List<Line>> lines, int order = 0) =>
        Add(new Section { owner = owner, title = title, column = Column.Left, order = order, lines = lines });

    // Adds free text (Right column: a list…)
    public static void AddPanel(object owner, string title, Func<string> text, int order = 0) =>
        Add(new Section { owner = owner, title = title, column = Column.Right, order = order, text = text });

    static void Add(Section section)
    {
        sections.Add(section);
        sections.Sort((a, b) => a.order.CompareTo(b.order));
    }

    public static void RemoveSections(object owner) => sections.RemoveAll(s => s.owner == owner);

    // That key went down this frame, unlocked and nothing being typed (its line lights up). Label: as its line shows it
    public static bool Pressed(KeyCode key, string label = null)
    {
        if (!DebugLock.Unlocked || Typing || !Input.GetKeyDown(key)) return false;
        if (instance != null && instance.unlockedFrame == Time.frameCount) return false;   // (the password's last digit)
        Flash(label ?? key.ToString());
        return true;
    }

    // A number key (top row or numpad), as its line shows it ("1"…)
    public static bool Pressed(int digit) =>
        digit >= 0 && digit <= 9 && (Pressed(KeyCode.Alpha0 + digit, digit.ToString()) || Pressed(KeyCode.Keypad0 + digit, digit.ToString()));

    // That key's line lights up once
    public static void Flash(string key)
    {
        if (instance != null && !string.IsNullOrEmpty(key)) instance.flashes[key] = Time.unscaledTime + FlashTime;
    }

    // Asks for a value (the console opens). Answer gets it on Enter; Esc cancels. Digits Only: a number
    public static void Ask(string prompt, Action<string> answer, bool digitsOnly = false)
    {
        if (instance == null || answer == null) return;
        instance.askPrompt = prompt;
        instance.asking = answer;
        instance.digitsOnly = digitsOnly;
        instance.typed = "";
        instance.askFrame = Time.frameCount;   // (the key that asked is typed this frame too: not counted)
        Open = true;
    }

    // A line for the console's log (and Unity's)
    public static void Log(string text)
    {
        Debug.Log("[Console] " + text);
        log.Add(text);
        if (log.Count > LogLines) log.RemoveAt(0);
        if (instance != null) instance.toastUntil = Time.unscaledTime + ToastTime;
    }

    // Its own canvas (DebugTools' Hide UI leaves it alone)
    public static bool IsConsoleCanvas(Canvas c) => instance != null && c == instance.canvas;

    //---- Keys

    void Awake() => Build();

    void Update()
    {
        if (!DebugLock.Unlocked)
        {
            Open = false;
            if (asking != null) EndTyping();
            if (Input.GetKeyDown(KeyCode.F1)) { if (Time.unscaledTime < promptUntil) promptUntil = 0f; else StartPrompt(); }
            else if (Time.unscaledTime < promptUntil) TypePassword();
        }
        else if (asking != null) TypeValue();
        else
        {
            if (Input.GetKeyDown(KeyCode.F1)) Open = !Open;
            if (Pressed(0)) Lock();
            if (Pressed(9)) { SettingsManager.ToggleLanguage(); Log("Language: " + SettingsManager.Language); }
        }
    }

    void LateUpdate() => Draw();   // (after everyone used their keys this frame)

    void StartPrompt()
    {
        code = "";
        promptUntil = Time.unscaledTime + PromptTime;
        messageUntil = 0f;
    }

    void TypePassword()
    {
        int digit = Digit();
        if (digit < 0) return;
        code += digit;
        promptUntil = Time.unscaledTime + PromptTime;
        if (code.Length < DebugLock.Code.Length) return;
        promptUntil = 0f;
        bool right = DebugLock.TryUnlock(code);
        if (right) unlockedFrame = Time.frameCount;
        message = right ? "CONSOLE UNLOCKED   F1: show / hide" : "WRONG PASSWORD";
        messageUntil = Time.unscaledTime + MessageTime;
        code = "";
    }

    static int Digit()
    {
        for (int i = 0; i <= 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i)) return i;
        return -1;
    }

    void Lock()
    {
        DebugLock.Lock();
        Open = false;
        message = "CONSOLE LOCKED";
        messageUntil = Time.unscaledTime + MessageTime;
    }

    void TypeValue()
    {
        if (Time.frameCount == askFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Log("Cancelled"); EndTyping(); return; }
        foreach (char c in Input.inputString)
        {
            if (c == '\b') { if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1); }
            else if (c == '\n' || c == '\r')
            {
                Action<string> answer = asking;
                string value = typed.Trim();
                EndTyping();
                answer(value);   // (it may ask again: the next question)
                return;
            }
            else if (!char.IsControl(c) && typed.Length < MaxTyped && (!digitsOnly || char.IsDigit(c))) typed += c;
        }
    }

    void EndTyping()
    {
        asking = null;
        typed = "";
        typingEndedFrame = Time.frameCount;
    }

    //---- Drawing

    void Draw()
    {
        bool unlocked = DebugLock.Unlocked;
        panel.SetActive(Open);
        corner.gameObject.SetActive(unlocked && !Open);

        bool prompting = !unlocked && Time.unscaledTime < promptUntil;
        bool messaging = Time.unscaledTime < messageUntil;
        promptBox.SetActive(!Open && (prompting || messaging));
        if (promptBox.activeSelf)
            promptText.text = prompting ? "CONSOLE LOCKED   PASSWORD: " + code + new string('_', DebugLock.Code.Length - code.Length) : message;

        toastBox.SetActive(!Open && log.Count > 0 && Time.unscaledTime < toastUntil);
        if (toastBox.activeSelf) toastText.text = "<noparse>" + log[log.Count - 1] + "</noparse>";

        if (!Open || (Time.unscaledTime < nextRefresh && !Input.anyKeyDown && asking == null)) return;
        nextRefresh = Time.unscaledTime + RefreshTime;
        left.text = LeftText();
        right.text = RightText();
        bottom.text = BottomText();
    }

    string LeftText()
    {
        var sb = new StringBuilder();
        sb.Append($"<color={Light}><size=120%>MEMETICA CONSOLE</size></color>   <color={Dim}>{BuildName()}</color>\n");
        sb.Append(KeyLine(new Line("F1", "show / hide this")));
        sb.Append(KeyLine(new Line("0", "lock the console")));
        sb.Append(KeyLine(new Line("9", "English / Spanish  (" + SettingsManager.Language + ")")));
        foreach (Section s in sections)
        {
            if (s.column != Column.Left) continue;
            List<Line> lines = null;
            try { lines = s.lines(); } catch (Exception e) { Debug.LogWarning($"[DebugConsole] '{s.title}': {e.Message}"); }
            sb.Append($"\n<color={Light}>{s.title}</color>\n");
            if (lines != null) foreach (Line l in lines) sb.Append(KeyLine(l));
        }
        return sb.ToString();
    }

    string KeyLine(Line l)
    {
        bool lit = l.key != null && flashes.TryGetValue(l.key, out float until) && Time.unscaledTime < until;
        string color = lit ? Bright : l.off ? Dim : Mid;
        string line = $"<color={color}>{l.key}<pos=3.4em>{l.text}{(l.off ? "  (off)" : "")}</color>";
        return (lit ? $"<mark={Light}55>{line}</mark>" : line) + "\n";
    }

    string RightText()
    {
        var sb = new StringBuilder();
        foreach (Section s in sections)
        {
            if (s.column != Column.Right) continue;
            string text = null;
            try { text = s.text(); } catch (Exception e) { Debug.LogWarning($"[DebugConsole] '{s.title}': {e.Message}"); }
            if (!string.IsNullOrEmpty(s.title)) sb.Append($"<color={Light}>{s.title}</color>\n");
            sb.Append($"<color={Mid}>{text}</color>\n\n");
        }
        return sb.ToString();
    }

    string BottomText()
    {
        var sb = new StringBuilder();
        foreach (string l in log) sb.Append($"<color={Dim}><noparse>{l}</noparse></color>\n");
        if (asking != null)
        {
            bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
            sb.Append($"<color={Bright}><noparse>{askPrompt} > {typed}</noparse>{(blink ? "_" : " ")}</color>   <color={Dim}>Enter: OK   Esc: cancel</color>");
        }
        else sb.Append($"<color={Dim}>></color>");
        return sb.ToString();
    }

    static string BuildName() =>
        (Application.isEditor ? "Editor" : Debug.isDebugBuild ? "Development build" : "Build") + "  ·  " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    //---- Building (its own canvas, under the cursor (999) and the frame (1000))

    void Build()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(FontPath);
        if (font == null) Debug.LogWarning($"[DebugConsole] No '{FontPath}' in Resources: TextMesh Pro's default font.");

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        RectTransform view = Child("View", transform);
        Stretch(view, Vector2.zero, Vector2.one);
        view.gameObject.AddComponent<AspectArea>();   // (inside the 4:3 window in Classic)

        // The console: the green to the screen's edges (under the frame), the texts inside the frame
        RectTransform back = Child("Panel", view);
        Stretch(back, Vector2.zero, Vector2.one);
        panel = back.gameObject;
        Back(panel);
        RectTransform p = Child("Content", back);
        Stretch(p, Vector2.zero, Vector2.one, 40f);
        left = Text("Left", p, font, 24f, new Vector2(0f, 0.26f), new Vector2(0.55f, 1f), TextAlignmentOptions.TopLeft);
        right = Text("Right", p, font, 22f, new Vector2(0.56f, 0.26f), new Vector2(1f, 1f), TextAlignmentOptions.TopLeft);
        bottom = Text("Log", p, font, 22f, new Vector2(0f, 0f), new Vector2(1f, 0.25f), TextAlignmentOptions.BottomLeft);

        // Locked: the password prompt, top left
        promptBox = Box("Prompt", view, new Vector2(0f, 1f), new Vector2(60f, -60f), new Vector2(760f, 56f), font, 28f, out promptText);
        // Hidden: the newest log line, bottom left
        toastBox = Box("Toast", view, new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(1000f, 44f), font, 22f, out toastText);

        // Unlocked and hidden: a small reminder
        RectTransform c = Child("Corner", view);
        c.anchorMin = c.anchorMax = c.pivot = new Vector2(1f, 1f);
        c.anchoredPosition = new Vector2(-60f, -50f);
        c.sizeDelta = new Vector2(300f, 30f);
        corner = c.gameObject.AddComponent<TextMeshProUGUI>();
        Style(corner, font, 20f, TextAlignmentOptions.TopRight);
        corner.text = $"<color={Light}>DEBUG  ·  F1</color>";

        panel.SetActive(false);
        promptBox.SetActive(false);
        toastBox.SetActive(false);
        corner.gameObject.SetActive(false);
    }

    static RectTransform Child(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform r, Vector2 min, Vector2 max, float inset = 0f)
    {
        r.anchorMin = min;
        r.anchorMax = max;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    static void Back(GameObject go)
    {
        var image = go.AddComponent<Image>();
        image.color = BackColor;
        image.raycastTarget = false;
    }

    static TMP_Text Text(string name, RectTransform parent, TMP_FontAsset font, float size, Vector2 min, Vector2 max, TextAlignmentOptions align)
    {
        RectTransform r = Child(name, parent);
        Stretch(r, min, max);
        r.offsetMin = new Vector2(28f, 18f);
        r.offsetMax = new Vector2(-28f, -18f);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        Style(t, font, size, align);
        return t;
    }

    static GameObject Box(string name, RectTransform parent, Vector2 corner, Vector2 at, Vector2 size, TMP_FontAsset font, float fontSize, out TMP_Text text)
    {
        RectTransform r = Child(name, parent);
        r.anchorMin = r.anchorMax = r.pivot = corner;
        r.anchoredPosition = at;
        r.sizeDelta = size;
        Back(r.gameObject);
        RectTransform t = Child("Text", r);
        Stretch(t, Vector2.zero, Vector2.one);
        t.offsetMin = new Vector2(16f, 4f);
        t.offsetMax = new Vector2(-16f, -4f);
        text = t.gameObject.AddComponent<TextMeshProUGUI>();
        Style(text, font, fontSize, TextAlignmentOptions.MidlineLeft);
        text.color = new Color32(0x9B, 0xBC, 0x0F, 0xFF);
        return r.gameObject;
    }

    static void Style(TMP_Text t, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.richText = true;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;   // (Truncate drops every line after one that is too wide)
        t.raycastTarget = false;
        t.color = Color.white;   // (each line brings its color)
    }
}
