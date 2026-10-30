using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// The MemeDex: every meme you unlock, in the order you did (for the score). Collect them all and back them up to win.
// Each axis (its MemeData) has 7, the standard:
//   Base     its white pedestal at the end of its course: the sweep ends on the base meme (Pedestal)
//   Fall     its Fall change shows (you fell with it)
//   Leave    its Leave change shows (you left it behind and came back, or it came to you: MemeRecall)
//   Move 1-4 its Move changes, in order: each main island of its course you carry it onto (MemeChanger)
// Optional memes (one each, at the end of the dex): the meme window at the end of an optional course (DexCollectible).
// An image an axis doesn't have (no Leave yet, 3 Moves) doesn't count. Once all are in: On Complete (the backup popup,
// later); meanwhile Win When Complete wins right away. Goes in Managers > MemeDex.
public class MemeDex : MonoBehaviour
{
    public static MemeDex Instance { get; private set; }

    public enum Kind { Base, Fall, Leave, Move, Optional }

    [System.Serializable]
    public struct Entry
    {
        public MemeData meme;
        public Kind kind;
        public int move;   // 1-4 (Move)
        public float time; // seconds into the run when unlocked (GameManager)
        public string Key => $"{(meme != null ? meme.name : "?")}/{kind}{(kind == Kind.Move ? move.ToString() : "")}";
    }

    [Section("Catalog")]
    [Tooltip("The axes, in the dex's order (7 each: Base, Fall, Leave, Move 1-4)")]
    public List<MemeData> axes = new List<MemeData>();
    [Tooltip("Optional memes, one each, at the end of the dex")]
    public List<MemeData> optional = new List<MemeData>();
    [Tooltip("Moves counted per axis")]
    public int movesPerAxis = 4;

    [Section("Complete")]
    [Tooltip("All of them in: win right away (until the backup popup exists: then turn it off and use On Complete)")]
    public bool winWhenComplete = true;
    public UnityEvent onComplete;

    [Tooltip("What you have, as it goes (while playing)")]
    [ReadOnlyField, TextArea(2, 6)] public string progress;

    // Something new (the display, a popup)
    public static event System.Action<Entry> Unlocked;

    public List<Entry> Order { get; } = new List<Entry>();   // as unlocked
    public int Count => Order.Count;
    public int Total { get; private set; }
    public bool Complete => Total > 0 && Count >= Total;

    private readonly HashSet<string> have = new HashSet<string>();
    private bool completed;

    void Awake()
    {
        Instance = this;
        Total = 0;
        foreach (Entry entry in All()) Total++;
        progress = $"0 / {Total}";
    }

    // Every entry of the dex, in its order
    public IEnumerable<Entry> All()
    {
        foreach (MemeData meme in axes)
        {
            if (meme == null) continue;
            yield return new Entry { meme = meme, kind = Kind.Base };
            if (meme.HasFall) yield return new Entry { meme = meme, kind = Kind.Fall };
            if (meme.HasLeave) yield return new Entry { meme = meme, kind = Kind.Leave };
            int moves = Mathf.Min(movesPerAxis, meme.variants.FindAll(MemeImage.Exists).Count);
            for (int m = 1; m <= moves; m++) yield return new Entry { meme = meme, kind = Kind.Move, move = m };
        }
        foreach (MemeData meme in optional)
            if (meme != null) yield return new Entry { meme = meme, kind = Kind.Optional };
    }

    public bool Has(Entry entry) => have.Contains(entry.Key);

    //---- Reported by the game

    // An image of a meme was shown as a change (MemeChanger) or as its result (Pedestal: the base)
    public static void Report(MemeData meme, MemeImage image)
    {
        if (Instance == null || meme == null || image == null) return;
        if (image == meme.baseImage) Instance.Unlock(new Entry { meme = meme, kind = Instance.optional.Contains(meme) ? Kind.Optional : Kind.Base });
        else if (image == meme.fall) Instance.Unlock(new Entry { meme = meme, kind = Kind.Fall });
        else if (image == meme.leave) Instance.Unlock(new Entry { meme = meme, kind = Kind.Leave });
        else
        {
            int move = meme.variants.IndexOf(image) + 1;
            if (move > Instance.movesPerAxis) return;   // (a meme with more Moves than the dex counts: those don't count)
            if (move > 0) Instance.Unlock(new Entry { meme = meme, kind = Kind.Move, move = move });
        }
    }

    public void Unlock(Entry entry)
    {
        if (entry.meme == null || have.Contains(entry.Key)) return;
        if (!InCatalog(entry)) { Debug.LogWarning($"[MemeDex] '{entry.Key}' isn't in the catalog (add its MemeData to Axes or Optional).", this); return; }
        entry.time = GameManager.Instance != null ? GameManager.Instance.Elapsed : Time.timeSinceLevelLoad;
        have.Add(entry.Key);
        Order.Add(entry);
        progress = $"{Count} / {Total}   last: {entry.Key}";
        Debug.Log($"[MemeDex] {entry.Key} ({Count}/{Total})", this);
        Unlocked?.Invoke(entry);

        if (Complete && !completed)
        {
            completed = true;
            onComplete.Invoke();
            if (winWhenComplete && GameManager.Instance != null) GameManager.Instance.Win();
        }
    }

    bool InCatalog(Entry entry)
    {
        foreach (Entry e in All()) if (e.Key == entry.Key) return true;
        return false;
    }

    //---- The score

    // This run as a score (name: typed at the end, not taken: ScoreBoard.IsTaken / NextFree)
    public ScoreBoard.Run CurrentRun(string playerName)
    {
        var run = new ScoreBoard.Run
        {
            name = playerName,
            date = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            mode = GameManager.Instance != null ? GameManager.Instance.scoreMode.ToString() : "",
            won = GameManager.Instance != null && GameManager.Instance.Current == GameManager.State.Won,
            seconds = GameManager.Instance != null ? GameManager.Instance.Elapsed : 0f,
            falls = RunStats.Falls,
            total = Total,
        };
        foreach (Entry entry in Order) run.memes.Add(entry.Key);
        return run;
    }

    // DebugTools F12: what you have, in order
    public void LogProgress()
    {
        var text = new System.Text.StringBuilder($"[MemeDex] {Count} / {Total}\n");
        for (int i = 0; i < Order.Count; i++) text.AppendLine($"  {i + 1}. {Order[i].Key}  ({GameManager.Format(Order[i].time)})");
        foreach (Entry entry in All()) if (!Has(entry)) text.AppendLine($"  - {entry.Key}");
        Debug.Log(text.ToString(), this);
    }
}
