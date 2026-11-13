using UnityEngine;

// The lock for every debug key (DebugTools' F keys, the console's number keys), so visitors can't use them by
// accident. Each launch starts LOCKED, everywhere (Editor, Development Build, normal build). The console (DebugConsole)
// asks for Code when F1 is pressed locked, and locks it again with its 0 key.
public static class DebugLock
{
    // The password (change it here)
    public const string Code = "1079";

    public static bool Unlocked { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Reset() => Unlocked = false;   // (each launch, and each Play in the Editor)

    // Right: unlocked
    public static bool TryUnlock(string typed)
    {
        if (typed != Code) { Debug.Log("[DebugLock] Wrong password."); return false; }
        Unlocked = true;
        Debug.Log("[DebugLock] Debug keys UNLOCKED.");
        return true;
    }

    public static void Lock()
    {
        Unlocked = false;
        Debug.Log("[DebugLock] Debug keys locked.");
    }
}
