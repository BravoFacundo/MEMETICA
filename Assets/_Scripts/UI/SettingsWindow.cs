using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum GameLanguage { English, Spanish }

// Global game settings: saved with PlayerPrefs, shared by the Menu and the Game.
// Anyone who cares (AudioManager, PlayerLook, Localized) listens to Changed and applies the new values.
public static class GameSettings
{
    // First launch: Spanish if the system is in Spanish, English otherwise
    public static GameLanguage Language
    {
        get => (GameLanguage)PlayerPrefs.GetInt("Language", Application.systemLanguage == SystemLanguage.Spanish ? 1 : 0);
        set => PlayerPrefs.SetInt("Language", (int)value);
    }

    public static float MasterVolume { get => PlayerPrefs.GetFloat("MasterVolume", 0.8f); set => PlayerPrefs.SetFloat("MasterVolume", value); }
    public static float MusicVolume  { get => PlayerPrefs.GetFloat("MusicVolume", 0.8f);  set => PlayerPrefs.SetFloat("MusicVolume", value); }
    public static float SfxVolume    { get => PlayerPrefs.GetFloat("SfxVolume", 0.8f);    set => PlayerPrefs.SetFloat("SfxVolume", value); }
    // Slider 0..1, middle (0.5) = the sensitivity set on PlayerLook in the Inspector
    public static float MouseSensitivity { get => PlayerPrefs.GetFloat("MouseSensitivityScale", 0.5f); set => PlayerPrefs.SetFloat("MouseSensitivityScale", value); }

    // Multiplier for PlayerLook: 0 → x0.25, 0.5 → x1, 1 → x4 (exponential, so both halves feel even)
    public static float MouseSensitivityMultiplier => Mathf.Pow(4f, (MouseSensitivity - 0.5f) * 2f);

    public static event Action Changed;
    public static void NotifyChanged() => Changed?.Invoke();
    public static void Save() => PlayerPrefs.Save();

    // Every Localized updates at once. Used by F9 (UIManager) and the editor menu
    public static void SetLanguage(GameLanguage language)
    {
        Language = language;
        NotifyChanged();
        Save();
        Debug.Log("[Language] " + language);
    }
    public static void ToggleLanguage() => SetLanguage(Language == GameLanguage.English ? GameLanguage.Spanish : GameLanguage.English);

#if UNITY_EDITOR
    // Testing outside Play mode (F9 is handled by UIManager while playing)
    [UnityEditor.MenuItem("Memetica/Idioma/Inglés")]
    private static void SetEnglishFromMenu() => SetLanguage(GameLanguage.English);
    [UnityEditor.MenuItem("Memetica/Idioma/Español")]
    private static void SetSpanishFromMenu() => SetLanguage(GameLanguage.Spanish);
#endif
}

// The configuration window, shared by the Menu (Game Configuration) and the pause (Pause Menu).
//   Pages:   one GameObject per tab; each tab button calls SelectTab(index) (WindowButton → On Click). Opens on Default Page.
//   Sliders: changes apply live (you hear/feel them right away).
//   Apply:   keeps the changes and closes.  Cancel: goes back to the values it had when opened, and closes.
//   Closing the window any other way (e.g. Continue in the pause) keeps the changes.
public class SettingsWindow : MonoBehaviour
{
    [Header("Pages")]
    [Tooltip("One page per tab, in the same order as the SelectTab indexes")]
    public GameObject[] pages;
    [Tooltip("Page shown every time the window opens")]
    [FormerlySerializedAs("defaultTab")]
    public int defaultPage = 0;

    [Header("Sound")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Mouse")]
    public Slider sensitivitySlider;

    [Header("Language")]
    [Tooltip("One per language (English, Spanish): shown only on the selected one, e.g. a check mark")]
    [SerializeField] private GameObject[] languageMarks;

    float savedMaster, savedMusic, savedSfx, savedSensitivity;
    GameLanguage savedLanguage;   // values when the window opened (for Cancel)

    void Awake()
    {
        Bind(masterSlider, v => GameSettings.MasterVolume = v);
        Bind(musicSlider, v => GameSettings.MusicVolume = v);
        Bind(sfxSlider, v => GameSettings.SfxVolume = v);
        Bind(sensitivitySlider, v => GameSettings.MouseSensitivity = v);
    }

    void OnEnable()
    {
        savedMaster = GameSettings.MasterVolume;
        savedMusic = GameSettings.MusicVolume;
        savedSfx = GameSettings.SfxVolume;
        savedSensitivity = GameSettings.MouseSensitivity;
        savedLanguage = GameSettings.Language;

        RefreshSliders();
        RefreshLanguageMarks();
        SelectTab(defaultPage);
    }

    void OnDisable() => GameSettings.Save();

    // ---------- Buttons ----------

    public void SelectTab(int index)
    {
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == index);
    }

    // Language buttons (WindowButton → On Click): 0 English, 1 Spanish
    public void SetLanguage(int language)
    {
        GameSettings.Language = (GameLanguage)language;
        GameSettings.NotifyChanged();
        RefreshLanguageMarks();
    }

    public void Apply()
    {
        gameObject.SetActive(false);   // changes were already applied live; OnDisable saves them
    }

    public void Cancel()
    {
        GameSettings.MasterVolume = savedMaster;
        GameSettings.MusicVolume = savedMusic;
        GameSettings.SfxVolume = savedSfx;
        GameSettings.MouseSensitivity = savedSensitivity;
        GameSettings.Language = savedLanguage;
        GameSettings.NotifyChanged();
        gameObject.SetActive(false);
    }

    // ---------- Internal ----------

    void Bind(Slider slider, Action<float> setter)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.onValueChanged.AddListener(v => { setter(v); GameSettings.NotifyChanged(); });
    }

    void RefreshSliders()
    {
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
    }

    private void RefreshLanguageMarks()
    {
        for (int i = 0; i < languageMarks.Length; i++)
            if (languageMarks[i] != null) languageMarks[i].SetActive(i == (int)GameSettings.Language);
    }
}
