// Cursor por software: una Image que sigue al mouse y reemplaza al cursor de Windows.
// Va en el Canvas "RetroCursor". El punto de clic de cada cursor es el PIVOT de su sprite
// (flecha y mano que señala → donde apunta la punta, mano que agarra → Center). El cambio de cursor al pasar por un ícono lo hace DesktopIcon.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Point: algo clickeable (íconos) | Grab: algo que se agarra (orbes) | Busy: cargando (lo pone el UIManager)
// NotAllowed: algo interactuable pero bloqueado (no hace nada)
public enum CursorState { Default, Grab, Busy, Point, NotAllowed }

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
    private bool busy;   // mientras carga, nada más cambia el cursor

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

    // Cursor de carga: lo pone el UIManager y bloquea cualquier otro cambio hasta que se saque
    public void SetBusy(bool value)
    {
        busy = false;
        SetState(value ? CursorState.Busy : CursorState.Default);
        busy = value;
    }

    public void SetState(CursorState state)
    {
        if (busy) return;

        foreach (CursorStyle s in styles)
        {
            if (s.state != state || s.sprite == null) continue;
            cursorImage.sprite = s.sprite;
            // La Image no usa el pivot del sprite por sí sola: se lo copiamos al RectTransform
            rect.pivot = new Vector2(s.sprite.pivot.x / s.sprite.rect.width, s.sprite.pivot.y / s.sprite.rect.height);
            return;
        }
        Debug.LogWarning("[RetroCursor] No hay sprite asignado para el estado " + state + " en Styles.", this);
    }
}
