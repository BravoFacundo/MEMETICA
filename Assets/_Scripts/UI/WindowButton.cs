// Botón de ventana (cerrar, minimizar, Sí/No de un popup): clic simple.
// - Hover: cambia el cursor (por defecto, la mano que señala).
// - Clic izquierdo: ejecuta "On Click", donde se elige un método del UIManager
//   (por ejemplo UIManager.CloseWindow con la ventana, o UIManager.QuitGame).
// Necesita algo con Raycast Target en el mismo objeto: una Image, aunque sea transparente (alpha 0).

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class WindowButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover")]
    [Tooltip("Cursor que se muestra con el mouse encima")]
    public CursorState onHover = CursorState.Point;

    [Space(10)]
    [Header("Click")]
    [Tooltip("Qué pasa con un clic (elegir un método del UIManager)")]
    public UnityEvent onClick;

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
        if (eventData.button == PointerEventData.InputButton.Left)
            onClick.Invoke();
    }

    // Al cerrar la ventana, este botón se apaga con el mouse encima: el cursor vuelve a la flecha
    void OnDisable()
    {
        if (hovering && RetroCursor.Instance != null) RetroCursor.Instance.SetState(CursorState.Default);
        hovering = false;
    }
}
