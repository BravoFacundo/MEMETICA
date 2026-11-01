using System.Collections;
using UnityEngine;

// A popup that stops the game until you click: time frozen (the elevator, the platforms, the countdown…), you can't
// move or look, and any click, key or gamepad button (not the pause keys) closes it after Min Time, then everything
// goes on. Its panel says "Click to start" / "Click to continue". One at a time.
// Who opens one: Elevator (Ride Popups with Modal), PopupTrigger (Modal), GameManager (the virus alert). A popup of
// the tutorial panel shows and hides through the TutorialManager (its fade); any other panel is just turned on and off.
public class ModalPopup : MonoBehaviour
{
    // A modal popup is open now (the game is stopped for it)
    public static bool Open { get; private set; }

    private static ModalPopup runner;

    // Shows it and stops the game until it is clicked; Closed is called once it is gone and the game runs again
    public static void Show(GameObject panel, System.Action closed = null, float minTime = 0.6f)
    {
        if (panel == null) { closed?.Invoke(); return; }
        if (runner == null) runner = new GameObject("ModalPopup").AddComponent<ModalPopup>();
        runner.StartCoroutine(runner.Run(panel, closed, minTime));
    }

    IEnumerator Run(GameObject panel, System.Action closed, float minTime)
    {
        while (Open) yield return null;   // (another one first)
        Open = true;
        PlayerMove player = FindObjectOfType<PlayerMove>();
        PlayerLook look = FindObjectOfType<PlayerLook>();
        bool wasInput = player != null && player.InputLocked, wasLook = look != null && look.LookLocked;
        if (player != null) player.InputLocked = true;
        if (look != null) look.LookLocked = true;
        SetShown(panel, true);

        // A click during Min Time isn't lost: it closes it as soon as Min Time is over (before: ignored, it looked stuck)
        bool clicked = false;
        for (float shownFor = 0f; ; )
        {
            if (!PauseMenu.GameIsPaused)
            {
                Time.timeScale = 0f;   // (again every frame: leaving the pause sets it back to 1)
                shownFor += Time.unscaledDeltaTime;
                bool mouse = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
                bool key = Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape) && !Input.GetKeyDown(KeyCode.P) &&
                           !Input.GetKeyDown(KeyCode.JoystickButton7);   // (not the pause keys)
                if ((mouse || key) && shownFor >= 0.15f) clicked = true;   // (not the click that opened it)
                if (clicked && shownFor >= minTime) break;
            }
            yield return null;
        }

        SetShown(panel, false);
        if (!PauseMenu.GameIsPaused) Time.timeScale = 1f;
        yield return null;   // (that click doesn't grab or jump too)
        if (player != null) player.InputLocked = wasInput;
        if (look != null) look.LookLocked = wasLook;
        Open = false;
        closed?.Invoke();
    }

    static void SetShown(GameObject panel, bool on)
    {
        TutorialManager tutorial = TutorialManager.Instance;
        bool tutorialPopup = tutorial != null && tutorial.popupCanvas != null && panel.transform.IsChildOf(tutorial.popupCanvas);
        if (!tutorialPopup) { panel.SetActive(on); return; }
        if (on) tutorial.Show(panel, true); else tutorial.Close(panel);
    }

    void OnDestroy() { if (runner == this) { runner = null; Open = false; } }
}
