using System.Collections;
using UnityEngine;

// Meme as a window of the game's retro OS (Game): a flat picture of a window (Art/Models/MemeWindow/MemeWindow_Frame.png,
// shader Memetica/MemeWindow) with the meme in its panel. When and which change shows: MemeChanger (the same rules as
// the orb). It can start empty (no Data): the panel as drawn, until MemeRandomizer's raffle gives it a meme.
// How it behaves:
//   Placed      it stays exactly where it was put in the scene (a tree branch, a shelf…), still, as it was turned,
//               until you pick it up (you may have to jump for it)
//   Held        it faces you
//   Dropped     After Drop: Fall Face Up, it falls like a box and, once still, lies down with the meme facing up so
//               you always see it; Float, it stays floating where you let it go, bobbing and facing you
//   Pedestal    it faces you
// Changes: the meme breaks into ever bigger squares (an old image loading), the new one comes in coarse and sharpens.
// Hierarchy (Memetica > Memes: make MemeWindow prefab):
//   Meme_Window (this, Rigidbody, BoxCollider, tag Meme)
//   └── Window (FaceCamera: on only while it faces you)
//       └── Picture (a quad, its front toward the Window's forward)
public class MemeWindow : MemeChanger
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int PixelateId = Shader.PropertyToID("_Pixelate");
    private static readonly int GlowId = Shader.PropertyToID("_Glow");
    private static readonly int FilledId = Shader.PropertyToID("_Filled");

    public enum AfterDropMode { FallFaceUp, Float }

    [Section("Behaviour")]
    [Tooltip("Fall Face Up: falls like a box and lies down showing the meme. Float: stays floating where you let it go, facing you")]
    public AfterDropMode afterDrop = AfterDropMode.FallFaceUp;
    [Tooltip("Float: meters it bobs up and down")]
    public float floatBob = 0.05f;
    [Tooltip("Fall Face Up: seconds it takes to lie down once it stopped")]
    public float lieDownTime = 0.3f;

    [Section("References")]
    [Tooltip("The window picture's renderer (material Memetica/MemeWindow)")]
    public Renderer window;
    [Tooltip("Turns the window toward the camera (on the Window child)")]
    public FaceCamera facing;

    private MaterialPropertyBlock block;
    private bool loose;          // falling freely (dropped, or after its meteor): it lies face up once still
    private bool floating;
    private Vector3 floatAnchor;
    private float stillTime;
    private Coroutine lyingDown;

    protected override bool KeepsItsPlace => true;
    protected override bool CanBeEmpty => true;

    protected override bool SetUpLook()
    {
        block = new MaterialPropertyBlock();
        Face(false);   // placed: as it was put
        if (window == null) { Debug.LogError($"[MemeWindow] '{name}' has no Window reference.", this); return false; }
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
        Face(true);
    }

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

    protected override void OnPlaced(Pedestal pedestal)
    {
        floating = loose = false;
        Face(true);
    }

    // On: the window turns to the camera. Off: the whole meme takes the window's turn (what you saw) and is a box again
    void Face(bool on)
    {
        if (facing == null) return;
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

    protected override void ShowTexture(Texture texture) => Set(b => { b.SetTexture(MainTexId, texture); b.SetFloat(FilledId, 1f); });

    protected override void ShowEmpty() => Set(b => b.SetFloat(FilledId, 0f));

    protected override void SetGlow(float amount) => Set(b => b.SetFloat(GlowId, amount * 0.5f));

    // Coming up the meme breaks into squares, going away the new one sharpens
    protected override void SetCover(float amount, bool rising) => Set(b => b.SetFloat(PixelateId, amount));

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
        window.GetPropertyBlock(block);
        change(block);
        window.SetPropertyBlock(block);
    }
}
