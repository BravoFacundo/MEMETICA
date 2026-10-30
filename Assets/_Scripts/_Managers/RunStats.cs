using UnityEngine;
using UnityEngine.SceneManagement;

// What this run counts besides the MemeDex, for the score (MemeDex.CurrentRun → ScoreBoard.Run):
//   Falls   falls into the void while the countdown runs (PlayerMove reappearing you). The portal's way back, or a
//           respawn before the countdown started (the tutorial), don't count: 0 falls is Flawless
// From zero each time the countdown starts or a scene loads. Nothing to place in the scene.
public static class RunStats
{
    public static int Falls { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        Falls = 0;
        PlayerMove.VoidRespawned -= OnVoidRespawn;
        PlayerMove.VoidRespawned += OnVoidRespawn;
        GameManager.StateChanged -= OnState;
        GameManager.StateChanged += OnState;
        SceneManager.sceneLoaded -= OnScene;
        SceneManager.sceneLoaded += OnScene;
    }

    static void OnVoidRespawn()
    {
        if (GameManager.Instance != null && GameManager.Instance.Running) Falls++;
    }

    static void OnState(GameManager.State state)
    {
        if (state == GameManager.State.Running) Falls = 0;
    }

    static void OnScene(Scene scene, LoadSceneMode mode) => Falls = 0;
}
