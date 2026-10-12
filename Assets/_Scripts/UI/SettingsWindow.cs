using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Global game settings: saved with PlayerPrefs, shared by the Menu and the Game.
// Anyone who cares (AudioManager, PlayerLook) listens to Changed and applies the new values.
public static class GameSettings
{

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

    float savedMaster, savedMusic, savedSfx, savedSensitivity;   // values when the window opened (for Cancel)

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

        RefreshSliders();
        SelectTab(defaultPage);
    }

    void OnDisable() => GameSettings.Save();

    // ---------- Buttons ----------

    public void SelectTab(int index)
    {
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == index);
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
}
