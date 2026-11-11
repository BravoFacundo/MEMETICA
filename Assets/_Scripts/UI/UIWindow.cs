using UnityEngine;

// What a window is, for its sound when it appears (AudioManager > UI Sounds):
//   Window   Open Sound (Configuration, Recycle Bin, the pause's windows, the end screen's…)
//   Popup    Popup Sound (a question: Exit?, the tutorial one)
// Opens With Button: a button (an icon, a page tab…) opens it: closed at the start, and still closed when its screen
// opens. Off: it opens with its screen (the pause's own windows) or is there from the start (UIManager).
// Heard when UIManager opens it (an icon, a button) or when its screen shows it (AppearInOrder). Without it: a window
// opened by UIManager sounds as Window; a screen's element appears silent.
public class UIWindow : MonoBehaviour
{
    public enum Kind { Window, Popup }
    public Kind kind = Kind.Window;
    [Tooltip("A button opens it (an icon, a page button): closed at the start and when its screen opens. Off: it opens " +
             "with its screen, or is there from the start")]
    public bool opensWithButton = false;

    // The sound for showing this object (null: no UIWindow on it)
    public static UISound? SoundOf(GameObject g) =>
        g != null && g.TryGetComponent(out UIWindow w) ? (w.kind == Kind.Popup ? UISound.Popup : UISound.Open) : (UISound?)null;
}
