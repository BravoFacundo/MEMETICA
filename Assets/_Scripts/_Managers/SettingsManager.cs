using System;
using UnityEngine;

public enum GameLanguage { English, Spanish }

// The game's settings (language, volumes, mouse), saved with PlayerPrefs and shared by every scene. Goes in the Menu's
// Managers > SettingsManager: its Inspector holds the values a first launch starts with (and the language every launch
// starts in). The other scenes don't need one: they use what was saved (or the defaults below, if you press Play there
// first in the Editor).
// Read and written from anywhere as SettingsManager.MusicVolume, SettingsManager.Language…; whoever cares (AudioManager,
// PlayerLook, Localized) listens to Changed. The window that edits them is SettingsWindow (UI); the language key is
// UIManager's.
[DefaultExecutionOrder(-100)]   // before anyone reads the language or the volumes in their Awake / OnEnable
public class SettingsManager : MonoBehaviour
{
    [Section("Language")]
    [Tooltip("The language a launch starts in")]
    public GameLanguage startLanguage = GameLanguage.Spanish;
    [Tooltip("Every launch starts in Start Language (a visitor who switched it doesn't leave it switched for the next " +
             "one, the exhibition). Off: it keeps the last one chosen")]
    public bool resetLanguageEachLaunch = true;

    [Section("First Launch")]
    [Tooltip("Volumes and mouse sensitivity before anything was saved (0-1)")]
    [Range(0f, 1f)] public float masterVolume = DefaultVolume;
    [Range(0f, 1f)] public float musicVolume = DefaultVolume;
    [Range(0f, 1f)] public float sfxVolume = DefaultVolume;
    [Tooltip("0.5 = the sensitivity set on PlayerLook in the Inspector")]
    [Range(0f, 1f)] public float mouseSensitivity = DefaultSensitivity;
    [Tooltip("Native: the monitor's own shape. Classic: 4:3 in the middle, grey bars around (DisplayManager)")]
    public DisplayManager.Mode displayMode = DisplayManager.Mode.Native;

    // Used when no SettingsManager set them (a scene played directly in the Editor)
    const float DefaultVolume = 0.8f, DefaultSensitivity = 0.5f;
    static GameLanguage firstLanguage = GameLanguage.Spanish;
    static float firstMaster = DefaultVolume, firstMusic = DefaultVolume, firstSfx = DefaultVolume, firstSensitivity = DefaultSensitivity;
    static DisplayManager.Mode firstDisplay = DisplayManager.Mode.Native;
    static bool languageSet;   // (once per launch: coming back to the Menu doesn't reset it)

    void Awake()
    {
        firstLanguage = startLanguage;
        firstMaster = masterVolume;
        firstMusic = musicVolume;
        firstSfx = sfxVolume;
        firstSensitivity = mouseSensitivity;
        firstDisplay = displayMode;
        if (resetLanguageEachLaunch && !languageSet) Language = startLanguage;
        languageSet = true;
    }

    //---- The settings (anywhere)

    public static GameLanguage Language
    {
        get => (GameLanguage)PlayerPrefs.GetInt("Language", (int)firstLanguage);
        set => PlayerPrefs.SetInt("Language", (int)value);
    }

    public static float MasterVolume { get => PlayerPrefs.GetFloat("MasterVolume", firstMaster); set => PlayerPrefs.SetFloat("MasterVolume", value); }
    public static float MusicVolume  { get => PlayerPrefs.GetFloat("MusicVolume", firstMusic);   set => PlayerPrefs.SetFloat("MusicVolume", value); }
    public static float SfxVolume    { get => PlayerPrefs.GetFloat("SfxVolume", firstSfx);       set => PlayerPrefs.SetFloat("SfxVolume", value); }
    // Slider 0..1, middle (0.5) = the sensitivity set on PlayerLook in the Inspector
    public static float MouseSensitivity { get => PlayerPrefs.GetFloat("MouseSensitivityScale", firstSensitivity); set => PlayerPrefs.SetFloat("MouseSensitivityScale", value); }

    // How the game fills the monitor (DisplayManager)
    public static DisplayManager.Mode DisplayMode { get => (DisplayManager.Mode)PlayerPrefs.GetInt("DisplayMode", (int)firstDisplay); set => PlayerPrefs.SetInt("DisplayMode", (int)value); }

    // Multiplier for PlayerLook: 0 → x0.25, 0.5 → x1, 1 → x4 (exponential, so both halves feel even)
    public static float MouseSensitivityMultiplier => Mathf.Pow(4f, (MouseSensitivity - 0.5f) * 2f);

    public static event Action Changed;
    public static void NotifyChanged() => Changed?.Invoke();
    public static void Save() => PlayerPrefs.Save();

    // Every Localized updates at once. Used by the language key (UIManager) and the editor menu
    public static void SetLanguage(GameLanguage language)
    {
        Language = language;
        NotifyChanged();
        Save();
        Debug.Log("[Language] " + language);
    }
    public static void ToggleLanguage() => SetLanguage(Language == GameLanguage.English ? GameLanguage.Spanish : GameLanguage.English);

#if UNITY_EDITOR
    // Testing outside Play mode
    [UnityEditor.MenuItem("Memetica/Language/English", false, 500)]
    private static void SetEnglishFromMenu() => SetLanguage(GameLanguage.English);
    [UnityEditor.MenuItem("Memetica/Language/Spanish", false, 501)]
    private static void SetSpanishFromMenu() => SetLanguage(GameLanguage.Spanish);
#endif
}
