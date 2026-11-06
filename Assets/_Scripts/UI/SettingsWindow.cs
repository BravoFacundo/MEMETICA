using System;
using UnityEngine;
using UnityEngine.UI;

// The configuration window, shared by the Menu (Game Configuration) and the pause (Pause Menu). The values themselves
// live in SettingsManager (saved with PlayerPrefs); this is only their view.
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
    public int defaultPage = 0;

    [Header("Sound")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Mouse")]
    public Slider sensitivitySlider;

    [Header("Display")]
    [Tooltip("One per mode (Native, Classic 4:3): shown only on the selected one, e.g. a check mark")]
    [SerializeField] private GameObject[] displayMarks;

    [Header("Language")]
    [Tooltip("One per language (English, Spanish): shown only on the selected one, e.g. a check mark")]
    [SerializeField] private GameObject[] languageMarks;

    float savedMaster, savedMusic, savedSfx, savedSensitivity;
    GameLanguage savedLanguage;   // values when the window opened (for Cancel)
    DisplayManager.Mode savedDisplay;

    void Awake()
    {
        Bind(masterSlider, v => SettingsManager.MasterVolume = v);
        Bind(musicSlider, v => SettingsManager.MusicVolume = v);
        Bind(sfxSlider, v => SettingsManager.SfxVolume = v);
        Bind(sensitivitySlider, v => SettingsManager.MouseSensitivity = v);
    }

    void OnEnable()
    {
        savedMaster = SettingsManager.MasterVolume;
        savedMusic = SettingsManager.MusicVolume;
        savedSfx = SettingsManager.SfxVolume;
        savedSensitivity = SettingsManager.MouseSensitivity;
        savedLanguage = SettingsManager.Language;
        savedDisplay = SettingsManager.DisplayMode;

        RefreshSliders();
        RefreshLanguageMarks();
        RefreshMarks(displayMarks, (int)SettingsManager.DisplayMode);
        SelectTab(defaultPage);
    }

    void OnDisable() => SettingsManager.Save();

    // ---------- Buttons ----------

    public void SelectTab(int index)
    {
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == index);
    }

    // Language buttons (WindowButton → On Click): 0 English, 1 Spanish
    public void SetLanguage(int language)
    {
        SettingsManager.Language = (GameLanguage)language;
        SettingsManager.NotifyChanged();
        RefreshLanguageMarks();
    }

    // Display buttons (WindowButton → On Click): 0 Native, 1 Classic (4:3 in the middle). Applies at once
    public void SetDisplayMode(int mode)
    {
        SettingsManager.DisplayMode = (DisplayManager.Mode)mode;
        SettingsManager.NotifyChanged();
        RefreshMarks(displayMarks, mode);
    }

    public void Apply()
    {
        gameObject.SetActive(false);   // changes were already applied live; OnDisable saves them
    }

    public void Cancel()
    {
        SettingsManager.MasterVolume = savedMaster;
        SettingsManager.MusicVolume = savedMusic;
        SettingsManager.SfxVolume = savedSfx;
        SettingsManager.MouseSensitivity = savedSensitivity;
        SettingsManager.Language = savedLanguage;
        SettingsManager.DisplayMode = savedDisplay;
        SettingsManager.NotifyChanged();
        gameObject.SetActive(false);
    }

    // ---------- Internal ----------

    void Bind(Slider slider, Action<float> setter)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.onValueChanged.AddListener(v => { setter(v); SettingsManager.NotifyChanged(); });
    }

    void RefreshSliders()
    {
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(SettingsManager.MasterVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(SettingsManager.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(SettingsManager.SfxVolume);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(SettingsManager.MouseSensitivity);
    }

    static void RefreshMarks(GameObject[] marks, int selected)
    {
        if (marks == null) return;
        for (int i = 0; i < marks.Length; i++)
            if (marks[i] != null) marks[i].SetActive(i == selected);
    }

    private void RefreshLanguageMarks()
    {
        for (int i = 0; i < languageMarks.Length; i++)
            if (languageMarks[i] != null) languageMarks[i].SetActive(i == (int)SettingsManager.Language);
    }
}
