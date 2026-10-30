// Qué hace cada cosa del escritorio. Va en Managers > UIManager.
// Los íconos (DesktopIcon) solo detectan el input; en su "On Open" se elige un método de acá.
// Ejemplo: Button_Play → On Open → UIManager.PlayGame()

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("Referencias")]
    public AudioManager audioManager;

    [Space(10)]
    [Header("Escenas")]
    public string gameScene = "Game";
    [Tooltip("Original Memetica, opened from the Recycle Bin")]
    public string oldGameScene = "Game_Old";
    public string menuScene = "Menu";

    [Header("Tutorial")]
    [Tooltip("Shown when a Play icon opens a game: does it skip the tutorial? Its buttons: Yes → UIManager.StartWithoutTutorial, " +
             "No → UIManager.StartWithTutorial. Empty: the games start with the tutorial")]
    public GameObject tutorialPopup;

    [Header("Carga")]
    [Tooltip("Segundos mínimos de carga, aunque el juego cargue antes")]
    public float minLoadingTime = 1.5f;
    [Tooltip("Segundos que \"piensa\" antes de cerrar el juego")]
    public float quitDelay = 2f;

    private bool loading;
    private string pendingScene;   // the game the tutorial popup is asking about

    // F9 switches English / Spanish anytime, also in the build (handy at the exhibition)
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9)) GameSettings.ToggleLanguage();
    }

    // ---------- Íconos ----------

    public void PlayGame() => AskTutorial(gameScene);

    // Button_Play inside the Recycle Bin window
    public void PlayOldGame() => AskTutorial(oldGameScene);

    // ---------- Tutorial popup ----------

    // The popup asks first (without one: straight in, with the tutorial)
    void AskTutorial(string sceneName)
    {
        if (loading) return;   // evita cargar dos veces si se hace doble clic de más
        pendingScene = sceneName;
        if (tutorialPopup != null) OpenWindow(tutorialPopup);
        else StartGame(false);
    }

    // The popup's buttons
    public void StartWithTutorial() => StartGame(false);
    public void StartWithoutTutorial() => StartGame(true);

    void StartGame(bool skipTutorial)
    {
        if (loading || string.IsNullOrEmpty(pendingScene)) return;
        if (tutorialPopup != null) tutorialPopup.SetActive(false);
        LevelManager.SkipFromMenu = skipTutorial;   // the game scene's LevelManager reads it when it starts
        StartCoroutine(LoadScene(pendingScene));
    }

    // Used by the pause window (Game → Menu)
    public void GoToMenu()
    {
        if (loading) return;
        StartCoroutine(LoadScene(menuScene));
    }

    public void QuitGame()
    {
        if (loading) return;   // evita repetirlo si se hace clic de más
        StartCoroutine(Quit());
    }

    // ---------- Ventanas ----------
    // Funcionan aunque la ventana esté inactiva: se la referencia desde el Inspector.

    public void OpenWindow(GameObject window)
    {
        window.SetActive(true);
        window.transform.SetAsLastSibling();   // la que se abre queda adelante
    }

    public void CloseWindow(GameObject window)
    {
        window.SetActive(false);
    }

    // ---------- Interno ----------

    IEnumerator Quit()
    {
        loading = true;

        // Igual que al cargar: cursor de espera, nada clickeable y la música se apaga
        if (EventSystem.current != null) EventSystem.current.enabled = false;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetBusy(true);
        if (audioManager != null) audioManager.FadeOutMusic(quitDelay);

        yield return new WaitForSecondsRealtime(quitDelay);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // en el editor: corta el Play
#else
        Application.Quit();
#endif
    }

    IEnumerator LoadScene(string sceneName)
    {
        loading = true;

        // Mientras carga: cursor de espera y nada más se puede clickear
        if (EventSystem.current != null) EventSystem.current.enabled = false;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetBusy(true);
        if (audioManager != null) audioManager.FadeOutMusic(minLoadingTime);   // la música se apaga mientras carga
        yield return null;   // un frame para que se vea el cursor de espera antes de empezar

        // Carga en segundo plano, sin pasar todavía a la escena nueva
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        float start = Time.unscaledTime;

        // Unity deja el progreso en 0.9 hasta que se permite activar la escena
        while (op.progress < 0.9f || Time.unscaledTime - start < minLoadingTime)
            yield return null;

        // Leave the time and pause state clean for the next scene (coming from the pause window)
        Time.timeScale = 1f;
        PauseMenu.GameIsPaused = false;

        op.allowSceneActivation = true;   // acá se cambia de escena
    }
}
