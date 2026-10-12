// Window button (close, minimize, tabs, Yes/No of a popup).
// - Hover: changes the cursor (by default, the pointing hand).
// - Left click: runs "On Click", where you pick a method (e.g. UIManager.CloseWindow, SettingsWindow.SelectTab).
// - Double Click: tick it so it only reacts to a double click (like a desktop icon).
// Needs something with Raycast Target on the same object: an Image, even a transparent one (alpha 0).

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class WindowButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover")]
    [Tooltip("Cursor shown while the mouse is over it")]
    public CursorState onHover = CursorState.Point;

    [Space(10)]
    [Header("Click")]
    [Tooltip("Off: reacts to a single click. On: only to a double click")]
    public bool doubleClick = false;
    [Tooltip("What happens on click")]
    [FormerlySerializedAs("onOpen")]   // keeps the event of objects that used the old DesktopIcon
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
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (doubleClick && eventData.clickCount != 2) return;
        onClick.Invoke();
    }

    // When the window closes, this button turns off with the mouse over it: the cursor goes back to the arrow
    void OnDisable()
    {
        if (hovering && RetroCursor.Instance != null) RetroCursor.Instance.SetState(CursorState.Default);
        hovering = false;
    }
}
