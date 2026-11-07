using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// How the run ends when you win (part of the GameManager):
//   End Screen  Game: the End Screen window (or the old Win Screen), the player stops
//   Ascend      OriginalGame: every island falls away, you rise into the sky, the Ending Popup shows, then the Menu
// Moved here from the LevelManager (its Game section).
public partial class GameManager
{
    public enum Ending { EndScreen, Ascend }

    [Section("Ending")]
    [Tooltip("End Screen: the End Screen window (Game). Ascend: the islands fall, you rise into the sky and it goes back " +
             "to the Menu (OriginalGame)")]
    public Ending ending = Ending.EndScreen;

    [Section("Ascend", nameof(ending), Ending.Ascend)]
    [Tooltip("Seconds the islands fall before going back to the Menu")]
    public float endingDuration = 10f;
    [Tooltip("How fast you speed up rising into the sky (m/s², gravity pulls 9.8 the other way)")]
    public float endingRiseAcceleration = 25f;
    [Tooltip("The top speed you rise at (m/s). Low: you don't go so high that the sky shows its seams")]
    public float endingRiseSpeed = 8f;
    [Tooltip("Shown while you rise (PopupManager: a popup of its panel fades in, anything else is just turned on)")]
    public GameObject endingPopup;
    [Tooltip("Seconds after the ending starts that it shows")]
    public float endingPopupDelay = 1f;
    [Tooltip("The Menu scene (used if this scene has no UIManager)")]
    public string menuScene = "Menu";

    IEnumerator Ascend()
    {
        if (player != null) player.ascend = true;

        // Every island falls (later: the bridges first, at random, fading out)
        foreach (Island island in FindObjectsOfType<Island>()) island.FallAway();

        if (endingPopup != null)
        {
            yield return new WaitForSeconds(Mathf.Min(endingPopupDelay, endingDuration));
            PopupManager.Show(endingPopup);
        }
        yield return new WaitForSeconds(Mathf.Max(endingDuration - (endingPopup != null ? endingPopupDelay : 0f), 0f));

        // Back to the desktop (Menu) instead of closing the game: the next visitor can play right away
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui != null) ui.GoToMenu();
        else { Time.timeScale = 1f; UIManager.GameIsPaused = false; SceneManager.LoadScene(menuScene); }
    }
}
