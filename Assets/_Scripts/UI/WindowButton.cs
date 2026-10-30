// Window button (close, minimize, tabs, Yes/No of a popup).
// - Hover: changes the cursor (by default, the pointing hand).
// - Left click: runs "On Click", where you pick a method (e.g. UIManager.CloseWindow, SettingsWindow.SelectTab).
// - Double Click: tick it so it only reacts to a double click (like a desktop icon). A single click on it shows the
//   NotificationWindow (e.g. "Use double click to open icons") if the scene has one and Hint On Single Click is on.
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
    [Tooltip("Double Click: a single click (no second one in time) shows the scene's NotificationWindow")]
    public bool hintOnSingleClick = true;
    [Tooltip("What happens on click")]
    [FormerlySerializedAs("onOpen")]   // keeps the event of objects that used the old DesktopIcon
    public UnityEvent onClick;

    private bool hovering;
    private Coroutine waitingSecond;

    // Unity counts a second click within 0.3 s as a double click: a bit more, and it was a single one
    const float DoubleClickTime = 0.35f;

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
        if (doubleClick && eventData.clickCount != 2)
        {
            if (eventData.clickCount == 1 && hintOnSingleClick) { StopWaiting(); waitingSecond = StartCoroutine(WaitSecondClick()); }
            return;
        }
        StopWaiting();
        onClick.Invoke();
    }

    // No second click in time: it was a single click, the notice says how to open it
    System.Collections.IEnumerator WaitSecondClick()
    {
        yield return new WaitForSecondsRealtime(DoubleClickTime);
        waitingSecond = null;
        NotificationWindow.Notify();
    }

    void StopWaiting()
    {
        if (waitingSecond != null) StopCoroutine(waitingSecond);
        waitingSecond = null;
    }

    // When the window closes, this button turns off with the mouse over it: the cursor goes back to the arrow
    void OnDisable()
    {
        if (hovering && RetroCursor.Instance != null) RetroCursor.Instance.SetState(CursorState.Default);
        hovering = false;
        waitingSecond = null;   // (its coroutine stopped with it)
    }
}
