using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Memetica > Scores (in Play mode: the online services need it running):
//   Print Online Top 20          the online leaderboard (OnlineScoreBoard.Leaderboard) in the Console
//   Upload Scores File To Online a scores.json (the exhibition PC's, the recovered runs) into it, each run as its own
//                                player; runs already there (same name) are skipped
//   Back Up And Reset Local      (any time) this computer's scores.json → scores_backup_<date>.json, and a new empty
//                                one: before an exhibition day, the trophies from zero
public static class ScoresTool
{
    const string Menu = "Memetica/Scores/";

    static string Board() => OnlineScoreBoard.Leaderboard;

    static bool InPlay()
    {
        if (Application.isPlaying) return true;
        Debug.LogWarning("[Scores] Press Play first (the online services run only in Play mode).");
        return false;
    }

    [MenuItem(Menu + "Print Online Top 20")]
    static async void Print()
    {
        if (!InPlay()) return;
        string board = Board();
        List<ScoreBoard.Run> runs = await OnlineScoreBoard.Fetch(board, 20);
        var text = new System.Text.StringBuilder($"[Scores] '{board}': {runs.Count} run(s)\n");
        for (int i = 0; i < runs.Count; i++)
        {
            ScoreBoard.Run r = runs[i];
            text.AppendLine($"  {i + 1}. {r.name}  {r.Count}/{r.total} memes  {GameManager.Format(r.seconds)}  " +
                            $"falls {(r.falls < 0 ? "?" : r.falls.ToString())}  {(r.won ? "won" : "lost")}  {r.Medal}" +
                            $"{(r.recovered ? "  (recovered)" : "")}  {r.date}");
        }
        Debug.Log(text.ToString());
    }

    [MenuItem(Menu + "Upload Scores File To Online…")]
    static async void Upload()
    {
        if (!InPlay()) return;
        string path = EditorUtility.OpenFilePanel("A scores file (scores.json)", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;
        List<ScoreBoard.Run> runs = ScoreBoard.ReadFile(path);
        string board = Board();
        var there = new HashSet<string>();
        foreach (ScoreBoard.Run r in await OnlineScoreBoard.Fetch(board, 1000)) there.Add(r.name);
        int sent = 0, skipped = 0;
        foreach (ScoreBoard.Run run in runs)
        {
            if (string.IsNullOrWhiteSpace(run.name) || there.Contains(run.name)) { skipped++; continue; }
            if (await OnlineScoreBoard.Submit(run, board)) { sent++; there.Add(run.name); }
        }
        Debug.Log($"[Scores] '{Path.GetFileName(path)}' → '{board}': {sent} sent, {skipped} skipped (no name, or already there).");
    }

    [MenuItem(Menu + "Back Up And Reset Local")]
    static void ResetLocal()
    {
        string path = ScoreBoard.FilePath;
        if (!File.Exists(path)) { Debug.Log($"[Scores] No local scores yet ({path})."); return; }
        if (!EditorUtility.DisplayDialog("Reset local scores", "This computer's scores are kept as a backup and a new empty list starts. Go on?", "Reset", "Cancel")) return;
        string backup = Path.Combine(Path.GetDirectoryName(path), $"scores_backup_{System.DateTime.Now:yyyy-MM-dd_HH-mm}.json");
        File.Move(path, backup);
        Debug.Log($"[Scores] Local scores kept as {backup}; a new list starts.");
    }
}
