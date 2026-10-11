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

    [Header("Carga")]
    [Tooltip("Segundos mínimos de carga, aunque el juego cargue antes")]
    public float minLoadingTime = 1.5f;
    [Tooltip("Segundos que \"piensa\" antes de cerrar el juego")]
    public float quitDelay = 2f;

    private bool loading;

    // ---------- Íconos ----------

    public void PlayGame()
    {
        if (loading) return;   // evita cargar dos veces si se hace doble clic de más
        StartCoroutine(LoadGame());
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

    IEnumerator LoadGame()
    {
        loading = true;

        // Mientras carga: cursor de espera y nada más se puede clickear
        if (EventSystem.current != null) EventSystem.current.enabled = false;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetBusy(true);
        if (audioManager != null) audioManager.FadeOutMusic(minLoadingTime);   // la música se apaga mientras carga
        yield return null;   // un frame para que se vea el cursor de espera antes de empezar

        // Carga en segundo plano, sin pasar todavía a la escena nueva
        AsyncOperation op = SceneManager.LoadSceneAsync(gameScene);
        op.allowSceneActivation = false;
        float start = Time.unscaledTime;

        // Unity deja el progreso en 0.9 hasta que se permite activar la escena
        while (op.progress < 0.9f || Time.unscaledTime - start < minLoadingTime)
            yield return null;

        op.allowSceneActivation = true;   // acá se cambia de escena
    }
}
