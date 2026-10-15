using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Swaps this object's text (TextMeshPro) or image (UI Image / SpriteRenderer) when the language changes.
// When added, it copies the current text or sprite as the English version: you only fill in Spanish.
public class Localized : MonoBehaviour
{
    [Header("Text")]
    [SerializeField, TextArea] private string english;
    [SerializeField, TextArea] private string spanish;

    [Header("Image")]
    [SerializeField] private Sprite englishSprite;
    [SerializeField] private Sprite spanishSprite;

    private TMP_Text text;
    private Image image;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        image = GetComponent<Image>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        GameSettings.Changed += ApplyLanguage;
        ApplyLanguage();
    }
    private void OnDisable() => GameSettings.Changed -= ApplyLanguage;

    private void ApplyLanguage()
    {
        bool isSpanish = GameSettings.Language == GameLanguage.Spanish;

        if (text != null && !string.IsNullOrEmpty(spanish)) text.text = isSpanish ? spanish : english;

        Sprite sprite = isSpanish && spanishSprite != null ? spanishSprite : englishSprite;
        if (sprite == null) return;
        if (image != null) image.sprite = sprite;
        if (spriteRenderer != null) spriteRenderer.sprite = sprite;
    }

    private void Reset()
    {
        if (TryGetComponent(out TMP_Text currentText)) english = currentText.text;
        if (TryGetComponent(out Image currentImage)) englishSprite = currentImage.sprite;
        if (TryGetComponent(out SpriteRenderer currentRenderer)) englishSprite = currentRenderer.sprite;
    }
}
