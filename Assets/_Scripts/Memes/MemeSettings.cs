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

    [Header("Meteor: the fall (a meme that fell into the void comes back as a meteor)")]
    [Tooltip("Meters it travels sideways while falling (the angle of the fall)")]
    public float meteorDrift = 10f;
    [Tooltip("Seconds the fall takes (it speeds up)")]
    public float meteorDuration = 1.1f;
    [Tooltip("Meters around the island's middle (its RespawnPoint) where it can land")]
    public float landingRadius = 4f;
    [Tooltip("Never lands closer than this to a pedestal (it would place itself)")]
    public float pedestalClearance = 3f;
    public Color meteorColor = new Color(1f, 0.75f, 0.35f);

    [Header("Meteor: the rock (the meme falls inside it, it bursts on impact)")]
    [Tooltip("Rock the meme falls as (any rock model): it bursts on impact in a dark cloud and the meme comes out. Empty: the meme falls as itself")]
    public GameObject meteorRock;
    [Tooltip("Meters wide the rock is (scaled to it, whatever the model)")]
    public float meteorRockSize = 0.9f;
    [Tooltip("Chunks of the rock thrown up when it bursts")]
    public int meteorChunks = 6;

    [Header("Meteor: impact")]
    [Tooltip("Pieces that fly out where it lands (small rock and grass prefabs, like the bridges)")]
    public GameObject[] impactDebris;
    public int impactDebrisCount = 10;
    public AudioClip impactClip;
    [Tooltip("The cloud the rock bursts in (darker: it hides the meme coming out)")]
    public Color impactSmokeColor = new Color(0.3f, 0.3f, 0.33f, 1f);

    [Header("Meteor: smoke (rises from the meme until it is picked up)")]
    [Tooltip("Smoke of the meteor (impact cloud and trail): Memetica/LowPolySmoke, solid low poly puffs")]
    public Material smokeMaterial;
    [Tooltip("Seconds between puffs of the smoke trail (it comes out broken)")]
    public float smokeGap = 0.45f;
    [Tooltip("The trail rising from the meme")]
    public Color smokeColor = new Color(0.52f, 0.53f, 0.57f, 1f);
    [Tooltip("Meters tall the pillar gets (seen from anywhere, zooming if needed)")]
    public float smokeHeight = 40f;
    [Tooltip("Meters per second the smoke rises")]
    public float smokeRiseSpeed = 2.5f;
    [Tooltip("Meters wide each puff starts (it grows as it rises)")]
    public float smokeSize = 2.5f;

    [Header("Meme sounds")]
    [Tooltip("Last resort for any meme change with no sound of its own nor of its meme: meme sounds that any meme can use")]
    public AudioClip[] fallbackSounds;

    [Header("Mist (MemeOrb): every change happens inside it")]
    [Tooltip("Seconds the fog takes to gather and hide the image (it is the warning that a change is coming)")]
    public float mistIn = 1f;
    [Tooltip("Seconds it takes to clear after the image changed")]
    public float mistOut = 0.6f;
    [Tooltip("Fog inside the orb: Memetica/ToonPuff, flat cartoon puffs (the orb is a flat drawing)")]
    public Material fogMaterial;
    [Tooltip("Fog wisps per second inside the bubble at its thickest")]
    public float fogRate = 55f;
    public Color fogColor = new Color(0.5f, 0.52f, 0.56f, 0.9f);
    [Tooltip("After the swap, how much faster the wisps age (they fade out instead of lingering)")]
    public float fogFadeSpeed = 3f;

    [Header("Sticker (MemeOrb)")]
    [Tooltip("Size of the meme inside the bubble (the Sticker's scale; every orb gets it, in the editor too). " +
             "Small enough that square images stay inside the sphere")]
    public float stickerSize = 0.08f;

    [Header("Unlocking (MemeOrb: one more Move variant per new island it is carried to)")]
    [Tooltip("The letter islands (Island_A, B, C: between bridges) unlock a variant too. The tutorial never does")]
    public bool letterIslandsUnlock = true;

    [Header("Falls")]
    [Tooltip("Seconds the meme in your hand takes to turn into its Fall change once you fall into the void (almost at once: the joke is the surprise)")]
    public float fallSwapTime = 0.05f;

    [Header("Changes you must see (fall alone (meteor), leave)")]
    [Tooltip("Meters: it changes in front of you when you come this close and it is on screen (close: it happens right in your face)")]
    public float revealDistance = 1.25f;
    [Tooltip("Seconds the change takes (the new image flickering in)")]
    public float revealTime = 0.35f;

    [Header("Mutation (MemeOrb): it changes while you carry it and move")]
    [Tooltip("Warning: it is about to change")]
    public AudioClip warnClip;
    public AudioClip changeClip;
    [Tooltip("Back to the base image")]
    public AudioClip returnClip;
    [Tooltip("Each image of the sweep on the pedestal")]
    public AudioClip sweepTickClip;
    [Tooltip("Seconds moving until the first change")]
    public float firstChangeAfter = 3f;
    [Tooltip("Seconds moving between changes (random between x and y)")]
    public Vector2 changeInterval = new Vector2(4f, 8f);
    [Tooltip("How long a change shows before going back to the base image (random between x and y). Fall and Leave with a sound: as long as their sound")]
    public Vector2 variantDuration = new Vector2(5f, 6f);
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
