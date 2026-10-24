using System.Collections;
using UnityEngine;

// A platform that turns to point at where you go next (the hub's, with the randomizer pedestal on it): after the
// raffle it turns its front (the thick blue arrow it draws in the Scene view) toward the winner's first island (the
// middle of its meshes, not its pivot), and only then that
// way rises in front of you. It only turns around the vertical, the shorter way round (90° left, never 270° right): an
// island below (rising, or waiting hidden) doesn't make it look up or down.
// Whatever is inside it turns with it (the pedestal and its meme); the player standing on it turns too (PlayerMove).
// Its front is part of it: rotate the platform in the Scene view (Y) and the arrow turns with it, so you can leave it
// pointing anywhere (e.g. at the tutorial). Set it once from its menu (right click on the component): Front = Where
// The Scene View Looks, or Front = World Z.
public class AimPlatform : MonoBehaviour
{
    [Tooltip("Its front, in its own space (set it with the component's right click menu)")]
    [SerializeField, HideInInspector] Vector3 frontLocal;
    [Tooltip("Degrees per second at full speed")]
    public float turnSpeed = 120f;
    [Tooltip("Seconds it takes at least (a small turn is not a jerk)")]
    public float minTurnTime = 0.5f;
    [Tooltip("Pointing there already (less than these degrees away): it goes a whole turn round, so you see it choose")]
    [Range(0f, 90f)] public float fullTurnBelow = 20f;

    [Section("Feel", Closed = true)]
    [Tooltip("While it turns (looped)")]
    public AudioClip turnClip;
    [Tooltip("When it stops")]
    public AudioClip stopClip;
    [Tooltip("Camera shake while it turns. 0 = off")]
    public float shake = 0.02f;

    public bool IsTurning { get; private set; }

    private AudioSource audioSource;

    // Not set yet (just added, or from before): the world's Z as it is now
    void OnValidate() { if (frontLocal == Vector3.zero) frontLocal = transform.InverseTransformDirection(Vector3.forward); }
    void Awake() => OnValidate();

#if UNITY_EDITOR
    [ContextMenu("Front = Where The Scene View Looks")]
    void FrontFromSceneView()
    {
        UnityEditor.SceneView view = UnityEditor.SceneView.lastActiveSceneView;
        if (view == null) return;
        SetFront(Flat(view.camera.transform.forward));
    }

    [ContextMenu("Front = World Z (the Global blue arrow)")]
    void FrontWorldZ() => SetFront(Vector3.forward);

    void SetFront(Vector3 world)
    {
        UnityEditor.Undo.RecordObject(this, "Platform Front");
        frontLocal = transform.InverseTransformDirection(world);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    // Its front now, flat in the world (turns with it). Everything is around the world's vertical, whatever its model's axes
    public Vector3 Front => Flat(transform.TransformDirection(frontLocal));

    // Where to aim at something: the middle of its meshes (an island's pivot may be anywhere), shown or hidden
    public static Vector3 CenterOf(Transform target)
    {
        bool any = false;
        Bounds bounds = new Bounds(target.position, Vector3.zero);
        foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds local = filter.sharedMesh.bounds;
            Vector3 center = filter.transform.TransformPoint(local.center);
            if (!any) { bounds = new Bounds(center, Vector3.zero); any = true; } else bounds.Encapsulate(center);
        }
        return bounds.center;
    }

    // Turns until its front points at that place (seen from above it: only around its vertical), in Seconds (0: at its
    // own Turn Speed). Wait for it if needed
    public Coroutine AimAt(Vector3 point, float seconds = 0f)
    {
        StopAllCoroutines();
        return StartCoroutine(Turning(point, seconds));
    }

    IEnumerator Turning(Vector3 point, float seconds)
    {
        Vector3 toward = point - transform.position;
        toward.y = 0f;
        if (toward.sqrMagnitude < 0.01f) yield break;
        float angle = Vector3.SignedAngle(Front, toward.normalized, Vector3.up);   // -180..180: the shorter way round
        if (Mathf.Abs(angle) < fullTurnBelow) angle += angle >= 0f ? 360f : -360f;   // one whole turn

        IsTurning = true;
        Sound(turnClip, true);
        float duration = seconds > 0f ? seconds : Mathf.Max(minTurnTime, Mathf.Abs(angle) / Mathf.Max(turnSpeed, 1f) * 1.5f);   // (eased: 1.5 x the time at full speed)
        if (shake > 0f && Camera.main != null && Camera.main.TryGetComponent(out PlayerLook look))
            look.StartCoroutine(look.ShakeTheCamera(duration, shake));

        // Around its own vertical, on its parent (it still floats with its island)
        Quaternion from = transform.localRotation;
        Vector3 axis = transform.parent != null ? transform.parent.InverseTransformDirection(Vector3.up) : Vector3.up;   // the world's vertical, on its parent
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = t / duration;
            float eased = p * p * p * (p * (6f * p - 15f) + 10f);   // speeds up, then slows down to a stop: same time for 90° or 270°
            transform.localRotation = Quaternion.AngleAxis(angle * eased, axis) * from;
            yield return null;
        }
        transform.localRotation = Quaternion.AngleAxis(angle, axis) * from;

        Sound(null, false);
        Sound(stopClip, false);
        IsTurning = false;
    }

    void Sound(AudioClip clip, bool loop)
    {
        if (audioSource == null)
        {
            if (clip == null) return;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 8f;
        }
        if (clip == null) { if (audioSource.loop) { audioSource.Stop(); audioSource.loop = false; } return; }
        if (loop) { audioSource.loop = true; audioSource.clip = clip; audioSource.Play(); }
        else { audioSource.loop = false; audioSource.PlayOneShot(clip); }
    }

#if UNITY_EDITOR
    // Its front: a blue arrow from its middle
    void OnDrawGizmos()
    {
        Vector3 front = Front;
        Vector3 side = Vector3.Cross(Vector3.up, front);
        Vector3 start = transform.position + Vector3.up * 0.1f, tip = start + front * 4f;
        Gizmos.color = new Color(0.3f, 0.6f, 1f);
        for (int k = -2; k <= 2; k++)   // thick
        {
            Vector3 o = side * k * 0.03f;
            Gizmos.DrawLine(start + o, tip + o);
            Gizmos.DrawLine(tip + o, tip - front * 0.8f + side * 0.5f + o);
            Gizmos.DrawLine(tip + o, tip - front * 0.8f - side * 0.5f + o);
        }
        UnityEditor.Handles.Label(tip + Vector3.up * 0.4f, "Front");
    }
#endif
}
