using UnityEngine;

// How each kind of stone (PathStone > Motion) looks and sounds, for every stone path of every level, in one place:
// Assets/Resources/StoneTypes (the stones load it on their own, nothing to assign). Before: tints, trails and cracks
// were repeated on each StonePath, and the sounds on each stone.
// Each kind has a tint, a Mark (a second texture laid over the stone, tiled in the world: every model and size shows
// the same pattern, so you learn it once) and three sounds:
//   Warn  just before it acts   (Fall: stepped on, it shakes | Blink: starts shaking | Slide: unused)
//   Go    when it acts          (Fall: drops | Blink: vanishes | Slide: leaves a point)
//   Back  when it is back       (Fall: rises back into place | Blink: shows up | Slide: arrives at a point)
// The Mark needs the stone models' material on Memetica/Stone (it has the Mark slots); any other one only gets the tint.
// Changing a value here: the stones in the open scene update (playing, the next time they change).
[CreateAssetMenu(fileName = "StoneTypes", menuName = "Memetica/Stone Types")]
public class StoneTypes : ScriptableObject
{
    [System.Serializable]
    public class Kind
    {
        [Tooltip("Multiplies the stone's own color")]
        public Color tint = Color.white;
        [Tooltip("A second texture over the stone, gray or white with transparency (where it is opaque it shows). Empty: none")]
        public Texture2D mark;
        [Tooltip("Color of the mark (alpha: how strong)")]
        public Color markColor = new Color(0f, 0f, 0f, 0.5f);
        [Tooltip("Meters one tile of the mark covers")]
        public float markSize = 1f;
        [Tooltip("How carved into the stone the mark looks (0: flat paint)")]
        [Range(0f, 1f)] public float relief = 0.5f;
        public AudioClip warnClip;
        public AudioClip goClip;
        public AudioClip backClip;
    }

    [Header("Kinds")]
    [Tooltip("None: safe, it only rises with the path")]
    public Kind safe = new Kind { tint = new Color(0.85f, 0.85f, 0.85f) };
    [Tooltip("Drops when stepped on, then rises back")]
    public Kind fall = new Kind { tint = new Color(0.55f, 0.55f, 0.58f) };
    [Tooltip("Glides between its points")]
    public Kind slide = new Kind { tint = new Color(0.55f, 0.55f, 0.58f) };
    [Tooltip("Vanishes and shows up at its next point")]
    public Kind blink = new Kind { tint = new Color(0.55f, 0.55f, 0.58f) };

    [Header("Fall")]
    [Tooltip("Cracks that open on top while it warns (a PNG with transparency, e.g. Art/Textures/Decal_CrackedStone). Empty: none")]
    public Texture2D crackDecal;
    [Tooltip("Pieces that break off as it drops (small rock prefabs, e.g. _Prefabs/Effects/RockDebriesSmall). Empty: none")]
    public GameObject[] fallDebris;
    public int fallDebrisCount = 5;

    [Header("Slide")]
    [Tooltip("A trail behind it while it moves, so you can read where it goes (its width and height: StonePath > Trail)")]
    public bool trails = true;
    [Tooltip("Seconds the trail lasts behind the stone")]
    public float trailSeconds = 1f;
    public Color trailColor = new Color(0.3f, 0.3f, 0.32f, 0.6f);

    public Kind Of(PathStone.Motion motion)
    {
        switch (motion)
        {
            case PathStone.Motion.Fall: return fall;
            case PathStone.Motion.Slide: return slide;
            case PathStone.Motion.Blink: return blink;
            default: return safe;
        }
    }

    private static StoneTypes instance;
    public static StoneTypes Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = Resources.Load<StoneTypes>(nameof(StoneTypes));
            if (instance != null) return instance;
            if (fallback == null)   // (not kept as the one: it is looked for again next time, e.g. still importing)
            {
                Debug.LogError("[Stone] No StoneTypes in a Resources folder: using the default values.");
                fallback = CreateInstance<StoneTypes>();
            }
            return fallback;
        }
    }
    private static StoneTypes fallback;

#if UNITY_EDITOR
    // Changed here: every stone in the open scenes shows it
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            foreach (PathStone stone in FindObjectsOfType<PathStone>(true)) if (stone != null) stone.ApplyColor();
        };
    }
#endif
}
