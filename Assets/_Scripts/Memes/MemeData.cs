using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// One meme: its images and sounds. An asset per meme (<Prefix>_Data in its folder), made and updated from its files
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
// Sounds: each Move its own (Move Sounds in the same order), Fall and Leave theirs; else this meme's, else MemeSettings' fallback.
[CreateAssetMenu(fileName = "MemeData", menuName = "Memetica/Meme Data")]
public class MemeData : ScriptableObject
{
    [Tooltip("Its name (the axis it belongs to): Religion, Conspiracy, Mode…")]
    public string title;

    [Header("Images")]
    [Tooltip("The meme as you find it: it always comes back to this")]
    public MemeImage baseImage = new MemeImage();
    [Tooltip("Move: the next one each time it is carried onto another island, in this order")]
    public List<MemeImage> variants = new List<MemeImage>();
    [Tooltip("While falling into the void (or found after its meteor). Empty: nothing happens")]
    public MemeImage fall = new MemeImage();
    [Tooltip("After you leave it, go to another island and come back. Empty: nothing happens")]
    public MemeImage leave = new MemeImage();

    [Header("Sounds")]
    [Tooltip("When you pick it up (its Base sound). It is also one of the meme's sounds")]
    public AudioClip grabSound;
    [Tooltip("One per Move variant, same order (empty slot: the meme's sounds)")]
    public MemeSound[] moveSounds;
    public MemeSound[] fallSounds;
    public MemeSound[] leaveSounds;
    [Tooltip("This meme's sounds: for any change with none of its own")]
    public MemeSound[] sounds;

    [Header("Painting")]
    [Tooltip("The clue videos of its paintings. Empty: it has no painting, so it goes on black (keyless) pedestals " +
             "(a white pedestal waiting for it warns)")]
    public List<VideoClip> hints = new List<VideoClip>();
    [Tooltip("Still images of its paintings (Reference_01…), same order as Hints")]
    public List<Texture2D> references = new List<Texture2D>();

    public bool HasPainting => hints.Exists(h => h != null);
    public bool HasFall => MemeImage.Exists(fall);
    public bool HasLeave => MemeImage.Exists(leave);
}

// One image of a meme: a picture, a video, or both (the picture until the video shows its first frame)
[System.Serializable]
public class MemeImage
{
    public Texture2D picture;
    public VideoClip video;

    public static bool Exists(MemeImage image) => image != null && (image.picture != null || image.video != null);
}

// A sound of a change. Loop: it keeps playing while that change shows, and stops when the meme goes back
[System.Serializable]
public class MemeSound
{
    public AudioClip clip;
    public bool loop;
}
