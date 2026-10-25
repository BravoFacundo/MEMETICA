using UnityEngine;

// A slow ring pan around the world from up high, over everything you unlocked (the islands and bridges that are up):
// the view behind the end screens (GameManager: win and lose), framed like the pause (Canvas_Frame). Later the menu's
// background too (call Begin).
// It looks at Pivot (empty: the world's 0,0,0) from as far as needed to fit all of that (an island 150 m away too),
// Elevation degrees from above, turning Speed degrees per second. It starts from your own view and glides into the
// ring (Blend Time). Its own camera: yours stays where it was.
// Goes in Managers (or anywhere in the scene).
public class OrbitView : MonoBehaviour
{
    [Tooltip("Starts by itself when the run ends (win or lose)")]
    public bool onGameEnd = true;
    [Tooltip("The middle of the ring (empty: the world's 0, 0, 0)")]
    public Transform pivot;
    [Tooltip("Degrees per second around the ring")]
    public float speed = 4f;
    [Tooltip("Degrees it looks down at the middle")]
    [Range(10f, 85f)] public float elevation = 35f;
    [Tooltip("Room around what it shows (1 = tight)")]
    public float margin = 1.1f;
    [Tooltip("Meters: it shows at least this far around the middle")]
    public float minRadius = 40f;
    [Tooltip("Seconds from your view to the ring")]
    public float blendTime = 3f;
    [Tooltip("The pause frame around it (Canvas_Frame)")]
    public bool framed = true;

    public bool IsShowing { get; private set; }

    private Camera view, source;
    private float angle, started, radius;
    private Vector3 fromPosition;
    private Quaternion fromRotation;

    void OnEnable() => GameManager.StateChanged += OnGameState;
    void OnDisable() => GameManager.StateChanged -= OnGameState;

    void OnGameState(GameManager.State state)
    {
        if (onGameEnd && (state == GameManager.State.Won || state == GameManager.State.Lost)) Begin();
    }

    //---- Showing it

    public void Begin()
    {
        if (IsShowing) return;
        PlayerLook look = FindObjectOfType<PlayerLook>();
        source = look != null ? look.GetComponent<Camera>() : Camera.main;
        if (source == null) { Debug.LogWarning("[OrbitView] No camera to start from.", this); return; }

        if (view == null)
        {
            view = new GameObject("OrbitView_Camera").AddComponent<Camera>();
            view.CopyFrom(source);
            view.depth = source.depth + 1;
        }
        view.enabled = true;
        source.enabled = false;   // (yours stays; its listener still hears)

        radius = Mathf.Max(minRadius, UnlockedRadius());
        view.farClipPlane = Mathf.Max(source.farClipPlane, Distance() * 3f);
        fromPosition = source.transform.position;
        fromRotation = source.transform.rotation;
        Vector3 flat = fromPosition - Center; flat.y = 0f;   // starts on the ring where you are
        angle = flat.sqrMagnitude > 0.01f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : source.transform.eulerAngles.y + 180f;
        started = Time.unscaledTime;
        if (framed) PauseMenu.Framed = true;
        GrassField.SetFarView(view, Distance() + radius * 2f);   // the grass out there too
        IsShowing = true;
    }

    public void End()
    {
        if (!IsShowing) return;
        IsShowing = false;
        if (view != null) view.enabled = false;
        if (source != null) source.enabled = true;
        if (framed) PauseMenu.Framed = false;
        GrassField.SetFarView(null, 0f);
    }

    void LateUpdate()
    {
        if (!IsShowing || view == null) return;
        angle += speed * Time.unscaledDeltaTime;
        Ring(out Vector3 position, out Quaternion rotation);

        float p = Mathf.Clamp01((Time.unscaledTime - started) / Mathf.Max(blendTime, 0.01f));
        p = p * p * (3f - 2f * p);
        view.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, p), Quaternion.Slerp(fromRotation, rotation, p));
        if (source != null) view.rect = source.rect;   // (the pause frame shrinks it)
    }

    //---- Where

    Vector3 Center => pivot != null ? pivot.position : Vector3.zero;

    // On the ring now: Distance away, Elevation degrees up, looking at the middle
    void Ring(out Vector3 position, out Quaternion rotation)
    {
        float distance = Distance();
        Vector3 back = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        position = Center + back * distance * Mathf.Cos(elevation * Mathf.Deg2Rad) + Vector3.up * distance * Mathf.Sin(elevation * Mathf.Deg2Rad);
        rotation = Quaternion.LookRotation(Center - position, Vector3.up);
    }

    // How far to fit a ball of Radius (times Margin) in the view
    float Distance()
    {
        Camera c = view != null ? view : source;
        float vertical = c.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float horizontal = Mathf.Atan(Mathf.Tan(vertical) * c.aspect);
        return radius * margin / Mathf.Sin(Mathf.Min(vertical, horizontal));
    }

    // Meters from the middle to the farthest thing that is up (islands and bridges showing)
    float UnlockedRadius()
    {
        float farthest = 0f;
        foreach (Lift lift in FindObjectsOfType<Lift>())
        {
            if (lift.IsHidden || !lift.gameObject.activeInHierarchy) continue;
            foreach (Renderer r in lift.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    farthest = Mathf.Max(farthest, Vector3.Distance(corner, Center));
                }
            }
        }
        return farthest;
    }
}
