// General sound. Goes in Managers > AudioManager.
// - Menu music: plays on start and fades out when entering the game (UIManager asks for it).
// - Volumes: the settings window sliders drive the mixer's exposed parameters
//   MasterVolume, MusicVolume and SfxVolume (names must match exactly in MainMixer).

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
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
