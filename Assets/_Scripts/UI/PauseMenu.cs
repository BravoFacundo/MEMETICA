using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("References")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private GameObject frameUI;   // Canvas_Frame: el marco del monitor, solo en pausa
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private UIManager uiManager;  // loads the Menu and quits with the busy cursor

    void Start()
    {
        // A fresh Game scene always starts unpaused (the static flag survives scene loads)
        GameIsPaused = false;
        Time.timeScale = 1f;
        if (frameUI != null) frameUI.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }

        }
    }

    public void Resume()
    {
        GameIsPaused = false;

        pauseMenuUI.SetActive(false);
        if (frameUI != null) frameUI.SetActive(false);
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetFollowMouse(false);   // vuelve la mira fija (Crosshair pone el ícono)

        Time.timeScale = 1f;
        Camera.main.GetComponent<PlayerLook>().playerCanLook = true;
        Cursor.lockState = CursorLockMode.Locked;
    }
    public void Pause()
    {
        GameIsPaused = true;

        pauseMenuUI.SetActive(true);
        if (frameUI != null) frameUI.SetActive(true);
        if (RetroCursor.Instance != null)
        {
            RetroCursor.Instance.SetFollowMouse(true);              // cursor libre, como en el menú
            RetroCursor.Instance.SetState(CursorState.Default);     // la flecha
        }

        Time.timeScale = 0f;
        Camera.main.GetComponent<PlayerLook>().playerCanLook = false;
        Cursor.lockState = CursorLockMode.None;
    }

    // Pause → Menu: the game stays frozen behind the busy cursor; UIManager unpauses right before switching
    public void ReturnToMenu()
    {
        if (uiManager != null) uiManager.GoToMenu();
        else Debug.LogWarning("[PauseMenu] Assign the UIManager to return to the Menu.", this);
    }

    public void QuitGame()
    {
        if (uiManager != null) uiManager.QuitGame();
        else Application.Quit();
    }

    public void SetVolume(float volume)
    {
        audioMixer.SetFloat("volume", volume);
    }
    public void SetMouseSens(float Sens)
    {
        GameObject.Find("Main Camera").GetComponent<PlayerLook>().mouseSensitivity = Sens;
    }

}
