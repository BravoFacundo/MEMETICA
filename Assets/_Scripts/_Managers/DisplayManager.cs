using UnityEngine;
using UnityEngine.SceneManagement;

// How the game fills the monitor (SettingsManager.DisplayMode, the settings window's Display tab):
//   Native   the monitor's own shape at its own resolution: a 16:9 monitor plays 16:9, a 4:3 one plays 4:3
//   Classic  4:3 in the middle of any monitor (the exhibition CRT look on a wide screen), the rest Bar Color
// It never changes the resolution (a monitor that can't show it would stretch or black-bar it on its own): the view
// is drawn smaller inside the native one. Every screen camera gets View as its Camera.rect, a camera behind everything
// paints the bars (FrameEffect's tone), and the UI follows through AspectArea (a stretched child of each canvas that
// the canvas' content goes in).
// Works in every scene (it makes itself, nothing to place).
public class DisplayManager : MonoBehaviour
{
    public enum Mode { Native, Classic }

    // The classic shape and the bars around it
    public const float ClassicAspect = 4f / 3f;
    // The bars' colour (FrameEffect sets it: the same as the frame)
    public static Color BarColor = new Color32(0x31, 0x31, 0x31, 0xFF);

    // The part of the screen the game uses (0-1 of the screen). Native: all of it
    public static Rect View { get; private set; } = new Rect(0f, 0f, 1f, 1f);
    // The same, in screen pixels
    public static Rect PixelView => new Rect(View.x * Screen.width, View.y * Screen.height, View.width * Screen.width, View.height * Screen.height);
    // View changed (the mode, or the window's size)
    public static event System.Action Changed;

    private Camera bars;
    private Vector2Int screenSize;
    private Mode appliedMode = (Mode)(-1);
    private int cameraCount = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        GameObject go = new GameObject("DisplayManager");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<DisplayManager>();
    }

    void Awake()
    {
        bars = gameObject.AddComponent<Camera>();
        bars.clearFlags = CameraClearFlags.SolidColor;
        bars.backgroundColor = BarColor;
        bars.cullingMask = 0;          // draws nothing: only clears
        bars.depth = -100;             // behind every other camera
        bars.useOcclusionCulling = false;
        bars.allowHDR = false;
        bars.allowMSAA = false;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SettingsManager.Changed += Refresh;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SettingsManager.Changed -= Refresh;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { cameraCount = -1; Refresh(); }

    // Something may have changed: the setting, the window's size, or a camera appeared
    void LateUpdate()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (bars.backgroundColor != BarColor) bars.backgroundColor = BarColor;
        if (size != screenSize || SettingsManager.DisplayMode != appliedMode) Refresh();
        else if (Camera.allCamerasCount != cameraCount) ApplyToCameras();
    }

    void Refresh()
    {
        screenSize = new Vector2Int(Screen.width, Screen.height);
        appliedMode = SettingsManager.DisplayMode;
        View = appliedMode == Mode.Classic ? Fit(ClassicAspect) : new Rect(0f, 0f, 1f, 1f);
        bars.enabled = View.width < 0.999f || View.height < 0.999f;
        ApplyToCameras();
        Changed?.Invoke();
    }

    // The biggest rectangle of that shape centered on the screen (wider screen: bars at the sides; taller: top and bottom).
    // Snapped to whole pixels: the cameras, the frame and the cursor's clip all end on the same pixel (no 1 px seam)
    static Rect Fit(float aspect)
    {
        int sw = Mathf.Max(Screen.width, 1), sh = Mathf.Max(Screen.height, 1);
        int w = Mathf.Min(sw, Mathf.RoundToInt(sh * aspect));
        int h = Mathf.Min(sh, Mathf.RoundToInt(sw / aspect));
        int x = (sw - w) / 2, y = (sh - h) / 2;
        return new Rect((float)x / sw, (float)y / sh, (float)w / sw, (float)h / sh);
    }

    // Every camera that draws to the screen (not to a texture: the portal's views)
    void ApplyToCameras()
    {
        cameraCount = Camera.allCamerasCount;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == bars || cam.targetTexture != null) continue;
            cam.rect = View;
        }
    }
}
