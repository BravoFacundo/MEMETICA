using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;
    public GameObject pauseMenuUI;
    public GameObject pointerUI;
    public AudioMixer audioMixer;
        
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
        pointerUI.SetActive(true);
        Time.timeScale = 1f;
        Camera.main.GetComponent<FPPlayerLook>().playerCanLook = true;
        Cursor.lockState = CursorLockMode.Locked;
    }
    public void Pause()
    {
        GameIsPaused = true;
        pauseMenuUI.SetActive(true);
        pointerUI.SetActive(false);
        Time.timeScale = 0f;
        Camera.main.GetComponent<FPPlayerLook>().playerCanLook = false;
        Cursor.lockState = CursorLockMode.None;
    }

    public void QuitGame()
    {
        Debug.Log("Cerrando el juego");
        Application.Quit();
    }

    public void SetVolume(float volume)
    {
        audioMixer.SetFloat("volume", volume);
    }
    public void SetMouseSens(float Sens)
    {
        GameObject.Find("Main Camera").GetComponent<FPPlayerLook>().mouseSensitivity = Sens;
    }

}
