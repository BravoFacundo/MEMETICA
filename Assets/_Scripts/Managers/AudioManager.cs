// Sonido general. Va en Managers > AudioManager.
// - Música del menú: suena al arrancar y se apaga con un fundido al entrar al juego (lo pide el UIManager).
// - Volúmenes: pasan por el AudioMixer (para la ventana de configuración, más adelante).

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    [Header("Mixer")]
    public AudioMixer mixer;
    [Tooltip("Exposed mixer parameters for the volume sliders (leave empty if not exposed yet)")]
    public string masterParam = "volume";
    public string musicParam = "MusicVolume";
    public string sfxParam = "SfxVolume";

    [Space(10)]
    [Header("Música")]
    [Tooltip("AudioSource con Output en el grupo Music del mixer")]
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
        if (!string.IsNullOrEmpty(masterParam)) SetVolume(masterParam, GameSettings.MasterVolume);
        if (!string.IsNullOrEmpty(musicParam)) SetVolume(musicParam, GameSettings.MusicVolume);
        if (!string.IsNullOrEmpty(sfxParam)) SetVolume(sfxParam, GameSettings.SfxVolume);
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

    // Para la configuración: valor de 0 a 1 → decibeles del mixer.
    // exposedParam es el nombre del parámetro expuesto en el mixer (por ejemplo "MusicVolume").
    public void SetVolume(string exposedParam, float value01)
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
