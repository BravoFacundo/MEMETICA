// General sound. Goes in Managers > AudioManager.
// - Music: each scene's own, looped from the start (Menu: its theme; Game / Game_Old: the same one in the background,
//   at Music Level). It fades out when changing scene (UIManager asks for it). The settings' Music slider turns it
//   down or off (mixer's Music group). It makes its own source.
// - Ambience: the scene's background loop (Menu: the wind), 2D, on the Sfx group (the Sfx slider). It fades out with
//   the music. It makes its own source.
// - Volumes: the settings window sliders. Master turns everything (the AudioListener: also sounds that don't go through
//   the mixer); Music and Sfx drive the mixer's exposed MusicVolume and SfxVolume. Every AudioSource without a mixer
//   group (most are made by the scripts: memes, stones, the player…) is sent to Sfx Group on its own, every half
//   second; Music Source to Music Group. The groups are found in the mixer by name (Sfx / SFX, Music) if left empty.
// - World sounds (Game): the stones (PathStone) and the islands and bridges rising (Lift) are heard at full volume
//   near you and fade to nothing farther away (Stone Range, Lift Range: the next course isn't heard), and they go
//   quieter while a meme sounds (Duck Volume). Without an AudioManager in the scene: the default values.

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Section("Mixer")]
    public AudioMixer mixer;
    [Tooltip("Empty: found in the mixer by name (Music)")]
    public AudioMixerGroup musicGroup;
    [Tooltip("Empty: found in the mixer by name (Sfx / SFX / Effects)")]
    public AudioMixerGroup sfxGroup;

    const string MasterParam = "MasterVolume";
    const string MusicParam = "MusicVolume";
    const string SfxParam = "SfxVolume";

    [Section("Music")]
    [Tooltip("This scene's music, looped from the start (Menu and Game: MainTheme). Empty: no music here")]
    [FormerlySerializedAs("menuMusic")] public AudioClip music;
    [Tooltip("Its level in this scene before the slider (Game: lower, so it stays in the background)")]
    [Range(0f, 1f)] public float musicLevel = 1f;
    [Tooltip("The music's volume while a meme sounds (so the meme is heard), then back up")]
    [Range(0f, 1f)] public float musicDuck = 0.4f;

    [Section("Ambience")]
    [Tooltip("This scene's background loop (Menu: the wind), heard everywhere (2D). Empty: none")]
    public AudioClip ambience;
    [Tooltip("Its level before the slider (the Sfx slider turns it)")]
    [Range(0f, 1f)] public float ambienceLevel = 0.6f;

    [Section("World Sounds (Game)", Closed = true)]
    [Tooltip("Stones: meters at full volume (x) and from where they aren't heard (y)")]
    public Vector2 stoneRange = DefaultStoneRange;
    [Tooltip("Islands and bridges rising: meters at full volume (x) and from where they aren't heard (y)")]
    public Vector2 liftRange = DefaultLiftRange;
    [Tooltip("Their volume while a meme sounds (so the meme is heard)")]
    [Range(0f, 1f)] public float duckVolume = DefaultDuck;

    // Made here when there is a clip for them (2D, heard everywhere, each on its mixer group)
    private AudioSource musicSource, ambienceSource;

    static readonly Vector2 DefaultStoneRange = new Vector2(4f, 30f), DefaultLiftRange = new Vector2(10f, 70f);
    const float DefaultDuck = 0.35f;

    void Awake() => Instance = this;

    void Start()
    {
        FindGroups();
        if (music != null) musicSource = MakeSource(musicGroup, "Music");
        if (ambience != null) ambienceSource = MakeSource(sfxGroup, "Sfx");
        RouteSources();
        ApplySettings();
        SettingsManager.Changed += ApplySettings;
        if (music != null) PlayMusic(music);
        if (ambience != null) PlayAmbience(ambience);
    }

    void OnDestroy() => SettingsManager.Changed -= ApplySettings;

    // Saved volumes → mixer (on start and whenever the settings window changes them)
    void ApplySettings()
    {
        AudioListener.volume = SettingsManager.MasterVolume;   // everything, routed or not
        if (mixer == null) return;
        SetVolume(MasterParam, 1f);                         // (Master is the listener's: not twice)
        SetVolume(MusicParam, SettingsManager.MusicVolume);
        SetVolume(SfxParam, SettingsManager.SfxVolume);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null) return;   // scene without music (e.g. Game for now)
        StopAllCoroutines();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = musicLevel;
        musicSource.Play();
        fading = false;
    }

    public void PlayAmbience(AudioClip clip)
    {
        if (ambienceSource == null) return;
        ambienceSource.clip = clip;
        ambienceSource.loop = true;
        ambienceSource.volume = ambienceLevel;
        ambienceSource.Play();
    }

    // Changing scene (UIManager): the music and the ambience fade out together
    public void FadeOutMusic(float duration)
    {
        if (musicSource == null && ambienceSource == null) return;   // nothing to fade
        StopAllCoroutines();
        fading = true;
        if (musicSource != null) StartCoroutine(FadeOut(musicSource, duration));
        if (ambienceSource != null) StartCoroutine(FadeOut(ambienceSource, duration));
    }

    //---- Routing: every sound through the mixer, so the Music and Sfx sliders reach it

    private float nextRoute;
    private bool fading;
    void Update()
    {
        // The music steps back while a meme sounds
        if (musicSource != null && musicSource.isPlaying && !fading)
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, musicLevel * (Time.unscaledTime < memeUntil ? musicDuck : 1f), 2f * Time.unscaledDeltaTime);

        if (Time.unscaledTime < nextRoute) return;
        nextRoute = Time.unscaledTime + 0.5f;
        RouteSources();
    }

    // Its own source, flat (2D, heard everywhere), on that group so its slider reaches it
    AudioSource MakeSource(AudioMixerGroup group, string groupName)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;   // (it goes on in the pause, like in the Menu)
        source.outputAudioMixerGroup = group;
        if (group == null) Debug.LogWarning($"[AudioManager] No {groupName} group in the mixer: the {groupName} slider won't reach it.", this);
        return source;
    }

    void FindGroups()
    {
        if (mixer == null) return;
        if (sfxGroup == null) sfxGroup = First("Sfx") ?? First("SFX") ?? First("Effects");
        if (musicGroup == null) musicGroup = First("Music");
        if (sfxGroup == null) Debug.LogWarning("[AudioManager] No Sfx group in the mixer: the Sfx slider won't reach the game's sounds.", this);
    }

    AudioMixerGroup First(string groupName)
    {
        AudioMixerGroup[] found = mixer.FindMatchingGroups(groupName);
        return found != null && found.Length > 0 ? found[0] : null;
    }

    void RouteSources()
    {
        if (musicSource != null && musicGroup != null && musicSource.outputAudioMixerGroup == null) musicSource.outputAudioMixerGroup = musicGroup;
        if (sfxGroup == null) return;
        foreach (AudioSource source in FindObjectsOfType<AudioSource>(true))
            if (source.outputAudioMixerGroup == null && source != musicSource) source.outputAudioMixerGroup = sfxGroup;   // (the ambience too)
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

    IEnumerator FadeOut(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }
        source.volume = 0f;
        source.Stop();
    }
}
