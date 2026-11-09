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
    [Tooltip("How cracked it always shows, before you step on it (0: clean until then). Then the cracks open all the way")]
    [Range(0f, 1f)] public float fallIdleCrack = 0.35f;
    [Tooltip("It bursts: the stone hides and this many smaller copies of it fly out, flat side up, to the sides and down")]
    public Vector2Int shardCount = new Vector2Int(4, 5);
    [Tooltip("Size of each shard, times the stone's")]
    [Range(0.1f, 0.8f)] public float shardSize = 0.35f;
    [Tooltip("Meters per second they shoot out (sideways; down: half of it)")]
    public float shardSpeed = 4f;
    [Tooltip("Seconds they fly before they are gone (shrinking at the end)")]
    public float shardTime = 1f;
    [Tooltip("Seconds the shards take to come back together into the stone")]
    public float assembleTime = 0.6f;
    [Tooltip("Part of that (0-1) when the stone shows again, the shards gone: it is them closing up (before they touch)")]
    [Range(0.5f, 1f)] public float assembleShowAt = 0.8f;

    [Header("Slide")]
    [Tooltip("A trail behind it while it moves, so you can read where it goes (its width and height: StonePath > Trail)")]
    public bool trails = true;
    [Tooltip("Seconds the trail lasts behind the stone")]
    public float trailSeconds = 1.8f;
    public Color trailColor = new Color(0.3f, 0.3f, 0.32f, 0.6f);
    [Tooltip("Meters the trail sways to each side (a winding line; small: you still read the way). 0: straight")]
    public float trailWobble = 0.2f;
    [Tooltip("Meters of the way for one sway to each side and back")]
    public float trailWaveLength = 3f;
    [Tooltip("Pieces that come off its trail while it moves (small rock prefabs). Empty: none")]
    public GameObject[] slideDebris;
    [Tooltip("Pieces per second while it moves")]
    public float slideDebrisRate = 2f;
    [Tooltip("Meters per second it must be going to drop pieces (slowing into a point, or setting off: none)")]
    public float slideDebrisMinSpeed = 1.5f;
    [Tooltip("Size of each piece when it comes off, times the stone's width (it shrinks from there: from the platform down to nothing)")]
    [Range(0.05f, 0.6f)] public float slideDebrisSize = 0.25f;
    [Tooltip("Seconds a piece floats where it came off the trail (bobbing, shrinking slowly) before it drops")]
    public float slideDebrisHang = 1f;
    [Tooltip("Meters it drops, shrinking fast to nothing (it goes, it doesn't fall into the void)")]
    public float slideDebrisDrop = 1.2f;

    [Header("Blink")]
    [Tooltip("Where it is going to show up, a tiny copy of it shakes there from its warning on, and at the end grows at " +
             "once into the stone itself")]
    public bool blinkPreview = true;
    [Tooltip("Size of the tiny copy, times the stone's")]
    [Range(0.05f, 0.6f)] public float previewSmall = 0.15f;
    [Tooltip("Meters it shakes while small")]
    public float previewShake = 0.05f;
    [Tooltip("Seconds of the end in which it grows to full size (short: at once)")]
    public float previewGrowTime = 0.15f;
    [Tooltip("Pebbles per second it throws to the sides while small and shaking (as if grinding). 0: none. They use " +
             "Slide Debris' rocks")]
    public float previewPebbleRate = 8f;
    [Tooltip("Size of each pebble, times the stone's width")]
    [Range(0.02f, 0.3f)] public float previewPebbleSize = 0.06f;

    [Header("Effects")]
    [Tooltip("Meters from the camera: farther, no pieces drop or pop (you wouldn't see them; they cost)")]
    public float effectsRange = 45f;

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
