// Cambia el cursor mientras el mouse está sobre este elemento de UI.
// Ejemplos: Link en botones, Text en campos de texto, Move en la barra de título (full version), Forbidden en algo bloqueado.
// El elemento necesita algo con Raycast Target activado (una Image, aunque sea transparente).

using UnityEngine;
using UnityEngine.EventSystems;

public class CursorHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CursorState state = CursorState.Link;
    private bool hovering;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetHover(state);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (RetroCursor.Instance != null) RetroCursor.Instance.ClearHover();
    }

    // Si la ventana se cierra con el mouse encima, el cursor no queda trabado en este estado
    void OnDisable()
    {
        if (hovering && RetroCursor.Instance != null) RetroCursor.Instance.ClearHover();
        hovering = false;
    }
}
