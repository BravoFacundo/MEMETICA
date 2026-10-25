using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Every run that was saved (the score screen, the MemeDex and the menu's screen saver read it): who (a name nobody else
// has: FACU001, FACU002…), when, which build (Mode: expo / itch, their times differ), won or not, how long, and which
// memes, in the order they were unlocked (how many: memes.Count). Best first: more memes, then won, then faster.
// Only runs with a name are saved (no name: no score).
// A file next to the game's other saved data (Application.persistentDataPath/scores.json): it stays between plays on
// that computer (the exhibition PC keeps everyone's).
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
        public List<string> memes = new List<string>();   // MemeDex keys, in unlock order
        public int Count => memes.Count;
    }

    [System.Serializable]
    class Saved { public List<Run> runs = new List<Run>(); }

    static string FilePath => Path.Combine(Application.persistentDataPath, "scores.json");

    // Every run (of that build, if given), best first
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
        runs.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count)
                          : a.won != b.won ? b.won.CompareTo(a.won)
                          : a.seconds.CompareTo(b.seconds));
        return runs;
    }

    // Adds a run. Returns its place (1 = best)
    public static int Save(Run run)
    {
        List<Run> runs = Load((string)null);
        runs.Add(run);
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(new Saved { runs = runs }, true)); }
        catch (System.Exception e) { Debug.LogWarning($"[ScoreBoard] Couldn't save {FilePath}: {e.Message}"); }
        return Load(run.mode).FindIndex(r => r.name == run.name) + 1;
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
