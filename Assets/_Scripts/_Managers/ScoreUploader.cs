using UnityEngine;

// When the game opens, the itch.io runs that couldn't go online before (no internet then) go now; the console's log
// says so a moment (DebugConsole). By hand, every run not online yet (the exhibition's too): the console's scores
// (ScoreConsole). Works in every scene (it makes itself, nothing to place).
public class ScoreUploader : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindObjectOfType<ScoreUploader>() != null) return;
        GameObject go = new GameObject("ScoreUploader");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<ScoreUploader>().UploadItch();
    }

    async void UploadItch()
    {
        var (sent, _) = await OnlineScoreBoard.UploadPending(false);
        if (sent > 0) DebugConsole.Log($"{sent} score(s) sent online");
    }
}
