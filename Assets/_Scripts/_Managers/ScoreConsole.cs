using System.Collections.Generic;
using System.Text;
using UnityEngine;

// The scores in the debug console (DebugConsole), in every scene (it makes itself, nothing to place): this computer's
// list (ScoreBoard's file) on the right, numbered as shown, and its keys (unlocked):
//   1  which runs: all / expo / itch / not online yet (this computer) / online (the leaderboard, read only)
//   2  order: best / memes / falls / time / date / name
//   3  rename one (its number, then the name)               4  delete one (its number, then YES)
//   5  test or not (a test never goes online)               6  send every run not online yet (the exhibition's too)
//   7  a backup copy of the file                            PgUp / PgDn  the list's pages
// Only the local file changes: what is already online stays (fix it in Unity Cloud's dashboard).
public class ScoreConsole : MonoBehaviour
{
    enum Filter { All, Expo, Itch, Pending, Online }
    enum Sort { Best, Memes, Falls, Time, Date, Name }

    const int PageSize = 20;

    private Filter filter;
    private Sort sort;
    private int page;
    private bool uploading, reading;
    private List<ScoreBoard.Run> online = new List<ScoreBoard.Run>();   // (read when Online is chosen)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        GameObject go = new GameObject("ScoreConsole");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<ScoreConsole>();
    }

    void OnEnable()
    {
        DebugConsole.AddSection(this, "SCORES  (this computer)", Lines, 20);
        DebugConsole.AddPanel(this, "", ListText, 20);
    }

    void OnDisable() => DebugConsole.RemoveSections(this);

    void Update()
    {
        if (DebugConsole.Pressed(1)) { filter = Next(filter); page = 0; if (filter == Filter.Online) ReadOnline(); }
        if (DebugConsole.Pressed(2)) { sort = Next(sort); page = 0; }
        if (DebugConsole.Pressed(3)) AskNumber("Rename #", run =>
            DebugConsole.Ask($"New name for {run.name}", name =>
                DebugConsole.Log(ScoreBoard.Rename(run.name, name) ? $"{run.name} is now {name}" + (run.uploaded ? " (online it keeps its old name)" : "")
                                                                   : $"Couldn't rename {run.name}: empty or taken name")));
        if (DebugConsole.Pressed(4)) AskNumber("Delete #", run =>
            DebugConsole.Ask($"Delete {run.name}? Type YES", answer =>
            {
                if (answer.ToUpperInvariant() != "YES") { DebugConsole.Log($"{run.name} kept"); return; }
                DebugConsole.Log(ScoreBoard.Delete(run.name) ? $"{run.name} deleted" + (run.uploaded ? " (online it stays)" : "") : $"No {run.name}");
            }));
        if (DebugConsole.Pressed(5)) AskNumber("Test on / off #", run =>
            DebugConsole.Log(ScoreBoard.ToggleTest(run.name) ? $"{run.name} is a test now (it won't go online)" : $"{run.name} isn't a test anymore"));
        if (DebugConsole.Pressed(6)) Upload();
        if (DebugConsole.Pressed(7))
        {
            string path = ScoreBoard.Backup();
            DebugConsole.Log(path != null ? "Backup: " + path : "No scores file yet");
        }
        if (DebugConsole.Pressed(KeyCode.PageDown, "PgDn")) page++;
        if (DebugConsole.Pressed(KeyCode.PageUp, "PgUp")) page = Mathf.Max(page - 1, 0);
    }

    static T Next<T>(T value) where T : System.Enum
    {
        var values = (T[])System.Enum.GetValues(typeof(T));
        return values[(System.Array.IndexOf(values, value) + 1) % values.Length];
    }

    // A run by its number in the list as shown, then Then
    void AskNumber(string prompt, System.Action<ScoreBoard.Run> then)
    {
        if (filter == Filter.Online) { DebugConsole.Log("Online runs are read only (change them in Unity Cloud's dashboard)"); return; }
        DebugConsole.Ask(prompt, typed =>
        {
            List<ScoreBoard.Run> runs = Shown();
            if (!int.TryParse(typed, out int n) || n < 1 || n > runs.Count) { DebugConsole.Log($"No #{typed} in the list"); return; }
            then(runs[n - 1]);
        }, true);
    }

    async void ReadOnline()
    {
        if (reading) return;
        reading = true;
        DebugConsole.Log($"Reading the online leaderboard ({OnlineScoreBoard.Leaderboard})…");
        online = await OnlineScoreBoard.Fetch(OnlineScoreBoard.Leaderboard, 200);
        reading = false;
        DebugConsole.Log(online.Count > 0 ? $"{online.Count} online run(s)" : "No online runs (or no internet)");
    }

    async void Upload()
    {
        if (uploading) { DebugConsole.Log("Already sending"); return; }
        int waiting = ScoreBoard.NotUploaded(true).Count;
        if (waiting == 0) { DebugConsole.Log("Every score is online already"); return; }
        uploading = true;
        DebugConsole.Log($"Sending {waiting} score(s)…");
        var (sent, already) = await OnlineScoreBoard.UploadPending(true, (done, of, name) => DebugConsole.Log($"Sent {done}/{of}: {name}"));
        uploading = false;
        int left = ScoreBoard.NotUploaded(true).Count;
        DebugConsole.Log(left == 0 ? $"Done: {sent} sent, {already} were there" : $"{sent} sent, {left} left (no internet?)");
    }

    //---- The list

    // The runs as the list shows them (its numbers)
    List<ScoreBoard.Run> Shown()
    {
        List<ScoreBoard.Run> runs = filter == Filter.Online ? new List<ScoreBoard.Run>(online) : ScoreBoard.All();
        string expo = GameManager.ScoreMode.Expo.ToString(), itch = GameManager.ScoreMode.Itch.ToString();
        if (filter == Filter.Expo) runs.RemoveAll(r => r.mode != expo);
        else if (filter == Filter.Itch) runs.RemoveAll(r => r.mode != itch);
        else if (filter == Filter.Pending) runs.RemoveAll(r => r.uploaded || r.test);
        switch (sort)
        {
            case Sort.Memes: return ScoreBoard.Sorted(runs, ScoreBoard.Order.Memes);
            case Sort.Falls: return ScoreBoard.Sorted(runs, ScoreBoard.Order.Falls);
            case Sort.Time: return ScoreBoard.Sorted(runs, ScoreBoard.Order.Time);
            case Sort.Date: runs.Sort((a, b) => string.CompareOrdinal(b.date, a.date)); return runs;   // newest first
            case Sort.Name: runs.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase)); return runs;
            default: return ScoreBoard.Sorted(runs, ScoreBoard.Order.Best);
        }
    }

    List<DebugConsole.Line> Lines() => new List<DebugConsole.Line>
    {
        new DebugConsole.Line("1", "show: " + filter),
        new DebugConsole.Line("2", "order: " + sort),
        new DebugConsole.Line("3", "rename one"),
        new DebugConsole.Line("4", "delete one"),
        new DebugConsole.Line("5", "test on / off  (a test never goes online)"),
        new DebugConsole.Line("6", uploading ? "sending…" : "send every run not online yet  (" + ScoreBoard.NotUploaded(true).Count + ")"),
        new DebugConsole.Line("7", "backup copy of the file"),
        new DebugConsole.Line("PgDn", "list: next page"),
        new DebugConsole.Line("PgUp", "list: previous page"),
    };

    string ListText()
    {
        List<ScoreBoard.Run> runs = Shown();
        int pages = Mathf.Max(1, Mathf.CeilToInt(runs.Count / (float)PageSize));
        page = Mathf.Clamp(page, 0, pages - 1);
        var sb = new StringBuilder();
        string source = filter == Filter.Online ? (reading ? "ONLINE (reading…)" : "ONLINE") : filter.ToString().ToUpperInvariant() + " (this computer)";
        sb.Append($"{source} RUNS  ·  {sort}  ·  {runs.Count}  ·  page {page + 1}/{pages}\n");
        sb.Append("#<pos=6%>NAME<pos=46%>MEMES<pos=60%>TIME<pos=72%>FALLS<pos=82%>BUILD\n");
        for (int i = page * PageSize; i < Mathf.Min(runs.Count, (page + 1) * PageSize); i++)
        {
            ScoreBoard.Run r = runs[i];
            string flags = (r.uploaded && filter != Filter.Online ? " online" : "") + (r.test ? " TEST" : "") + (r.recovered ? " rec" : "");
            sb.Append($"{i + 1}<pos=6%><noparse>{r.name}</noparse><pos=46%>{r.Count}/{r.total}<pos=60%>{GameManager.Format(r.seconds)}" +
                      $"<pos=72%>{(r.falls < 0 ? "?" : r.falls.ToString())}<pos=82%>{r.mode}{flags}\n");
        }
        if (runs.Count == 0) sb.Append("(none)\n");
        return sb.ToString();
    }
}
