// The game's cursor: an Image that replaces Windows' cursor. Two ways of being, decided here every frame:
//   Crosshair  playing (Game): fixed in the middle as the aim; its icon follows what you do (Aim, below)
//   Free       it follows the mouse: in the Menu, in the pause, and while playing with Free Key held (Ctrl). Playing,
//              the mouse moves it instead of the camera but you keep walking and jumping (it isn't a pause), and it
//              clicks buttons: the UI ones (WindowButton: the HUD, the meme window in the HUD) and the 3D meme window's
//              in the world (WindowControls, within World Range). In the Menu and the pause only the UI ones
// Aim icons: Zoom (right click): magnifier + while zooming in, nothing once fully zoomed, magnifier − while zooming out.
//   Looking at a meme you can pick up: hand | Holding a meme in front (Game_Old): nothing (seen clean) | Else: Dot
//   (Game: carrying the meme window on the HUD keeps the aim: hand only over an optional meme, DexCollectible)
// Each cursor's click point is its sprite's PIVOT (arrow and pointing hand → the tip, grabbing hand → Center).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Point: something clickable | Grab: something you grab (memes) | Busy: loading (UIManager)
// NotAllowed: something you can't do (a locked button) | ZoomIn/Zoom/ZoomOut/Dot: the aim | Hidden: no cursor
public enum CursorState { Default, Grab, Busy, Point, NotAllowed, ZoomIn, Zoom, ZoomOut, Dot, Hidden }

[RequireComponent(typeof(Canvas))]
public class RetroCursor : MonoBehaviour
{
    public enum Mode { Crosshair, Free }

    [System.Serializable]
    public class CursorStyle
    {
        public CursorState state;
        public Sprite sprite;
    }

    public static RetroCursor Instance { get; private set; }

    public Image cursorImage;
    public List<CursorStyle> styles = new List<CursorStyle>();

    [Header("Free cursor while playing (Game only)")]
    [Tooltip("Hold it to free the cursor while playing")]
    public KeyCode freeKey = KeyCode.LeftControl;
    [Tooltip("Meters away a window in the world can be clicked")]
    public float worldRange = 6f;

    [Header("Aim (Game only)")]
    [Tooltip("Main Camera. Empty (Menu) = always the free cursor")]
    public PlayerLook playerLook;
    [Tooltip("FOV margin to call the zoom in or out finished")]
    public float fovMargin = 1f;

    public Mode CurrentMode { get; private set; } = Mode.Free;
    // Free while playing (not the Menu, not the pause): the windows can be clicked
    public bool FreeWhilePlaying => CurrentMode == Mode.Free && playerLook != null && !PauseMenu.GameIsPaused;

    private RectTransform rect;
    private bool busy;   // while loading, nothing else changes the cursor
    private Camera aimCamera;
    private CursorState current = (CursorState)(-1);
    private bool modeStarted;
    private bool wasPaused;

    void Awake()
    {
        Instance = this;
        GetComponent<Canvas>().sortingOrder = 999;   // above the UI (the frame is at 1000)
        rect = cursorImage.rectTransform;
        cursorImage.raycastTarget = false;           // it doesn't block clicks
        SetState(CursorState.Default);
    }

    void Start()
    {
        if (playerLook == null) return;
        aimCamera = playerLook.GetComponent<Camera>();
    }

    // Windows shows its own cursor again when the game window gets the focus back
    void OnApplicationFocus(bool focused) { if (focused && CurrentMode == Mode.Free) Cursor.visible = false; }
    void OnDisable() => Cursor.visible = true;

    void Update()
    {
        bool ended = GameManager.Instance != null && GameManager.Instance.Ended;   // the blue screen / win screen: click on them
        Mode wanted = playerLook == null || PauseMenu.GameIsPaused || ended || Input.GetKey(freeKey) ? Mode.Free : Mode.Crosshair;
        bool paused = PauseMenu.GameIsPaused;
        if (!modeStarted || wanted != CurrentMode || paused != wasPaused) ApplyMode(wanted);   // the pause also locks / frees it
        wasPaused = paused;

        if (playerLook == null || PauseMenu.GameIsPaused) return;   // Menu / pause: the UI buttons set the icon
        if (CurrentMode == Mode.Crosshair) SetState(AimState());
        else UpdateWindows();
    }

    void LateUpdate()
    {
        // On an Overlay Canvas, screen = position
        rect.position = CurrentMode == Mode.Free ? Input.mousePosition : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
    }

    void ApplyMode(Mode mode)
    {
        modeStarted = true;
        CurrentMode = mode;
        bool free = mode == Mode.Free;
        Cursor.visible = false;   // ours, never Windows'
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        if (playerLook != null) playerLook.CursorFree = free && !PauseMenu.GameIsPaused;
        if (free) SetState(CursorState.Default);   // the arrow (the aim sets its own icon)
    }

    // Before: PauseMenu switched it. Now the mode is decided here; kept so old calls still work
    public void SetFollowMouse(bool value) { }

    //---- Crosshair

    CursorState AimState()
    {
        float fov = aimCamera.fieldOfView;
        if (playerLook.IsZooming)
            return fov > playerLook.ZoomFOV + fovMargin ? CursorState.ZoomIn : CursorState.Hidden;
        if (fov < playerLook.DefaultFOV - fovMargin)
            return CursorState.ZoomOut;

        if (playerLook.heldObject != null && !playerLook.CarriedInHud) return CursorState.Hidden;   // Game_Old: the meme in front, seen clean
        if (playerLook.CanGrabLooked) return CursorState.Grab;   // (carrying yours: only an optional meme)
        return CursorState.Dot;
    }

    //---- Free while playing: the windows' buttons

    void UpdateWindows()
    {
        // Over the UI (the HUD window's buttons): the UI buttons set the icon and take the click
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        WindowControls window = WindowUnderCursor(out Vector2 uv);
        SetState(window != null ? window.Hover(uv) : CursorState.Default);
        if (window != null && Input.GetMouseButtonDown(0)) window.Click(uv);
    }

    // The 3D window in the world the cursor is on, the nearest within range (the HUD's are UI buttons)
    WindowControls WindowUnderCursor(out Vector2 uv)
    {
        uv = default;
        WindowControls best = null;
        float bestDistance = worldRange;
        Ray ray = aimCamera.ScreenPointToRay(Input.mousePosition);

        foreach (WindowControls window in WindowControls.All)
        {
            if (!window.Clickable) continue;
            Transform picture = window.picture;
            if (!new Plane(picture.forward, picture.position).Raycast(ray, out float distance) || distance > bestDistance) continue;

            Vector3 local = picture.InverseTransformPoint(ray.GetPoint(distance));
            var at = new Vector2(local.x + 0.5f, local.y + 0.5f);
            if (at.x < 0f || at.y < 0f || at.x > 1f || at.y > 1f) continue;
            best = window; bestDistance = distance; uv = at;
        }
        return best;
    }

    //---- Icons

    // Loading cursor: UIManager sets it and it blocks any other change until it is taken off
    public void SetBusy(bool value)
    {
        busy = false;
        SetState(value ? CursorState.Busy : CursorState.Default);
        busy = value;
    }

    public void SetState(CursorState state)
    {
        if (busy || state == current) return;
        current = state;
        cursorImage.enabled = state != CursorState.Hidden;
        if (state == CursorState.Hidden) return;

        foreach (CursorStyle s in styles)
        {
            if (s.state != state || s.sprite == null) continue;
            cursorImage.sprite = s.sprite;
            // The Image doesn't use the sprite's pivot on its own: copied to the RectTransform
            rect.pivot = new Vector2(s.sprite.pivot.x / s.sprite.rect.width, s.sprite.pivot.y / s.sprite.rect.height);
            return;
        }
        Debug.LogWarning("[RetroCursor] No sprite in Styles for the state " + state + ".", this);
    }
}
