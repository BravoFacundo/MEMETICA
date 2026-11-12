using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Every run that was saved (the score screen, the MemeDex and the menu's screen saver read it): who (a name nobody else
// has: FACU001, FACU002…), when, which build (Mode: expo / itch, their times differ), won or not, how long, how many
// falls into the void (RunStats: the portal's way back doesn't count) and which memes, in the order they were unlocked
// (how many: memes.Count, of Total). Only runs with a name are saved (no name: no score).
// Orders (the score screen's filter, each either way; the categories they make):
//   Best      more memes, then less time (default)
//   Memes     more memes first: Perfect Completion (all of them) at the top
//   Falls     fewer falls first: Flawless (0 falls) at the top, then more memes, then less time
//   Time      less time first: Speedrun, then more memes
// A file next to the game's other saved data (Application.persistentDataPath/scores.json): it stays between plays on
// that computer (the exhibition PC keeps everyone's). Itch: each run also goes to the online leaderboard
// (OnlineScoreBoard); not sent (no internet), it goes next time the game opens online (ScoreUploader). Expo: only here,
// until uploaded by hand (ScoreUploader's key in the build, Memetica > Scores in the Editor). Uploaded: marked so.
// Medals (to decide; for now): Gold, all the memes (Perfect); Silver, no falls (Flawless).
public static class ScoreBoard
{
    [System.Serializable]
    public class Run
    {
        public string name;
        public string date;
        public string mode;      // GameManager's Score Mode: runs of different builds don't mix
        public bool won;
        public float seconds;    // played
        public int falls;        // into the void (RunStats)
        public int total;        // memes there were to unlock (the MemeDex then)
        public List<string> memes = new List<string>();   // MemeDex keys, in unlock order
        public int memeCount;    // how many, when the list isn't known (recovered runs)
        public bool recovered;   // put back by hand from notes (no falls, its time: the whole countdown)
        public string notes;     // anything else known about it
        public bool uploaded;    // already in the online leaderboard
        public int Count => memes.Count > 0 ? memes.Count : memeCount;

        public bool Flawless => falls == 0;   // (-1: not known)
        public bool Perfect => total > 0 && Count >= total;
        public Medal Medal => Perfect ? Medal.Gold : Flawless ? Medal.Silver : Medal.None;
    }

    public enum Medal { None, Silver, Gold }

    public enum Order { Best, Memes, Falls, Time }

    // These runs in that order (Ascending: the other way round), ties broken by the rest
    public static List<Run> Sorted(List<Run> runs, Order by, bool ascending = false)
    {
        var sorted = new List<Run>(runs);
        System.Comparison<Run> best = (a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : a.seconds.CompareTo(b.seconds);
        System.Comparison<Run> compare = best;
        if (by == Order.Falls) compare = (a, b) => a.falls != b.falls ? a.falls.CompareTo(b.falls) : best(a, b);
        else if (by == Order.Time) compare = (a, b) => a.seconds != b.seconds ? a.seconds.CompareTo(b.seconds) : b.Count.CompareTo(a.Count);
        if (ascending) { System.Comparison<Run> forward = compare; compare = (a, b) => forward(b, a); }
        sorted.Sort(compare);
        return sorted;
    }

    [System.Serializable]
    class Saved { public List<Run> runs = new List<Run>(); }

    public static string FilePath => Path.Combine(Application.persistentDataPath, "scores.json");

    // The runs of any scores file (a copy from the exhibition PC, recovered ones…)
    public static List<Run> ReadFile(string path)
    {
        try { return JsonUtility.FromJson<Saved>(File.ReadAllText(path)).runs ?? new List<Run>(); }
        catch (System.Exception e) { Debug.LogWarning($"[ScoreBoard] Couldn't read {path}: {e.Message}"); return new List<Run>(); }
    }

    // Every run (of that build, if given), best first (Order.Best)
    public static List<Run> Load(GameManager.ScoreMode? mode = null) => Load(mode.HasValue ? mode.Value.ToString() : null);

    static List<Run> Load(string mode)
    {
        var runs = new List<Run>();
        try
        {
            if (File.Exists(FilePath)) runs = JsonUtility.FromJson<Saved>(File.ReadAllText(FilePath)).runs ?? runs;
        }
        catch (System.Exception e) { Debug.LogWarning($"[ScoreBoard] Couldn't read {FilePath}: {e.Message}"); }
        if (!string.IsNullOrEmpty(mode)) runs.RemoveAll(r => r.mode != mode);
        return Sorted(runs, Order.Best);
    }

    // Adds a run (only with a name: no name, no score). Returns its place (1 = best). Itch: online too
    public static int Save(Run run)
    {
        if (run == null || string.IsNullOrWhiteSpace(run.name)) return 0;
        List<Run> runs = Load((string)null);
        runs.Add(run);
        Write(runs);
        if (run.mode == GameManager.ScoreMode.Itch.ToString()) _ = OnlineScoreBoard.UploadPending(false);   // (online now: it goes)
        return Load(run.mode).FindIndex(r => r.name == run.name) + 1;
    }

    static void Write(List<Run> runs)
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(new Saved { runs = runs }, true)); }
        catch (System.Exception e) { Debug.LogWarning($"[ScoreBoard] Couldn't save {FilePath}: {e.Message}"); }
    }

    // The runs not online yet (with a name). Expo too, or only Itch's
    public static List<Run> NotUploaded(bool expoToo) =>
        Load((string)null).FindAll(r => !r.uploaded && !string.IsNullOrWhiteSpace(r.name) && (expoToo || r.mode == GameManager.ScoreMode.Itch.ToString()));

    // These are online now (by name)
    public static void MarkUploaded(ICollection<string> names)
    {
        if (names.Count == 0) return;
        List<Run> runs = Load((string)null);
        foreach (Run r in runs) if (names.Contains(r.name)) r.uploaded = true;
        Write(runs);
    }

    // Someone already has it (names are unique: the name screen says it's taken)
    public static bool IsTaken(string name) => Load((string)null).Exists(r => string.Equals(r.name, name, System.StringComparison.OrdinalIgnoreCase));

    // The first free one with a number: FACU → FACU001, FACU002…
    public static string NextFree(string name)
    {
        for (int n = 1; n < 1000; n++)
        {
            string candidate = $"{name}{n:000}";
            if (!IsTaken(candidate)) return candidate;
        }
        return name + System.DateTime.Now.Ticks;
    }

    //---- Everyone together (the MemeDex's global tab)

    // Every meme anyone ever unlocked, with how many runs did (and who first)
    public static Dictionary<string, (int runs, string first)> Global(GameManager.ScoreMode? mode = null)
    {
        var found = new Dictionary<string, (int runs, string first)>();
        List<Run> runs = Load(mode);
        runs.Sort((a, b) => string.CompareOrdinal(a.date, b.date));   // (dates as yyyy-MM-dd HH:mm:ss sort as text)   // oldest first: the first one to find it
        foreach (Run run in runs)
            foreach (string meme in new HashSet<string>(run.memes))
                found[meme] = found.TryGetValue(meme, out var seen) ? (seen.runs + 1, seen.first) : (1, run.name);
        return found;
    }
}
