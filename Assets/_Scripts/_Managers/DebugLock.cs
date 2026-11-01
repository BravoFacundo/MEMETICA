using UnityEngine;

// The lock for every debug key (DebugTools' F keys and the language key here), so visitors can't use them by
// accident. Unlocked, Language Key switches English / Spanish in any scene (the Menu too). Each launch starts LOCKED. Typing Code on the numpad (one digit after the other, less than Digit Time
// apart) unlocks them; typing it again locks them. While unlocked a small "DEBUG" shows in a corner (lock it before
// leaving). Works in every scene (it makes itself, nothing to place).
// Everywhere (Editor, Development Build, normal build): locked until the code is typed.
public class DebugLock : MonoBehaviour
{
    // The numpad digits to type (change it here)
    public const string Code = "1079";
    // Seconds allowed between one digit and the next
    const float DigitTime = 2f;
    // Seconds "DEBUG ON / OFF" shows after typing the code
    const float NoticeTime = 2f;

    // Switches English / Spanish (unlocked). Not an F key: those are DebugTools'
    public const KeyCode LanguageKey = KeyCode.L;

    public static bool Unlocked { get; private set; }

    private int typed;
    private float lastDigit, noticeUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        Unlocked = false;   // (each launch, and each Play in the Editor)
        GameObject go = new GameObject("DebugLock");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<DebugLock>();
    }

    void Update()
    {
        if (!Input.anyKeyDown) return;
        if (Unlocked && Input.GetKeyDown(LanguageKey)) SettingsManager.ToggleLanguage();
        int digit = NumpadDigit();
        if (digit < 0) return;
        if (Time.unscaledTime - lastDigit > DigitTime) typed = 0;   // too slow: from the start
        lastDigit = Time.unscaledTime;

        if (Code[typed] - '0' == digit) typed++;
        else typed = Code[0] - '0' == digit ? 1 : 0;

        if (typed < Code.Length) return;
        typed = 0;
        Unlocked = !Unlocked;
        noticeUntil = Time.unscaledTime + NoticeTime;
        Debug.Log("[DebugLock] Debug keys " + (Unlocked ? "UNLOCKED" : "locked") + ".");
    }

    static int NumpadDigit()
    {
        for (int i = 0; i <= 9; i++)
            if (Input.GetKeyDown(KeyCode.Keypad0 + i)) return i;
        return -1;
    }

    void OnGUI()
    {
        bool notice = Time.unscaledTime < noticeUntil;
        if (!Unlocked && !notice) return;
        string text = notice ? (Unlocked ? "DEBUG ON" : "DEBUG OFF") : "DEBUG";
        var style = new GUIStyle(GUI.skin.label) { fontSize = notice ? 18 : 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
        style.normal.textColor = Unlocked ? new Color(1f, 0.85f, 0.2f) : Color.white;
        GUI.Label(new Rect(Screen.width - 210, 6, 200, 30), text, style);
    }
}
