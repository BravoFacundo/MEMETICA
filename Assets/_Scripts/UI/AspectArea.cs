using UnityEngine;

// The part of a canvas the game shows (DisplayManager.View): Native, the whole screen; Classic, the 4:3 window in the
// middle. Put it on a stretched child of each canvas (e.g. Canvas_HUD > View) and move the canvas' content inside it:
// everything anchored to its edges stays inside the 4:3 window, never under the bars.
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class AspectArea : MonoBehaviour
{
    private RectTransform rect;
    private Rect applied = new Rect(-1f, -1f, -1f, -1f);

    void OnEnable() => rect = (RectTransform)transform;

    void LateUpdate()
    {
        Rect view = Application.isPlaying ? DisplayManager.View : new Rect(0f, 0f, 1f, 1f);   // (editing: the whole canvas)
        if (view == applied) return;
        applied = view;
        rect.anchorMin = view.min;
        rect.anchorMax = view.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
