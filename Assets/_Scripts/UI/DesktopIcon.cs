// Ícono del escritorio: maneja todo su input.
// - Hover: cambia el cursor (por defecto, la mano que señala) y vuelve a la flecha al salir.
// - Doble clic izquierdo: ejecuta "On Open", donde se elige un método del UIManager
//   (por ejemplo UIManager.PlayGame o UIManager.OpenWindow).
// Va en la raíz del botón. Los clics y el hover sobre la imagen o el texto cuentan.

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class DesktopIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover")]
    [Tooltip("Cursor que se muestra con el mouse encima")]
    [FormerlySerializedAs("hoverCursor")]   // conserva lo que ya estaba asignado con el nombre viejo
    public CursorState onHover = CursorState.Point;

    [Space(10)]
    [Header("Open (doble clic)")]
    [Tooltip("Qué pasa con doble clic (elegir un método del UIManager)")]
    public UnityEvent onOpen;

    private bool hovering;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetState(onHover);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetState(CursorState.Default);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
            onOpen.Invoke();
    }

    // Si el ícono se oculta con el mouse encima, el cursor vuelve a la flecha
    void OnDisable()
    {
        if (hovering && RetroCursor.Instance != null) RetroCursor.Instance.SetState(CursorState.Default);
        hovering = false;
    }
}
