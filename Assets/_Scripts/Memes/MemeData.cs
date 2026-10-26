using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// One meme: its images and sounds. An asset per meme (MemeData_<Folder> in its folder; older ones <Prefix>_Data), made and updated from its files
// by Memetica > Memes > Import (Reli_Base.png, Reli_Move_01.png…, Reli_Fall.png, Reli_Leave.png, sounds with the same
// names; see MemeImportTool). The orb in the scene (Meme_<ID>) holds one; the ID stays the orb's, so any meme can go
// in any orb.
//   Move   every time it is carried onto an island other than the last one it changes: variant 1, then 2… (the tutorial
//          doesn't count; once all were shown, any of them); moving around, it turns into the ones already shown too.
//          The last ones are seen by who goes around the most: good for the references closest to the painting
//   Fall   while you fall into the void with it in your hand; alone, when you find it after its meteor (empty: nothing)
//   Leave  after you leave it on the ground, go to another island and come back (empty: nothing happens)
// Whenever you find it again after losing sight of it, it is its base image: a change only happens once you see it.
// Each image can be a picture, a video, or both (the picture shows until the video's first frame, and in the editor).
// Pictures: 1024 x 1024 PNG, the meme as big as it fits; transparent around it and the sticker border follows its shape
// (a video has no transparency: its sticker is a rectangle).
// Sounds: each image has its own, next to it (Base's plays when you pick it up). An image without one: one of the
// Fallback Sounds at random, else Fallback Sound, else MemeSettings' fallback. Picked up with no Base sound: Fallback Sound.
[CreateAssetMenu(fileName = "MemeData", menuName = "Memetica/Meme Data")]
public class MemeData : ScriptableObject
{
    public enum VideoStart { Playing, OnPickUp }

    [Header("Meme")]
    [Tooltip("The axis it belongs to: Religion, Conspiracy, Mode… (each image's Window Title is what its window shows)")]
    public string title;

    [Header("Base")]
    [Tooltip("The meme as you find it: it always comes back to this. Its sound plays when you pick it up")]
    public MemeImage baseImage = new MemeImage();
    [Tooltip("Its videos: Playing = they play from the start. On Pick Up = still on the first frame until you pick it up")]
    public VideoStart videoStart = VideoStart.Playing;
    [Tooltip("Every image fills the whole window (as each image's Fill)")]
    public bool fillAll = false;

    // Shown filling the window: its own Fill, or Fill All
    public bool Fills(MemeImage image) => fillAll || (image != null && image.fill);

    [Header("Variants")]
    [Tooltip("While falling into the void (or found after its meteor). Empty: nothing happens")]
    public MemeImage fall = new MemeImage();
    [Tooltip("After you leave it, go to another island and come back (or it comes back to you). Empty: nothing happens")]
    public MemeImage leave = new MemeImage();
    [Tooltip("Move: the next one each time it is carried onto another island, in this order")]
    public List<MemeImage> variants = new List<MemeImage>();

    [Header("Fallback")]
    [Tooltip("Picked up with no Base sound, and the last resort for an image without one")]
    [UnityEngine.Serialization.FormerlySerializedAs("grabSound")] public AudioClip fallbackSound;
    [Tooltip("For any image without its own sound: one of these at random")]
    public MemeSound[] fallbackSounds;

    // Before each image had its own sound (older MemeData): brought into the images and Fallback Sounds once
    [SerializeField, HideInInspector] MemeSound[] moveSounds, fallSounds, leaveSounds, sounds;

    [Header("Painting")]
    [Tooltip("The clue videos of its paintings. Empty: it has no painting, so it goes on black (keyless) pedestals " +
             "(a white pedestal waiting for it warns)")]
    public List<VideoClip> hints = new List<VideoClip>();
    [Tooltip("Still images of its paintings (Reference_01…), same order as Hints")]
    public List<Texture2D> references = new List<Texture2D>();

    public bool HasPainting => hints.Exists(h => h != null);
    public bool HasFall => MemeImage.Exists(fall);
    public bool HasLeave => MemeImage.Exists(leave);
    // What plays when you pick it up: Base's own sound, else Fallback Sound
    public AudioClip BaseSound => baseImage != null && baseImage.sound != null ? baseImage.sound : fallbackSound;

    // The older sound lists into the images (Move N → its variant, the first Fall / Leave → theirs) and the rest into
    // Fallback Sounds. Once, when it loads (in the editor it is saved like that)
    void OnEnable()
    {
        bool old = Has(moveSounds) || Has(fallSounds) || Has(leaveSounds) || Has(sounds);
        if (!old) return;
        var rest = new List<MemeSound>();
        if (fallbackSounds != null) rest.AddRange(fallbackSounds);
        for (int i = 0; moveSounds != null && i < moveSounds.Length; i++)
            if (i < variants.Count && Valid(moveSounds[i]) && variants[i].sound == null) Give(variants[i], moveSounds[i]);
        Spread(fallSounds, fall, rest);
        Spread(leaveSounds, leave, rest);
        if (sounds != null) rest.AddRange(System.Array.FindAll(sounds, Valid));
        fallbackSounds = rest.ToArray();
        moveSounds = fallSounds = leaveSounds = sounds = null;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    static bool Has(MemeSound[] list) => list != null && System.Array.Exists(list, Valid);
    static bool Valid(MemeSound s) => s != null && s.clip != null;
    static void Give(MemeImage image, MemeSound s) { image.sound = s.clip; image.loop = s.loop; }

    // The first one to the image (if it has none), the others to the fallback
    static void Spread(MemeSound[] list, MemeImage image, List<MemeSound> rest)
    {
        if (list == null) return;
        foreach (MemeSound s in list)
        {
            if (!Valid(s)) continue;
            if (image != null && image.sound == null && MemeImage.Exists(image)) Give(image, s);
            else rest.Add(s);
        }
    }
}

// One image of a meme: a picture, a video, or both (the picture until the video shows its first frame)
[System.Serializable]
public class MemeImage
{
    [Tooltip("What its window's title shows while this image is on it (Trollface.png, RageGuy.jpg…). Empty: the meme's axis")]
    public string windowTitle;
    public Texture2D picture;
    public VideoClip video;
    [Tooltip("Its sound (empty: the meme's lists, then the fallback)")]
    public AudioClip sound;
    [Tooltip("The sound loops while this image shows")]
    public bool loop;
    [Tooltip("Fills the whole window, cutting what is left over (centered): a square meme in the wider window, without borders")]
    public bool fill;

    public static bool Exists(MemeImage image) => image != null && (image.picture != null || image.video != null);
}

// A sound of a change. Loop: it keeps playing while that change shows, and stops when the meme goes back
[System.Serializable]
public class MemeSound
{
    public AudioClip clip;
    public bool loop;
}
