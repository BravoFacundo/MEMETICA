// Cambia el cursor mientras el mouse está sobre este elemento de UI (por ejemplo, la mano: Grab).
// El elemento necesita Raycast Target activado.

using UnityEngine;
using UnityEngine.EventSystems;

public class CursorHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CursorState state = CursorState.Grab;
    private bool hovering;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetState(state);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetState(CursorState.Default);
    }

    // Si el elemento se oculta con el mouse encima, el cursor vuelve a la flecha
    void OnDisable()
    {
        if (hovering && RetroCursor.Instance != null) RetroCursor.Instance.SetState(CursorState.Default);
        hovering = false;
    }
}
