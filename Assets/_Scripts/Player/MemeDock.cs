using System.Collections;
using UnityEngine;

// The meme window between the world and the HUD (Game). Its HUD version (HudWindow, on the HUD canvas) gives where
// and how big it shows on the screen; from that the 3D window (MemeWindow) gets a place in front of the camera where
// it looks the same (HudPose).
//   Picked up   the 3D one flies there and, at Swap At of the flight (85%), hides: the HUD one shows instead
//   Let go      (its Minimize or Close button, Q, a click): the opposite. The 3D one starts where the HUD one is,
//               hidden for the first part of its flight (Swap Back At, 15%), then shows and the HUD one hides; it
//               flies to the Drop Pivot and falls (PlayerLook). Maximized, it starts as big as it showed
//   Placed      on a pedestal, the same, flying to its socket (Pedestal)
// While on the screen the 3D one rides with the camera, hidden and without physics: it is still carried (it notices
// islands and falls, and keeps changing: the HUD one shows its changes).
// Without a HUD version (Game_Old) nothing of this happens: the window is carried as any meme. Goes on the Player.
public class MemeDock : MonoBehaviour
{
    public static MemeDock Instance { get; private set; }

    [Tooltip("Let go (as its Minimize button)")]
    public KeyCode key = KeyCode.Q;
    [Tooltip("Seconds it takes to fly to its HUD place")]
    public float flyTime = 0.22f;
    [Tooltip("Picking up: part of the flight when the 3D one hides and the HUD one shows (before arriving: the swap isn't noticed)")]
    [Range(0.5f, 1f)] public float swapAt = 0.85f;
    [Tooltip("Letting go: part of the flight the 3D one stays hidden (the HUD one still showing)")]
    [Range(0f, 0.5f)] public float swapBackAt = 0.15f;
    [Tooltip("The 3D one never comes closer to the camera than this (a big HUD window: it grows instead)")]
    public float minDistance = 0.45f;

    public Meme Carried { get; private set; }
    public bool OnScreen(Meme meme) => meme != null && meme == Carried;

    private PlayerLook look;
    private Camera cam;
    private Coroutine flying, swappingBack;
    private Meme onScreen;       // the one riding with the camera now
    private Vector3 worldScale;  // its size in the world
    private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
    private Renderer[] renderers;

    public Camera Cam => cam;

    private HudWindow Hud => HudWindow.Instance;

    void Awake() => Instance = this;

    void Start()
    {
        cam = Camera.main;
        look = cam.GetComponent<PlayerLook>();
    }

    void Update()
    {
        if (!PauseMenu.GameIsPaused && Input.GetKeyDown(key) && Carried != null) look.DropObject();
    }

    //---- In the hand (PlayerLook)

    // Whether it goes to the HUD (a meme window, with its HUD version on the canvas)
    public bool CanCarry(GameObject meme) => Hud != null && meme.TryGetComponent(out MemeWindow _);

    // Picked up: toward its HUD place, in Seconds (0: Fly Time; MemeRecall: longer from far away)
    public void Carry(Meme meme, float seconds = 0f)
    {
        Carried = meme;
        EnterScreen(meme);
        if (flying != null) StopCoroutine(flying);
        flying = StartCoroutine(FlyToHud(meme, seconds > 0f ? seconds : flyTime));
    }

    // Leaving the hand (let go, placed): the 3D one starts where the HUD one is, hidden for the first Swap Back At of
    // a flight of Flight Time seconds. Returns its size in the world (it starts as big as the HUD one shows)
    public Vector3 Uncarry(Meme meme, float flightTime)
    {
        if (Carried != meme) return meme.transform.lossyScale;
        Carried = null;
        if (flying != null) { StopCoroutine(flying); flying = null; }
        PlaceAtHud(meme.transform);
        LeaveScreen(meme);
        if (swappingBack != null) StopCoroutine(swappingBack);
        swappingBack = StartCoroutine(SwapBack(flightTime * swapBackAt));
        return worldScale;
    }

    // Gone from your hand for good (an optional meme, once in the MemeDex: DexCollectible). Its HUD version hides
    public void Discard(Meme meme)
    {
        if (Carried != meme) return;
        Carried = null;
        if (flying != null) { StopCoroutine(flying); flying = null; }
        if (onScreen == meme) { onScreen = null; meme.transform.SetParent(null, true); }
        Hud.Show(false);
    }

    //---- The swap

    void EnterScreen(Meme meme)
    {
        if (swappingBack != null) { StopCoroutine(swappingBack); swappingBack = null; SetVisible(true); Hud.Show(false); }
        if (onScreen == meme) return;
        onScreen = meme;
        worldScale = meme.transform.lossyScale;
        renderers = meme.GetComponentsInChildren<Renderer>(true);
        if (meme is MemeWindow window) window.StopFacing();
        SetPhysics(meme, false);
        meme.transform.SetParent(cam.transform, true);   // rides with the camera
    }

    // Back in the world (still hidden: SwapBack shows it)
    void LeaveScreen(Meme meme)
    {
        if (onScreen != meme) return;
        onScreen = null;
        meme.transform.SetParent(null, true);
        SetPhysics(meme, true);
    }

    IEnumerator SwapBack(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SetVisible(true);
        Hud.Show(false);
        swappingBack = null;
    }

    // Toward its HUD place; near the end the 3D one hides and the HUD one shows
    IEnumerator FlyToHud(Meme meme, float flyTime)
    {
        Transform t = meme.transform;
        Vector3 fromPosition = t.localPosition;
        Quaternion fromRotation = t.localRotation;
        Vector3 fromScale = t.localScale;
        bool swapped = false;
        for (float time = 0f; time < flyTime; time += Time.deltaTime)
        {
            float p = Ease(time / flyTime);
            HudPose(out Vector3 position, out Quaternion rotation, out Vector3 scale);
            t.localPosition = Vector3.LerpUnclamped(fromPosition, position, p);
            t.localRotation = Quaternion.SlerpUnclamped(fromRotation, rotation, p);
            t.localScale = Vector3.LerpUnclamped(fromScale, scale, p);
            if (!swapped && time / flyTime >= swapAt) { Swap(meme); swapped = true; }
            yield return null;
        }
        PlaceAtHud(t);
        if (!swapped) Swap(meme);
        flying = null;
    }

    void Swap(Meme meme)
    {
        SetVisible(false);
        if (meme is MemeWindow window) window.MirrorTo(Hud);
        Hud.Show(true);
    }

    void PlaceAtHud(Transform t)
    {
        HudPose(out Vector3 position, out Quaternion rotation, out Vector3 scale);
        t.localPosition = position;
        t.localRotation = rotation;
        t.localScale = scale;
    }

    // Where the 3D window looks just like its HUD version, in the camera's space: in its middle, facing the camera,
    // as far as its height fills the HUD one's height (never closer than Min Distance: then it grows).
    // Scale: local to the camera (the camera itself is at scale 1)
    void HudPose(out Vector3 position, out Quaternion rotation, out Vector3 scale) => HudPose(Hud, onScreen, worldScale, out position, out rotation, out scale);

    // The same for any HUD window and meme window (an optional meme's own place: DexCollectible)
    public void HudPose(HudWindow hud, Meme meme, Vector3 worldScale, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        hud.ViewportRect(out Vector2 center, out float height);
        float tall = WindowHeight(meme, worldScale);
        float viewTall = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Max(height, 0.001f);   // per meter away
        float distance = Mathf.Max(tall / viewTall, minDistance);
        scale = worldScale * (viewTall * distance / tall);
        position = cam.transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(center.x, center.y, distance)));
        rotation = Quaternion.Euler(0f, 180f, 0f);   // its front toward the camera
    }

    // Meters tall of its frame (the picture its buttons are on), at its size in the world
    static float WindowHeight(Meme meme, Vector3 worldScale)
    {
        if (meme != null && meme.TryGetComponent(out WindowControls controls) && controls.picture != null)
            return controls.picture.localScale.y * worldScale.y;   // (the Window between them is at scale 1)
        return 0.7f * worldScale.y;
    }

    void SetVisible(bool on)
    {
        if (renderers == null) return;
        foreach (Renderer r in renderers) if (r != null) r.enabled = on;
    }

    // On the screen: no physics (kinematic, no colliders). Back: as it was
    void SetPhysics(Meme meme, bool on)
    {
        if (meme.TryGetComponent(out Rigidbody body))
        {
            if (!on) { if (body.interpolation != RigidbodyInterpolation.None) interpolation = body.interpolation; body.interpolation = RigidbodyInterpolation.None; }   // (off on a pedestal: keeps the one it had)
            else body.interpolation = interpolation;
            body.isKinematic = !on;
        }
        foreach (Collider c in meme.GetComponentsInChildren<Collider>(true)) c.enabled = on;
    }

    public static float Ease(float p) => p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f;
}
