// Cursor por software: una Image que sigue al mouse y reemplaza al cursor de Windows.
// Va en el Canvas "RetroCursor". El punto de clic de cada cursor es el PIVOT de su sprite
// (flecha → Top Left, mano (Grab) → Center). Para cambiar de cursor sobre un elemento, usar CursorHover.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CursorState { Default, Grab }

[RequireComponent(typeof(Canvas))]
public class RetroCursor : MonoBehaviour
{
    [System.Serializable]
    public class CursorStyle
    {
        public CursorState state;
        public Sprite sprite;
    }

    public static RetroCursor Instance { get; private set; }

    public Image cursorImage;
    public List<CursorStyle> styles = new List<CursorStyle>();

    private RectTransform rect;

    void Awake()
    {
        Instance = this;
        GetComponent<Canvas>().sortingOrder = 999;   // arriba de la UI (el marco va en 1000)
        rect = cursorImage.rectTransform;
        cursorImage.raycastTarget = false;           // que no bloquee clics
        SetState(CursorState.Default);
    }

    void OnEnable() { Cursor.visible = false; }
    void OnApplicationFocus(bool focused) { if (focused) Cursor.visible = false; }   // Windows lo vuelve a mostrar al cambiar de ventana
    void OnDisable() { Cursor.visible = true; }

    void LateUpdate()
    {
        rect.position = Input.mousePosition;   // en un Canvas Overlay, pantalla = posición
    }

    public void SetState(CursorState state)
    {
        foreach (CursorStyle s in styles)
        {
            if (s.state != state || s.sprite == null) continue;
            cursorImage.sprite = s.sprite;
            // La Image no usa el pivot del sprite por sí sola: se lo copiamos al RectTransform
            rect.pivot = new Vector2(s.sprite.pivot.x / s.sprite.rect.width, s.sprite.pivot.y / s.sprite.rect.height);
            return;
        }
    }
}
