using System.Collections;
using TMPro;
using UnityEngine;

// Meme as a window of the game's retro OS (Game), made of your PNG layers: the frame in front (its middle
// transparent) and behind it the Content (shader Memetica/MemeWindow: its own background, the meme on it), with a body
// behind both (the depth). When and which change shows: MemeChanger (the same rules as the orb). It can start empty
// (no Data): the Content's background only, until MemeRandomizer's raffle gives it a meme.
// How it behaves:
//   Placed      it stays exactly where it was put in the scene (a tree branch, a shelf…), still, as it was turned,
//               until you pick it up (you may have to jump for it)
//   Held        it flies to its HUD version (HudWindow, on the HUD canvas), which shows instead while you carry it (MemeDock)
//   Dropped     After Drop: Fall Face Up, it falls like a box and, once still, lies down with the meme facing up so
//               you always see it; Float, it stays floating where you let it go, bobbing and facing you
//   Pedestal    it faces you
//   Let go      its Minimize / Close buttons or Q: it comes out of the HUD and falls. Its buttons: HudWindow (in the
//               HUD), WindowControls (in the world)
// Changes: the meme breaks into ever bigger squares (an old image loading), the new one comes in coarse and sharpens.
// Hierarchy (Memetica > Memes > Make MemeWindow Prefab from Selected PNGs):
//   Meme_Window (this, WindowControls, Rigidbody, BoxCollider, tag Meme)
//   └── Window (FaceCamera: on only while it faces you)
//       ├── Content (quad: the meme, Memetica/MemeWindow)
//       ├── Frame (quad: your frame PNG, in front)
//       └── Body (the depth)
public class MemeWindow : MemeChanger
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int PixelateId = Shader.PropertyToID("_Pixelate");
    private static readonly int GlowId = Shader.PropertyToID("_Glow");
    private static readonly int FilledId = Shader.PropertyToID("_Filled");
    private static readonly int FillId = Shader.PropertyToID("_Fill");

    public enum AfterDropMode { FallFaceUp, Float }

    [Section("Behaviour")]
    [Tooltip("Fall Face Up: falls like a box and lies down showing the meme. Float: stays floating where you let it go, facing you")]
    public AfterDropMode afterDrop = AfterDropMode.FallFaceUp;
    [Tooltip("Float: meters it bobs up and down")]
    public float floatBob = 0.05f;
    [Tooltip("Fall Face Up: seconds it takes to lie down once it stopped")]
    public float lieDownTime = 0.3f;
    [Tooltip("In the hand: 0 = straight ahead and upright on the screen, 1 = right at the camera. A little gives it depth")]
    [Range(0f, 1f)] public float heldTurn = 0.2f;

    [Section("References")]
    [Tooltip("The Content's renderer, where the meme shows (material Memetica/MemeWindow)")]
    public Renderer window;
    [Tooltip("Turns the window toward the camera (on the Window child)")]
    public FaceCamera facing;
    [Tooltip("The meme's name on its title bar (TextMeshPro: Memetica > Memes > Add Window Titles)")]
    public TMP_Text titleText;

    private MaterialPropertyBlock block;
    private bool loose;          // falling freely (dropped, or after its meteor): it lies face up once still
    private bool floating;
    private Vector3 floatAnchor;
    private float stillTime;
    private Coroutine lyingDown;

    // Stays where it was placed; an optional meme with Use Physics: rises frozen with its island, then falls (as the orbs)
    protected override bool KeepsItsPlace => !TryGetComponent(out DexCollectible dex) || !dex.usePhysics;
    protected override bool CanBeEmpty => true;
    protected override bool ChangesOnce => true;   // Game: each change once, Moves from the island's number (MemeChanger)

    protected override bool SetUpLook()
    {
        block = new MaterialPropertyBlock();
        Face(false);   // placed: as it was put
        if (window == null) { Debug.LogError($"[MemeWindow] '{name}' has no Window reference.", this); return false; }
        // Its title over the frame from every angle: both are transparent and almost on the same plane, so by distance
        // alone the frame was drawn over it when seen from the side
        if (titleText is TextMeshPro worldTitle && worldTitle.TryGetComponent(out MeshRenderer titleRenderer)) titleRenderer.sortingOrder = TitleOrder;
        return true;
    }

    protected override void Update()
    {
        base.Update();
        if (IsHeld || CurrentPedestal != null) return;
        if (floating) { transform.position = floatAnchor + Vector3.up * (Mathf.Sin(Time.time * 2f) * floatBob); return; }
        if (loose) WaitToLieDown();
    }

    //---- Behaviour

    protected override void OnPickedUp()
    {
        base.OnPickedUp();
        floating = loose = false;
        if (lyingDown != null) { StopCoroutine(lyingDown); lyingDown = null; }
        if (OnScreen) Face(false); else Face(true, true);   // on the screen (its HUD version shows): still
    }

    // Still, as its parent turns it (MemeDock)
    public void StopFacing() => Face(false);

    protected override void OnDropped()
    {
        if (afterDrop == AfterDropMode.Float)
        {
            floating = true;
            floatAnchor = transform.position;
            body.isKinematic = true;
            return;
        }
        Face(false);   // a box again, turned as you saw it
        loose = true;
        stillTime = 0f;
    }

    protected override void OnFell(bool withPlayer)
    {
        base.OnFell(withPlayer);
        if (withPlayer) return;
        Face(false);   // after its meteor: lies down where it landed
        floating = false;
        loose = true;
        stillTime = 0f;
    }

    // Once still after falling, it lies down with the meme facing up. Not an optional meme (DexCollectible)
    private bool LiesFaceUp => !TryGetComponent(out DexCollectible _);
    protected override bool SnapsToGround => false;   // exactly where you placed it (hidden in the ground, on a branch…)
    protected override bool ReadyToRest => !loose && lyingDown == null;   // lies face up first, then rests on its island

    protected override void OnReleasedFromIsland()
    {
        Face(false);   // falls like a box, then lies down showing the meme
        floating = false;
        loose = true;
        stillTime = 0f;
    }

    protected override void OnPlaced(Pedestal pedestal)
    {
        floating = loose = false;
        Face(true);
    }

    // On: the window turns to the camera (in the hand: mostly ahead, upright on the screen; elsewhere: right at it,
    // upright in the world). Off: the whole meme takes the window's turn (what you saw) and is a box again
    void Face(bool on, bool held = false)
    {
        if (facing == null) return;
        facing.towardCamera = held ? heldTurn : 1f;
        facing.cameraUp = held;
        if (!on && facing.enabled)
        {
            Quaternion seen = facing.transform.rotation;
            facing.transform.localRotation = Quaternion.identity;
            transform.rotation = seen;
        }
        if (!on) facing.transform.localRotation = Quaternion.identity;
        facing.enabled = on;
    }

    // Once it has stopped moving for a moment: lies down with the meme facing up
    void WaitToLieDown()
    {
        if (!LiesFaceUp) { loose = false; return; }   // an optional meme: it stays as it landed (you may hide it facing away)
        if (body.isKinematic || lyingDown != null) return;
        bool still = body.velocity.magnitude < 0.15f && body.angularVelocity.magnitude < 0.5f;
        stillTime = still ? stillTime + Time.deltaTime : 0f;
        if (stillTime < 0.25f) return;
        loose = false;
        lyingDown = StartCoroutine(LieDown());
    }

    IEnumerator LieDown()
    {
        // The front (the picture) up; its top toward where its top was pointing
        Vector3 top = Vector3.ProjectOnPlane(transform.up, Vector3.up);
        if (top.sqrMagnitude < 0.001f) top = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        Quaternion to = Quaternion.LookRotation(Vector3.up, top.normalized);

        // Resting on the ground under it, its back on it
        Vector3 toPosition = transform.position;
        if (TryGetComponent(out BoxCollider box) && GroundBelow(out float groundY))
        {
            Vector3 scale = transform.lossyScale;
            float halfThickness = box.size.z * 0.5f * Mathf.Abs(scale.z);
            float centerOffset = (to * Vector3.Scale(box.center, scale)).y;
            toPosition.y = groundY + halfThickness - centerOffset + 0.005f;
        }

        body.isKinematic = true;
        Quaternion from = transform.rotation;
        Vector3 fromPosition = transform.position;
        for (float t = 0f; t < lieDownTime; t += Time.deltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / lieDownTime);
            transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, p), Quaternion.Slerp(from, to, p));
            yield return null;
        }
        transform.SetPositionAndRotation(toPosition, to);
        body.isKinematic = false;
        lyingDown = null;
    }

    // The nearest solid thing under its middle that isn't itself
    bool GroundBelow(out float y)
    {
        y = 0f;
        float nearest = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position + Vector3.up * 0.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
            nearest = hit.distance;
            y = hit.point.y;
        }
        return nearest < float.MaxValue;
    }

    //---- Look

    // What it shows also goes to its HUD version while it is on the screen (HudWindow)
    private Texture shownTexture;
    private float shownCover;
    private bool OnScreen => MemeDock.Instance != null && MemeDock.Instance.OnScreen(this);

    // Its title: the image's Window Title (Trollface.png…; also the raffle's candidates), else the meme's axis (MemeData
    // Title, else its asset's name). Title Shows Change (MemeSettings, hidden): also which change ("Troll · Move 2")
    private string shownTitle = "";
    string TitleOf(MemeImage image)
    {
        if (image != null && !string.IsNullOrEmpty(image.windowTitle)) return image.windowTitle;
        if (Data == null) return "";
        string title = !string.IsNullOrEmpty(Data.title) ? Data.title : Data.name.Replace("MemeData_", "");
        if (Settings == null || !Settings.titleShowsChange || image == Data.baseImage) return title;
        if (image == Data.fall) return title + " · Fall";
        if (image == Data.leave) return title + " · Leave";
        int move = Data.variants.IndexOf(image) + 1;
        return move > 0 ? $"{title} · Move {move}" : title;
    }

    void SetTitle(string title)
    {
        shownTitle = title;
        if (titleText != null && titleText.text != title) titleText.text = title;
        if (OnScreen && HudWindow.Instance != null) HudWindow.Instance.SetTitle(title);
    }

    // Fill (MemeData): this image covers the whole window
    const int TitleOrder = 10;   // (Order in Layer: after the frame's)
    private bool shownFill;
    protected override void OnImageShown(MemeImage image)
    {
        shownFill = Data != null && Data.Fills(image);
        SetTitle(TitleOf(image));
        Set(b => b.SetFloat(FillId, shownFill ? 1f : 0f));
        if (OnScreen && HudWindow.Instance != null) HudWindow.Instance.SetFill(shownFill);
    }

    protected override void ShowTexture(Texture texture)
    {
        shownTexture = texture;
        Set(b => { b.SetTexture(MainTexId, texture); b.SetFloat(FilledId, 1f); });
        if (OnScreen && HudWindow.Instance != null) HudWindow.Instance.SetTexture(texture);
    }

    protected override void ShowEmpty()
    {
        shownTexture = null;
        SetTitle("");
        Set(b => b.SetFloat(FilledId, 0f));
        if (OnScreen && HudWindow.Instance != null) HudWindow.Instance.SetEmpty();
    }

    protected override void SetGlow(float amount)
    {
        Set(b => b.SetFloat(GlowId, amount * 0.5f));
        if (OnScreen && HudWindow.Instance != null) HudWindow.Instance.SetGlow(amount);
    }

    // Its HUD version shows what it shows now (MemeDock, when it swaps)
    public void MirrorTo(HudWindow hud)
    {
        if (hud == null) return;
        hud.SetFill(shownFill);
        hud.SetTitle(shownTitle);
        if (shownTexture != null) hud.SetTexture(shownTexture); else hud.SetEmpty();
        hud.SetCover(shownCover);
        hud.SetGlow(0f);
    }

    // Coming up the meme breaks into squares, going away the new one sharpens
    protected override void SetCover(float amount, bool rising)
    {
        shownCover = amount;
        Set(b => b.SetFloat(PixelateId, amount));
        if (OnScreen && HudWindow.Instance != null) HudWindow.Instance.SetCover(amount);
    }

    protected override void PreviewInEditor(MemeData meme)
    {
#if UNITY_EDITOR
        if (window == null) return;
        Texture picture = meme != null && meme.baseImage != null ? meme.baseImage.picture : null;   // (a video-only base: no preview)
        var preview = new MaterialPropertyBlock();
        window.GetPropertyBlock(preview);
        if (picture != null) preview.SetTexture(MainTexId, picture);
        preview.SetFloat(FilledId, picture != null ? 1f : 0f);
        window.SetPropertyBlock(preview);
#endif
    }

    void Set(System.Action<MaterialPropertyBlock> change)
    {
        if (window == null) return;
        if (block == null) block = new MaterialPropertyBlock();   // (a change can come before its look was set up)
        window.GetPropertyBlock(block);
        change(block);
        window.SetPropertyBlock(block);
    }
}
