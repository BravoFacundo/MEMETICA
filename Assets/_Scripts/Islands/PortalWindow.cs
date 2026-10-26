using System.Collections;
using UnityEngine;

// The way back to the hub (Game): a big window lying on the target island's ground that shows the hub seen from the
// sky, live. It isn't a picture: a camera above the hub copies yours as if the window were a hole into that sky (look
// at it from the side and you see the hub from the side), so walking onto it is stepping onto a hole.
//   Hidden   inside the ground until its pedestal (Open After) went into the earth; then it rises showing the
//            empty window's PNG, and the view loads in, pixelated (an old image loading), down to its Pixelate
//   Open     jump (or step) onto the middle of its screen (Edge Margin inside): it lets you through and you sink into it,
//            looking down, without ever standing on it; its frame and edge you can walk on. Fall In off: stand on it
//            (Stand Time) instead. The pixels go away as you sink
//   Through  when your eyes reach the screen you are already above the hub, seeing the same thing: no cut. Above you
//            a window like it shows where you came from for a moment, then closes (Exit Window). You keep falling
//            and land on the hub's respawn point (Exit) with the slam
// With MemeRecall's Bring For Portal, a meme window left behind comes to your hand before it takes you.
// Made by Memetica > Level > Portal > New Portal Window (where the Scene view looks), inside its island.
// The camera above the hub only draws while you are near and it can be seen.
[DefaultExecutionOrder(1000)]   // after the player and its camera moved this frame
public class PortalWindow : MonoBehaviour
{
    [Section("Where It Leads")]
    [Tooltip("Where you land in the hub (a RespawnPoint: you look toward its arrow)")]
    public RespawnPoint exit;
    [Tooltip("Meters above Exit you come out of the window (and fall from)")]
    public float exitHeight = 10f;

    [Section("Opening")]
    [Tooltip("It rises once this pedestal went into the earth (the target island's, Swallowed). Empty: open from the start")]
    public PedestalRetire openAfter;
    [Tooltip("Seconds after that")]
    public float openDelay = 0.4f;
    [Tooltip("Meters below its place it waits hidden")]
    public float riseDepth = 0.6f;
    [Tooltip("Seconds it takes to rise out of the ground")]
    public float riseTime = 1.4f;
    [Tooltip("Seconds the view takes to load in (empty PNG, then from big pixels down to its Pixelate)")]
    public float tuneTime = 1f;

    public enum Needs { Nothing, MemeWindow, EmptyMemeWindow }

    [Section("Going Through")]
    [Tooltip("What you must carry to go through: nothing, the meme window, or the meme window empty (downloaded). " +
             "Without it, it flickers and won't take you")]
    public Needs needs = Needs.Nothing;
    [Tooltip("The meme window you carry shows its Fall change while you fall through (back to normal when you land)")]
    public bool fallChange = true;
    [Tooltip("The meme window you carry empties once you landed in the hub (its meme downloaded)")]
    public bool emptyOnLanding = false;
    [Tooltip("On: in its middle you fall into it (it never holds you there). Off: you stand on it Stand Time, then sink")]
    public bool fallIn = true;
    [Tooltip("Fall In off: seconds standing on it before you sink")]
    public float standTime = 0.5f;
    [Tooltip("Meters inside the screen's edge you must stand (on the frame or its edge nothing happens)")]
    public float edgeMargin = 0.4f;
    [Tooltip("How fast you sink into it (m/s²)")]
    public float sinkAcceleration = 9f;
    [Tooltip("Seconds your head takes to look down as you sink")]
    public float lookDownTime = 0.3f;
    [Tooltip("Seconds your head takes to look up again after coming out")]
    public float lookUpTime = 0.7f;

    [Section("View")]
    [Tooltip("Size of the view, times your screen's (it goes full size while you go through, so there's no jump)")]
    [Range(0.25f, 1f)] public float resolution = 0.5f;
    [Tooltip("Meters: farther than this the camera above the hub doesn't draw")]
    public float viewDistance = 60f;
    [Range(0f, 1f)] public float scanlines = 0.12f;
    [Tooltip("How pixelated the view is while open (0 = sharp). It goes away as you sink through")]
    [Range(0f, 1f)] public float pixelate = 0.3f;
    [Tooltip("What it shows while it loads or won't take you: the empty meme window's PNG (set by the tool)")]
    public Texture emptyTexture;

    [Section("Exit Window")]
    [Tooltip("Coming out above the hub: a window like this one, above you, shows where you came from, then closes")]
    public bool exitWindow = true;
    [Tooltip("Seconds it stays open")]
    public float exitWindowTime = 1.2f;
    [Tooltip("Seconds it takes to close")]
    public float exitCloseTime = 0.35f;
    [Tooltip("The view shows upside down? Turn this on")]
    public bool flipView = false;

    [Section("Sounds", Closed = true)]
    public AudioClip riseClip;
    [Tooltip("Looped while it is open")]
    public AudioClip humClip;
    [Tooltip("When you start sinking")]
    public AudioClip enterClip;
    [Tooltip("When you come out above the hub (not 3D)")]
    public AudioClip outClip;
    [Tooltip("Standing on it without what it Needs")]
    public AudioClip deniedClip;

    [Section("References", Closed = true)]
    [Tooltip("The quad showing the view (its material: Memetica/PortalScreen)")]
    public Renderer screen;
    [Tooltip("What you stand on")]
    public Collider floor;

    enum State { Hidden, Rising, Open, Sinking }
    State state = State.Hidden;

    static readonly Quaternion Flip = Quaternion.Euler(0f, 0f, 180f);   // in front of this one = behind the exit

    private Camera view;           // above the hub
    private Camera backView;       // the exit window's: above this one
    private RenderTexture backTexture;
    private Material backMaterial;
    private Transform exitRoot;
    private float pixels = 1f;
    private Camera playerCamera;
    private PlayerMove player;
    private PlayerLook look;
    private RenderTexture texture;
    private Material material;
    private AudioSource hum, sfx, flat;
    private bool landed;
    private Renderer[] renderers;
    private Vector3 restLocal;
    private float standing;
    private float staticAmount = 1f, ripple;

    void Start()
    {
        player = FindObjectOfType<PlayerMove>();
        look = FindObjectOfType<PlayerLook>();
        playerCamera = look != null ? look.GetComponent<Camera>() : Camera.main;
        restLocal = transform.localPosition;
        renderers = GetComponentsInChildren<Renderer>(true);
        if (screen != null) material = screen.material;   // its own copy
        if (material != null && emptyTexture != null) material.SetTexture("_EmptyTex", emptyTexture);
        hum = MakeSource(true, 1f);
        sfx = MakeSource(false, 1f);
        flat = MakeSource(false, 0f);

        if (exit == null) { Debug.LogWarning($"[Portal] '{name}' has no Exit (a RespawnPoint in the hub).", this); }
        if (openAfter != null) Hide();
        else { state = State.Open; staticAmount = 0f; pixels = pixelate; StartHum(); }
    }

    void OnDestroy()
    {
        if (texture != null) texture.Release();
        if (backTexture != null) backTexture.Release();
        if (view != null) Destroy(view.gameObject);
        if (backView != null) Destroy(backView.gameObject);
        if (exitRoot != null) Destroy(exitRoot.gameObject);
    }

    //---- Every frame

    void Update()
    {
        if (state == State.Hidden)
        {
            SetShown(false);   // (its island rising shows what it hid: not this yet)
            if (openAfter != null && PedestalGone()) StartCoroutine(Rise());
        }
        if (state == State.Open) WatchPlayer();
    }

    void LateUpdate()
    {
        UpdateView();
        if (material != null)
        {
            material.SetFloat("_Static", staticAmount);
            material.SetFloat("_Pixelate", pixels);
            material.SetFloat("_Ripple", ripple);
            material.SetFloat("_FlipY", flipView ? 1f : 0f);
            if (state != State.Sinking) material.SetFloat("_Scanlines", scanlines);
            if (player != null && screen != null)
            {
                Vector3 local = screen.transform.InverseTransformPoint(player.transform.position);
                material.SetVector("_RippleCenter", new Vector4(local.x + 0.5f, local.y + 0.5f, 0f, 0f));
            }
        }
    }

    bool PedestalGone() => !openAfter.gameObject.activeInHierarchy || (!openAfter.IsUp && !openAfter.IsMoving);

    // Fall In: over its middle (and carrying what it needs) it doesn't hold you: you fall into it, and as your feet reach
    // it you are going through. Not allowed: it holds you and flickers
    private bool passThrough, wasOver;
    private CharacterController playerBody;
    void WatchFallIn()
    {
        if (playerBody == null && player != null) playerBody = player.GetComponent<CharacterController>();
        bool over = player != null && !player.Scripted && OnScreen(player.transform.position, 4f);

        // A meme window left behind comes first (MemeRecall): it takes you once it is in your hand
        MemeRecall recall = MemeRecall.Instance;
        bool waiting = over && recall != null && recall.bringForPortal && CarriedWindow == null && recall.BringLeftBehind();
        bool allowed = over && !waiting && Allowed();
        SetPassThrough(allowed);

        if (over && !waiting && !allowed)   // it won't take you: flickers
        {
            if (!wasOver) Play(deniedClip);
            staticAmount = Random.value < 0.2f ? 0.7f : 0.2f;
            ripple = 0f;
        }
        else
        {
            if (!over && staticAmount > 0f) staticAmount = Mathf.MoveTowards(staticAmount, 0f, Time.deltaTime * 2f);
            ripple = Mathf.MoveTowards(ripple, over ? 1f : 0f, Time.deltaTime * 3f);
        }
        wasOver = over;
        if (allowed && FeetHeight() < 0.1f && exit != null) { SetPassThrough(false); StartCoroutine(GoThrough()); }
    }

    // Its floor lets the player through (in its middle): off while they are over it
    void SetPassThrough(bool on)
    {
        if (on == passThrough || floor == null) return;
        passThrough = on;
        floor.enabled = !on;
    }

    // Meters from the player's feet down to the screen (along its up: it may lean)
    float FeetHeight()
    {
        Vector3 feet = playerBody != null ? playerBody.bounds.center + Vector3.down * playerBody.bounds.extents.y : player.transform.position;
        return Vector3.Dot(feet - transform.position, transform.up);
    }

    // Standing on it long enough: through
    void WatchPlayer()
    {
        if (fallIn) { WatchFallIn(); return; }
        bool on = player != null && !player.Scripted && player.isGrounded && OnScreen(player.transform.position) && StandingOnIt();
        bool before = standing > 0f;
        standing = on ? standing + Time.deltaTime : 0f;

        // A meme window left behind comes first (MemeRecall): it takes you once it is in your hand
        MemeRecall recall = MemeRecall.Instance;
        if (on && recall != null && recall.bringForPortal && CarriedWindow == null && recall.BringLeftBehind())
        {
            standing = Mathf.Min(standing, standTime * 0.5f);
            ripple = Mathf.MoveTowards(ripple, 1f, Time.deltaTime * 3f);
            return;
        }
        if (on && !Allowed())   // it won't take you: flickers
        {
            if (!before) Play(deniedClip);
            staticAmount = Random.value < 0.2f ? 0.7f : 0.2f;
            ripple = 0f;
            return;
        }
        if (!on && staticAmount > 0f && state == State.Open) staticAmount = Mathf.MoveTowards(staticAmount, 0f, Time.deltaTime * 2f);
        ripple = Mathf.MoveTowards(ripple, on ? 1f : 0f, Time.deltaTime * 3f);
        if (on && standing >= standTime && exit != null) StartCoroutine(GoThrough());
    }

    // Carrying what it Needs
    bool Allowed()
    {
        if (needs == Needs.Nothing) return true;
        MemeChanger carried = MemeDock.Instance != null ? MemeDock.Instance.Carried as MemeChanger : null;
        return carried != null && (needs == Needs.MemeWindow || carried.IsEmpty);
    }

    MemeChanger CarriedWindow => MemeDock.Instance != null ? MemeDock.Instance.Carried as MemeChanger : null;

    // Once you landed in the hub (and got up): the window you carry empties, if it should
    IEnumerator AfterLanding()
    {
        yield return null;
        for (float t = 0f; t < 15f && player.Respawning; t += Time.deltaTime) yield return null;
        MemeChanger window = CarriedWindow;
        if (emptyOnLanding && window != null) window.SetMeme(null);
        Arrived?.Invoke();   // the hub's pedestal comes back up (PedestalRetire)
    }

    // You came back through a portal and got up in the hub
    public static event System.Action Arrived;

    // Over the screen (seen from above), Edge Margin inside its edge, and at its height
    bool OnScreen(Vector3 position, float maxHeight = 2.5f)
    {
        if (screen == null) return false;
        Vector3 local = screen.transform.InverseTransformPoint(position);
        Vector3 size = screen.transform.lossyScale;
        float marginX = edgeMargin / Mathf.Max(Mathf.Abs(size.x), 0.01f), marginY = edgeMargin / Mathf.Max(Mathf.Abs(size.y), 0.01f);
        float height = Vector3.Dot(position - transform.position, transform.up);
        return Mathf.Abs(local.x) < 0.5f - marginX && Mathf.Abs(local.y) < 0.5f - marginY && height > -0.2f && height < maxHeight;
    }

    // Its floor right under you (not the island's ground beside it)
    bool StandingOnIt()
    {
        if (floor == null) return true;
        int notPlayer = ~(1 << player.gameObject.layer);
        return Physics.Raycast(player.transform.position, Vector3.down, out RaycastHit hit, 3f, notPlayer, QueryTriggerInteraction.Ignore)
               && hit.collider == floor;   // the first thing under you
    }

    //---- The view: a camera above the hub, where yours would be if this were a hole into that sky

    Vector3 ExitPosition => exit.transform.position + Vector3.up * exitHeight;
    Quaternion ExitRotation => Quaternion.LookRotation(exit.Facing, Vector3.down);   // its face looks down at the hub

    Vector3 MapPoint(Vector3 p) => ExitPosition + ExitRotation * (Flip * (Quaternion.Inverse(transform.rotation) * (p - transform.position)));
    Quaternion MapRotation(Quaternion r) => ExitRotation * Flip * Quaternion.Inverse(transform.rotation) * r;

    void UpdateView()
    {
        bool wanted = exit != null && playerCamera != null && state != State.Hidden && screen != null && screen.enabled &&
                      (state == State.Sinking || (Vector3.Distance(playerCamera.transform.position, transform.position) < viewDistance && screen.isVisible));
        if (!wanted) { if (view != null) view.enabled = false; return; }

        float scale = state == State.Sinking ? 1f : resolution;
        int width = Mathf.Max(64, Mathf.RoundToInt(Screen.width * scale)), height = Mathf.Max(64, Mathf.RoundToInt(Screen.height * scale));
        if (texture == null || texture.width != width || texture.height != height)
        {
            if (texture != null) texture.Release();
            texture = new RenderTexture(width, height, 24) { name = "PortalView" };
            if (view != null) view.targetTexture = texture;
            if (material != null) material.SetTexture("_MainTex", texture);
        }
        if (view == null) MakeViewCamera();

        view.enabled = true;
        view.fieldOfView = playerCamera.fieldOfView;
        view.nearClipPlane = playerCamera.nearClipPlane;
        view.farClipPlane = playerCamera.farClipPlane;
        view.transform.SetPositionAndRotation(MapPoint(playerCamera.transform.position), MapRotation(playerCamera.transform.rotation));
    }

    void MakeViewCamera()
    {
        var go = new GameObject($"{name}_View");
        view = go.AddComponent<Camera>();
        view.CopyFrom(playerCamera);
        view.depth = playerCamera.depth - 1;   // before yours, this same frame
        view.targetTexture = texture;
        view.cullingMask = playerCamera.cullingMask;
        if (material != null) material.SetTexture("_MainTex", texture);
    }

    //---- Opening

    void Hide()
    {
        state = State.Hidden;
        SetShown(false);
        Place(riseDepth);
    }

    IEnumerator Rise()
    {
        state = State.Rising;
        yield return new WaitForSeconds(openDelay);
        SetShown(true);
        staticAmount = 1f;
        pixels = 1f;
        Play(riseClip);
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            float p = t / riseTime;
            Place(riseDepth * (1f - p * p * (3f - 2f * p)), Random.insideUnitSphere * 0.02f * (1f - p));
            yield return null;
        }
        Place(0f);
        state = State.Open;
        StartHum();
        // The view loads in: the empty PNG goes, the pixels get smaller in steps (an old image loading)
        for (float t = 0f; t < tuneTime; t += Time.deltaTime)
        {
            float p = t / tuneTime;
            staticAmount = Mathf.Clamp01(1f - p * 3f);
            pixels = Mathf.Lerp(1f, pixelate, Mathf.Floor(p * 6f) / 6f);
            yield return null;
        }
        staticAmount = 0f;
        pixels = pixelate;
    }

    void Place(float depth, Vector3 jitter = default)
    {
        Vector3 down = transform.parent != null ? transform.parent.InverseTransformDirection(Vector3.down) : Vector3.down;
        transform.localPosition = restLocal + down * depth + jitter;
    }

    void SetShown(bool on)
    {
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers) if (r != null && r.enabled != on) r.enabled = on;
        if (floor != null && floor.enabled != on) floor.enabled = on;
    }

    //---- Going through

    IEnumerator GoThrough()
    {
        float entrySpeed = player.FallSpeed;   // (jumped in: it goes on at the speed you came)
        state = State.Sinking;
        ripple = 1f;
        Play(enterClip);
        player.InputLocked = true;
        look.LookLocked = true;
        player.BeginScripted();

        // Sinking, faster and faster, the head going down to look at the screen
        float speed = entrySpeed, near = Mathf.Max(playerCamera.nearClipPlane * 2f, 0.08f);
        for (float t = 0f; ; t += Time.deltaTime)
        {
            float down = Mathf.Clamp01(t / Mathf.Max(lookDownTime, 0.01f));
            look.SetPose(down * down * (3f - 2f * down), 0f, 0f);
            material.SetFloat("_Scanlines", Mathf.Lerp(scanlines, 0f, down));   // fade out: you are almost there
            pixels = Mathf.Lerp(pixelate, 0f, down);
            speed += sinkAcceleration * Time.deltaTime;
            player.transform.position += Vector3.down * speed * Time.deltaTime;
            float eyes = Vector3.Dot(playerCamera.transform.position - transform.position, transform.up);
            if (eyes <= near) break;
            yield return null;
        }

        // Through: the same view, from above the hub
        Vector3 body = MapPoint(player.transform.position);
        Vector3 facing = Vector3.ProjectOnPlane(MapRotation(player.transform.rotation) * Vector3.forward, Vector3.up);
        player.FallFrom(body, Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing.normalized : exit.Facing, Vector3.up), speed, exit.transform);
        if (outClip != null) flat.PlayOneShot(outClip);
        if (fallChange && CarriedWindow != null) player.AnnounceFall();   // the window you carry: its Fall change
        StartCoroutine(AfterLanding());
        if (exitWindow) StartCoroutine(ExitWindow());
        state = State.Open;
        pixels = pixelate;
        standing = 0f;
        ripple = 0f;

        // Looking up again while you fall (landing first: the slam takes the head from there)
        landed = false;
        PlayerMove.RespawnTouchedDown += OnLanded;
        for (float t = 0f; t < lookUpTime && !landed; t += Time.deltaTime)
        {
            float p = 1f - t / lookUpTime;
            look.SetPose(p * p, 0f, 0f);
            yield return null;
        }
        PlayerMove.RespawnTouchedDown -= OnLanded;
        RespawnSlam slam = player.GetComponent<RespawnSlam>();
        if (landed && slam != null && slam.isActiveAndEnabled) yield break;   // (it locks and frees you itself)
        look.SetPose(0f, 0f, 0f);
        look.LookLocked = false;
        player.InputLocked = false;
    }

    void OnLanded() => landed = true;

    //---- Exit window: above you as you come out, showing where you came from (its view pixelated), then it closes

    Vector3 MapBack(Vector3 p) => transform.position + transform.rotation * (Flip * (Quaternion.Inverse(ExitRotation) * (p - ExitPosition)));
    Quaternion MapBackRotation(Quaternion r) => transform.rotation * Flip * Quaternion.Inverse(ExitRotation) * r;

    IEnumerator ExitWindow()
    {
        if (screen == null || screen.transform.parent == null) yield break;
        if (exitRoot == null)
        {
            exitRoot = new GameObject($"{name}_ExitWindow").transform;
            Transform copy = Instantiate(screen.transform.parent.gameObject, exitRoot).transform;   // the same frame and screen
            copy.localPosition = screen.transform.parent.localPosition;
            copy.localRotation = screen.transform.parent.localRotation;
            copy.localScale = screen.transform.parent.localScale;
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                if (r.name == screen.name) { backMaterial = new Material(material); r.sharedMaterial = backMaterial; }
            var go = new GameObject($"{name}_BackView");
            backView = go.AddComponent<Camera>();
            backView.CopyFrom(playerCamera);
            backView.depth = playerCamera.depth - 1;
        }
        exitRoot.gameObject.SetActive(true);
        exitRoot.SetPositionAndRotation(ExitPosition, ExitRotation);   // its face looks down at you
        exitRoot.localScale = Vector3.one;

        for (float t = 0f; t < exitWindowTime + exitCloseTime; t += Time.deltaTime)
        {
            float closing = Mathf.Clamp01((t - exitWindowTime) / Mathf.Max(exitCloseTime, 0.01f));
            UpdateBackView();
            if (backMaterial != null)
            {
                backMaterial.SetFloat("_Pixelate", Mathf.Lerp(Mathf.Max(pixelate, 0.4f), 1f, closing));
                backMaterial.SetFloat("_Static", closing);
                backMaterial.SetFloat("_Ripple", 0f);
                backMaterial.SetFloat("_Scanlines", scanlines);
            }
            float size = 1f - closing * closing;
            exitRoot.localScale = new Vector3(size, size, size);
            yield return null;
        }
        exitRoot.gameObject.SetActive(false);
        backView.enabled = false;
    }

    // A camera above this window, where yours would be looking up through the exit one (only what is above it)
    void UpdateBackView()
    {
        int width = Mathf.Max(64, Mathf.RoundToInt(Screen.width * 0.5f)), height = Mathf.Max(64, Mathf.RoundToInt(Screen.height * 0.5f));
        if (backTexture == null || backTexture.width != width || backTexture.height != height)
        {
            if (backTexture != null) backTexture.Release();
            backTexture = new RenderTexture(width, height, 24) { name = "PortalBackView" };
            backView.targetTexture = backTexture;
            if (backMaterial != null) backMaterial.SetTexture("_MainTex", backTexture);
        }
        backView.enabled = true;
        backView.fieldOfView = playerCamera.fieldOfView;
        backView.transform.SetPositionAndRotation(MapBack(playerCamera.transform.position), MapBackRotation(playerCamera.transform.rotation));
        backView.ResetProjectionMatrix();
        backView.projectionMatrix = backView.CalculateObliqueMatrix(ClipPlane(backView, transform.position, transform.up));   // nothing below this window
    }

    static Vector4 ClipPlane(Camera camera, Vector3 point, Vector3 normal)
    {
        Matrix4x4 m = camera.worldToCameraMatrix;
        Vector3 p = m.MultiplyPoint(point + normal * 0.05f);
        Vector3 n = m.MultiplyVector(normal).normalized;
        return new Vector4(n.x, n.y, n.z, -Vector3.Dot(p, n));
    }

    //---- Sound

    AudioSource MakeSource(bool loop, float spatial)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = spatial;
        source.minDistance = 4f;
        return source;
    }

    void StartHum()
    {
        if (humClip == null) return;
        hum.clip = humClip;
        hum.Play();
    }

    void Play(AudioClip clip)
    {
        if (clip != null) sfx.PlayOneShot(clip);
    }

    // Testing: right click on the component while playing
    [ContextMenu("Open Now")]
    void OpenNow() { if (state == State.Hidden) { openDelay = 0f; StartCoroutine(Rise()); } }

#if UNITY_EDITOR
    // Where it leads: a line up to where you come out above the hub
    void OnDrawGizmosSelected()
    {
        if (exit == null) return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f);
        Gizmos.DrawLine(transform.position, ExitPosition);
        Gizmos.DrawWireSphere(ExitPosition, 0.6f);
        Gizmos.DrawLine(ExitPosition, exit.transform.position);
    }
#endif
}
