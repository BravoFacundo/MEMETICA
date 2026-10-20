using UnityEngine;

// What every meme shares: resting, the meteor respawn and the MemeOrb mutation. One asset for the whole game,
// Assets/Resources/MemeSettings (the memes load it on their own, nothing to assign).
// Before: the same values were repeated on each meme of each scene.
[CreateAssetMenu(fileName = "MemeSettings", menuName = "Memetica/Meme Settings")]
public class MemeSettings : ScriptableObject
{
    [Header("Rest")]
    [Tooltip("Seconds on the ground before it freezes in place, so it doesn't roll away")]
    public float restDelay = 3f;
    public float groundCheckRadius = 0.6f;

    [Header("Respawn (meteor)")]
    [Tooltip("Meters above the island it starts falling from")]
    public float meteorHeight = 25f;
    [Tooltip("Meters it travels sideways while falling (the angle of the fall)")]
    public float meteorDrift = 10f;
    [Tooltip("Seconds the fall takes (it speeds up)")]
    public float meteorDuration = 1.1f;
    [Tooltip("Meters around the island's middle (its RespawnPoint) where it can land")]
    public float landingRadius = 4f;
    [Tooltip("Never lands closer than this to a pedestal (it would place itself)")]
    public float pedestalClearance = 3f;
    public Color meteorColor = new Color(1f, 0.75f, 0.35f);
    [Tooltip("Pieces that fly out where it lands (small rock and grass prefabs, like the bridges)")]
    public GameObject[] impactDebris;
    public int impactDebrisCount = 10;
    public AudioClip impactClip;

    [Header("Mutation (MemeOrb): it changes while you carry it and move")]
    [Tooltip("Warning: it is about to change")]
    public AudioClip warnClip;
    public AudioClip changeClip;
    [Tooltip("Back to the original")]
    public AudioClip returnClip;
    [Tooltip("Each image of the sweep on the pedestal")]
    public AudioClip sweepTickClip;
    [Tooltip("Seconds moving until the first change")]
    public float firstChangeAfter = 3f;
    [Tooltip("Seconds moving between changes (random between x and y)")]
    public Vector2 changeInterval = new Vector2(4f, 8f);
    [Tooltip("How long a variant lasts before going back to the original (random between x and y)")]
    public Vector2 variantDuration = new Vector2(1.5f, 2.5f);
    [Tooltip("Seconds before the change that the warning plays")]
    public float warnLead = 0.6f;
    [Tooltip("No change this close to jumps, falls, new bridges or new islands (the player would think they caused it)")]
    public float graceAfterEvent = 1f;
    public float flashIntensity = 2f;
    [Tooltip("Part of the pedestal charge the sweep takes")]
    [Range(0.3f, 1f)] public float sweepPortion = 0.75f;

    private static MemeSettings instance;

    public static MemeSettings Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = Resources.Load<MemeSettings>(nameof(MemeSettings));
            if (instance == null)
            {
                Debug.LogError("[Meme] No MemeSettings in a Resources folder: using the default values.");
                instance = CreateInstance<MemeSettings>();
            }
            return instance;
        }
    }
}
