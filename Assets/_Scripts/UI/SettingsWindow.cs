using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Global game settings: saved with PlayerPrefs, shared by the Menu and the Game.
// Anyone who cares (AudioManager, PlayerLook) listens to Changed and applies the new values.
public static class GameSettings
{
    // Mouse sensitivity is stored as 0..1 and mapped to this range of PlayerLook.mouseSensitivity
    public const float SensitivityMin = 20f;
    public const float SensitivityMax = 300f;

    public static float MasterVolume { get => PlayerPrefs.GetFloat("MasterVolume", 0.8f); set => PlayerPrefs.SetFloat("MasterVolume", value); }
    public static float MusicVolume  { get => PlayerPrefs.GetFloat("MusicVolume", 0.8f);  set => PlayerPrefs.SetFloat("MusicVolume", value); }
    public static float SfxVolume    { get => PlayerPrefs.GetFloat("SfxVolume", 0.8f);    set => PlayerPrefs.SetFloat("SfxVolume", value); }
    public static float MouseSensitivity { get => PlayerPrefs.GetFloat("MouseSensitivity", 0.3f); set => PlayerPrefs.SetFloat("MouseSensitivity", value); }

    public static float MouseSensitivityValue => Mathf.Lerp(SensitivityMin, SensitivityMax, MouseSensitivity);

    public static event Action Changed;
    public static void NotifyChanged() => Changed?.Invoke();
    public static void Save() => PlayerPrefs.Save();
}

// The configuration window, shared by the Menu (Game Configuration) and the pause (Pause Menu).
// Same prefab in both scenes; per scene you choose which tabs exist and which buttons are shown.
//   Tabs:    each tab button calls SelectTab(index) (WindowButton → On Click). Opens on Default Tab.
//   Sliders: changes apply live (you hear/feel them right away).
//   Apply:   keeps the changes and closes.  Cancel: goes back to the values it had when opened, and closes.
//   Closing the window any other way (e.g. Continue in the pause) keeps the changes.
public class SettingsWindow : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        [Tooltip("Content shown when this tab is selected")]
        public GameObject panel;
        [Tooltip("The tab button image (swaps between normal and selected sprite)")]
        public Image tabImage;
    }

    [Header("Tabs")]
    public Tab[] tabs;
    [Tooltip("Tab selected every time the window opens")]
    public int defaultTab = 0;
    public Sprite tabNormal;
    public Sprite tabSelected;

    [Header("Sound")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Mouse")]
    public Slider sensitivitySlider;

    [Header("Display")]
    [Tooltip("Optional: shows the current resolution as text (read only)")]
    public TMP_Text resolutionText;

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
        if (resolutionText != null)
            resolutionText.text = Screen.width + " x " + Screen.height + " - " + Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value) + " Hz";
        SelectTab(defaultTab);
    }

    void OnDisable() => GameSettings.Save();

    // ---------- Buttons ----------

    public void SelectTab(int index)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            bool selected = i == index;
            if (tabs[i].panel != null) tabs[i].panel.SetActive(selected);
            if (tabs[i].tabImage != null && tabNormal != null && tabSelected != null)
                tabs[i].tabImage.sprite = selected ? tabSelected : tabNormal;
        }
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
