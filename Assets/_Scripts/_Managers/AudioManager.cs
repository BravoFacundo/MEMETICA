// General sound. Goes in Managers > AudioManager.
// - Menu music: plays on start and fades out when entering the game (UIManager asks for it).
// - Volumes: the settings window sliders drive the mixer's exposed parameters
//   MasterVolume, MusicVolume and SfxVolume (names must match exactly in MainMixer).
// - World sounds (Game): the stones (PathStone) and the islands and bridges rising (Lift) are heard at full volume
//   near you and fade to nothing farther away (Stone Range, Lift Range: the next course isn't heard), and they go
//   quieter while a meme sounds (Duck Volume). Without an AudioManager in the scene: the default values.

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer")]
    public AudioMixer mixer;

    const string MasterParam = "MasterVolume";
    const string MusicParam = "MusicVolume";
    const string SfxParam = "SfxVolume";

    [Space(10)]
    [Header("Music")]
    [Tooltip("AudioSource with its Output on the mixer's Music group")]
    public AudioSource musicSource;
    public AudioClip menuMusic;

    [Space(10)]
    [Header("World Sounds (Game)")]
    [Tooltip("Stones: meters at full volume (x) and from where they aren't heard (y)")]
    public Vector2 stoneRange = DefaultStoneRange;
    [Tooltip("Islands and bridges rising: meters at full volume (x) and from where they aren't heard (y)")]
    public Vector2 liftRange = DefaultLiftRange;
    [Tooltip("Their volume while a meme sounds (so the meme is heard)")]
    [Range(0f, 1f)] public float duckVolume = DefaultDuck;

    static readonly Vector2 DefaultStoneRange = new Vector2(4f, 30f), DefaultLiftRange = new Vector2(10f, 70f);
    const float DefaultDuck = 0.35f;

    void Awake() => Instance = this;

    void Start()
    {
        ApplySettings();
        GameSettings.Changed += ApplySettings;
        if (menuMusic != null) PlayMusic(menuMusic);
    }

    void OnDestroy() => GameSettings.Changed -= ApplySettings;

    // Saved volumes → mixer (on start and whenever the settings window changes them)
    void ApplySettings()
    {
        if (mixer == null) return;
        SetVolume(MasterParam, GameSettings.MasterVolume);
        SetVolume(MusicParam, GameSettings.MusicVolume);
        SetVolume(SfxParam, GameSettings.SfxVolume);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null) return;   // scene without music (e.g. Game for now)
        StopAllCoroutines();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = 1f;
        musicSource.Play();
    }

    public void FadeOutMusic(float duration)
    {
        if (musicSource == null) return;   // nothing to fade
        StopAllCoroutines();
        StartCoroutine(FadeOut(duration));
    }

    //---- World sounds

    public enum World { Stone, Lift }

    // A world sound's source: 3D, full volume near, nothing from its range's end
    public static void SetUp(AudioSource source, World kind)
    {
        if (source == null) return;
        Vector2 range = kind == World.Stone ? (Instance != null ? Instance.stoneRange : DefaultStoneRange)
                                            : (Instance != null ? Instance.liftRange : DefaultLiftRange);
        float far = Mathf.Max(range.y, range.x + 0.1f);
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.minDistance = Mathf.Max(range.x, 0.01f);
        source.maxDistance = far;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff(Mathf.Clamp01(range.x / far)));
    }

    // 1 up to Near (0-1 of the range), then fading like a real sound (1/distance), down to 0 at the end
    static AnimationCurve Rolloff(float near)
    {
        var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(Mathf.Max(near, 0.001f), 1f));
        const int steps = 8;
        for (int i = 1; i <= steps; i++)
        {
            float t = Mathf.Lerp(near, 1f, i / (float)steps);
            float fade = (t - near) / Mathf.Max(1f - near, 0.001f);
            curve.AddKey(t, i == steps ? 0f : Mathf.Max(near, 0.001f) / t * (1f - fade * fade));
        }
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    // A meme is sounding (MemeChanger, DexCollectible): for these seconds the world sounds go quieter
    private static float memeUntil;
    public static void MemeSounding(float seconds = 0.2f) => memeUntil = Mathf.Max(memeUntil, Time.unscaledTime + seconds);

    // The volume a world sound starts at now
    public static float WorldVolume => Time.unscaledTime < memeUntil ? (Instance != null ? Instance.duckVolume : DefaultDuck) : 1f;

    // 0..1 → mixer decibels
    void SetVolume(string exposedParam, float value01)
    {
        mixer.SetFloat(exposedParam, Mathf.Log10(Mathf.Clamp(value01, 0.0001f, 1f)) * 20f);
    }

    IEnumerator FadeOut(float duration)
    {
        float startVolume = musicSource.volume;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }
        musicSource.volume = 0f;
        musicSource.Stop();
    }
}
