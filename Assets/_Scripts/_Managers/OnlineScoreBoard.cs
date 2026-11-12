using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

// The online scores (Unity Gaming Services > Leaderboards: the itch.io build). Each run is an entry of its own: before
// sending one it signs in as a new anonymous player (one computer, many players), named as the run. Its score is one
// number for the "Best" order (more memes, then less time); the rest of the run (falls, time, memes, won…) travels
// with it as its metadata, so the game can show any order and medal (ScoreBoard).
// The leaderboard: Leaderboard below (its ID in Unity Cloud: Highest to lowest, Best score, no tiers). A new version
// from zero: a new leaderboard there (itch_v2…) and its ID here; the old one stays.
// UploadPending sends this computer's runs not online yet (ScoreBoard.Save, ScoreUploader); Memetica > Scores uploads a
// scores file (the exhibition's copy, recovered ones) and prints the table. Without internet it only logs it (the runs
// stay in the local file, not marked: they go next time).
public static class OnlineScoreBoard
{
    public const string Leaderboard = "itch_v1";

    // One call to the service at a time (reading while a run signs in as its player failed: "already signing in")
    static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

    // Best order as one number: memes first, then fewer seconds (up to 999)
    public static double Score(ScoreBoard.Run run) => run.Count * 1000 + (999 - Mathf.Clamp(Mathf.RoundToInt(run.seconds), 0, 999));

    static async Task<bool> Ready()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            return UnityServices.State == ServicesInitializationState.Initialized;
        }
        catch (System.Exception e) { Debug.LogWarning($"[OnlineScoreBoard] Services not available: {e.Message}"); return false; }
    }

    // Sends one run as a new player. True: it is in
    public static async Task<bool> Submit(ScoreBoard.Run run, string leaderboard)
    {
        if (run == null || string.IsNullOrWhiteSpace(run.name) || string.IsNullOrEmpty(leaderboard)) return false;
        if (!await Ready()) return false;
        await gate.WaitAsync();
        try
        {
            IAuthenticationService auth = AuthenticationService.Instance;
            if (auth.IsSignedIn) auth.SignOut();
            auth.ClearSessionToken();   // (a new player: not the last one again)
            await auth.SignInAnonymouslyAsync();
            await auth.UpdatePlayerNameAsync(PlayerName(run.name));
            await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboard, Score(run), new AddPlayerScoreOptions { Metadata = Metadata(run) });
            Debug.Log($"[OnlineScoreBoard] '{run.name}' sent to '{leaderboard}': {run.Count} memes, {run.seconds:0} s (score {Score(run)}).");
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[OnlineScoreBoard] Couldn't send '{run.name}' to '{leaderboard}': {e.Message}"); return false; }
        finally { gate.Release(); }
    }

    // This computer's runs not online yet (Expo too: by hand), one by one; the ones already there (same name) are only
    // marked. Returns how many went, and how many were already there
    static bool uploading;
    public static async Task<(int sent, int already)> UploadPending(bool expoToo)
    {
        if (uploading) return (0, 0);
        uploading = true;
        int sent = 0, already = 0;
        try
        {
            List<ScoreBoard.Run> pending = ScoreBoard.NotUploaded(expoToo);
            if (pending.Count == 0) return (0, 0);
            var there = new HashSet<string>();
            foreach (ScoreBoard.Run r in await Fetch(Leaderboard, 1000)) there.Add(r.name);
            var done = new HashSet<string>();
            foreach (ScoreBoard.Run run in pending)
            {
                if (there.Contains(run.name)) { done.Add(run.name); already++; continue; }
                if (!await Submit(run, Leaderboard)) break;   // (no internet: the rest next time)
                done.Add(run.name);
                sent++;
            }
            ScoreBoard.MarkUploaded(done);
        }
        finally { uploading = false; }
        return (sent, already);
    }

    // The runs in the leaderboard, best first (up to Limit)
    public static async Task<List<ScoreBoard.Run>> Fetch(string leaderboard, int limit = 100)
    {
        var runs = new List<ScoreBoard.Run>();
        if (!await Ready()) return runs;
        await gate.WaitAsync();
        try
        {
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            var page = await LeaderboardsService.Instance.GetScoresAsync(leaderboard, new GetScoresOptions { Limit = limit, IncludeMetadata = true });
            foreach (var entry in page.Results)
            {
                ScoreBoard.Run run = !string.IsNullOrEmpty(entry.Metadata) ? JsonUtility.FromJson<ScoreBoard.Run>(entry.Metadata) : null;
                if (run == null) run = new ScoreBoard.Run { name = entry.PlayerName };
                runs.Add(run);
            }
        }
        catch (System.Exception e) { Debug.LogWarning($"[OnlineScoreBoard] Couldn't read '{leaderboard}': {e.Message}"); }
        finally { gate.Release(); }
        return runs;
    }

    // The run as its metadata (the meme list only if it fits: the count always travels)
    static Dictionary<string, object> Metadata(ScoreBoard.Run run)
    {
        var data = new Dictionary<string, object>
        {
            { "name", run.name }, { "date", run.date ?? "" }, { "mode", run.mode ?? "" }, { "won", run.won },
            { "seconds", run.seconds }, { "falls", run.falls }, { "total", run.total }, { "memeCount", run.Count },
            { "recovered", run.recovered }, { "notes", run.notes ?? "" },
        };
        int size = 0;
        foreach (string key in run.memes) size += key.Length + 3;
        if (size < 700) data["memes"] = run.memes;
        else Debug.Log($"[OnlineScoreBoard] '{run.name}': its meme list is long, only its count goes ({run.Count}).");
        return data;
    }

    // A player name the service takes: no spaces, up to 50 characters
    static string PlayerName(string name)
    {
        string clean = name.Replace(" ", "");
        return clean.Length > 50 ? clean.Substring(0, 50) : clean;
    }
}
