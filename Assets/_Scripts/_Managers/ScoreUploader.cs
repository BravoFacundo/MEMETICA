using UnityEngine;

// Sends this computer's runs to the online leaderboard in the build (no Editor needed):
//   On its own   when the game opens, the itch.io runs that couldn't go (no internet then) go now
//   By hand      unlocked (DebugLock: its code on the numpad), Upload Key sends every run not online yet, the
//                exhibition's too: after an exhibition without internet, connect that PC and press it in the Menu
// A note in a corner says how it went. Works in every scene (it makes itself, nothing to place).
public class ScoreUploader : MonoBehaviour
{
    public const KeyCode UploadKey = KeyCode.U;
    const float NoticeTime = 4f;

    private string notice;
    private float noticeUntil;

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
        if (sent > 0) Show($"{sent} score(s) sent online");
    }

    async void Update()
    {
        if (!DebugLock.Unlocked || !Input.GetKeyDown(UploadKey)) return;
        int waiting = ScoreBoard.NotUploaded(true).Count;
        if (waiting == 0) { Show("Every score is online already"); return; }
        Show($"Sending {waiting} score(s)…", 60f);
        var (sent, already) = await OnlineScoreBoard.UploadPending(true);
        int left = ScoreBoard.NotUploaded(true).Count;
        Show(left == 0 ? $"Done: {sent} sent, {already} were there" : $"{sent} sent, {left} left (no internet?)");
    }

    void Show(string text, float seconds = NoticeTime)
    {
        notice = text;
        noticeUntil = Time.unscaledTime + seconds;
        Debug.Log($"[ScoreUploader] {text}");
    }

    void OnGUI()
    {
        if (Time.unscaledTime > noticeUntil || string.IsNullOrEmpty(notice)) return;
        GUI.Label(new Rect(10, Screen.height - 30, 600, 24), notice);
    }
}
