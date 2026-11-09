using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Every popup of the game goes through here (before: TutorialManager faded them, ModalPopup stopped the game, and
// GameManager, CourseGuide and Elevator each decided how to show theirs). Goes in Managers > PopupManager.
// A popup is any panel. The children of Popup Canvas (the HUD's tutorial panel) fade in and out; any other panel is
// just turned on and off. Who asks doesn't need to know which: they all call
//   PopupManager.Show(popup)          shown until Hide (PopupTrigger while you are inside, the elevator's ride popups)
//   PopupManager.Show(popup, true)    closable: it stays until its X (WindowButton → On Click: PopupManager.Close)
//   PopupManager.Hide(popup)          one asker less: it fades out once nobody asks for it
//   PopupManager.ShowFor(popup, s)    shown for some seconds (CourseGuide's wrong way); asked again, the time restarts
//   PopupManager.ShowModal(popup)     stops the game until you click (the virus alert, MemeDelivery, Modal triggers)
// Several can be visible at once (each in its own place); two asking for the same one keep it up until both let go.
// While the game is paused the whole Popup Canvas hides (the pause window owns the screen).
// Modal: time frozen (the elevator, the platforms, the countdown…), you can't move or look, and any click, key or
// gamepad button (not the pause keys) closes it after Min Time, then everything goes on. One at a time.
// A scene without a PopupManager still shows them (one is made, without Popup Canvas: every panel on and off).
// Sound: each time a popup appears (not while it stays up, or asked again) one of Appear Sounds (a SoundSet: at random,
// each as it is or disguised, a little higher or lower each time); one that stops the game, one of Modal Sounds.
public class PopupManager : MonoBehaviour
{
    private static PopupManager instance;
    public static PopupManager Instance
    {
        get
        {
            if (instance == null) instance = FindObjectOfType<PopupManager>();
            if (instance == null && Application.isPlaying) instance = new GameObject("PopupManager").AddComponent<PopupManager>();
            return instance;
        }
    }

    [Header("References")]
    [Tooltip("The panel whose children are the popups that fade (Tutorial inside Canvas_HUD)")]
    public Transform popupCanvas;

    [Header("Pause")]
    [Tooltip("Hide all popups while the game is paused")]
    public bool hideWhilePaused = true;

    [Header("Fade")]
    [Tooltip("Seconds to fade a popup in or out")]
    public float fadeTime = 0.25f;

    [Header("Sound")]
    [Tooltip("When a popup appears: one of these, at random. Each one as it is or Disguised (changed to sound like its own)")]
    public SoundSet appearSounds = new SoundSet();
    [Tooltip("When a popup that stops the game appears (the virus alert, 'Click to continue'): a sound of its own, so you " +
             "hear the difference. Empty: Appear Sounds")]
    public SoundSet modalSounds = new SoundSet();

    // A modal popup is open now (the game is stopped for it): the cursor shows, grabbing waits
    public static bool ModalOpen { get; private set; }

    private readonly Dictionary<GameObject, int> requests = new Dictionary<GameObject, int>();
    private readonly Dictionary<GameObject, CanvasGroup> popups = new Dictionary<GameObject, CanvasGroup>();
    private readonly Dictionary<GameObject, Coroutine> timed = new Dictionary<GameObject, Coroutine>();
    private readonly List<GameObject> fading = new List<GameObject>();   // (reused every frame)
    private CanvasGroup canvasGroup;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        if (popupCanvas == null) return;   // (only on and off)

        // One CanvasGroup on the whole panel: hides everything during pause. Clicks: only the closable popups (their own group)
        canvasGroup = popupCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = popupCanvas.gameObject.AddComponent<CanvasGroup>();
        foreach (Transform child in popupCanvas)
        {
            GroupOf(child.gameObject).alpha = 0f;
            child.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        ModalOpen = false;
    }

    //---- Asking (from anywhere)

    public static void Show(GameObject popup, bool closable = false) { if (popup != null) Instance.Request(popup, closable); }
    public static void Hide(GameObject popup) { if (popup != null && instance != null) instance.Release(popup, false); }

    public static void ShowFor(GameObject popup, float seconds)
    {
        if (popup == null) return;
        PopupManager m = Instance;
        if (m.timed.TryGetValue(popup, out Coroutine running) && running != null) m.StopCoroutine(running);   // (still up: the time restarts)
        else m.Request(popup, false);
        m.timed[popup] = m.StartCoroutine(m.HideAfter(popup, seconds));
    }

    // Stops the game until it is clicked; Closed is called once it is gone and the game runs again
    public static void ShowModal(GameObject popup, System.Action closed = null, float minTime = 0.6f)
    {
        if (popup == null) { closed?.Invoke(); return; }
        Instance.StartCoroutine(Instance.Modal(popup, closed, minTime));
    }

    // Its X button (WindowButton → On Click, with the popup): it goes away whoever asked for it
    public void Close(GameObject popup)
    {
        if (popup != null) Release(popup, true);
    }

    //---- Showing

    bool Fades(GameObject popup) => popupCanvas != null && popup.transform.IsChildOf(popupCanvas);

    void Request(GameObject popup, bool closable, bool modal = false)
    {
        if (!Fades(popup))
        {
            if (!popup.activeSelf) PlayAppear(modal);
            popup.SetActive(true);
            return;
        }
        if (Count(popup) == 0) PlayAppear(modal);   // (it wasn't asked for: it appears now, or comes back while fading out)
        requests[popup] = Count(popup) + 1;
        CanvasGroup g = GroupOf(popup);
        g.blocksRaycasts = g.interactable = closable;
        popup.SetActive(true);
    }

    void Release(GameObject popup, bool all)
    {
        if (!Fades(popup)) { popup.SetActive(false); return; }
        requests[popup] = all ? 0 : Mathf.Max(0, Count(popup) - 1);
    }

    IEnumerator HideAfter(GameObject popup, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        timed.Remove(popup);
        Release(popup, false);
    }

    void Update()
    {
        if (canvasGroup == null) return;
        bool hidden = hideWhilePaused && UIManager.GameIsPaused;
        canvasGroup.alpha = hidden ? 0f : 1f;
        canvasGroup.blocksRaycasts = canvasGroup.interactable = !hidden;

        float step = Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f);
        fading.Clear();
        fading.AddRange(popups.Keys);   // (gone through as a copy)
        foreach (GameObject popup in fading)
        {
            if (popup == null || !popup.activeSelf) continue;
            CanvasGroup g = popups[popup];
            float target = Count(popup) > 0 ? 1f : 0f;
            g.alpha = Mathf.MoveTowards(g.alpha, target, step);
            if (target == 0f && g.alpha == 0f) popup.SetActive(false);   // fully faded out
        }
    }

    int Count(GameObject popup) => requests.TryGetValue(popup, out int n) ? n : 0;

    CanvasGroup GroupOf(GameObject popup)
    {
        if (popups.TryGetValue(popup, out CanvasGroup g)) return g;
        g = popup.GetComponent<CanvasGroup>();
        if (g == null) g = popup.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = g.interactable = false;
        popups[popup] = g;
        return g;
    }

    void PlayAppear(bool modal) => (modal && !modalSounds.IsEmpty ? modalSounds : appearSounds).Play(gameObject);

    //---- Modal

    IEnumerator Modal(GameObject popup, System.Action closed, float minTime)
    {
        while (ModalOpen) yield return null;   // (another one first)
        ModalOpen = true;
        PlayerMove player = FindObjectOfType<PlayerMove>();
        PlayerLook look = FindObjectOfType<PlayerLook>();
        bool wasInput = player != null && player.InputLocked, wasLook = look != null && look.LookLocked;
        if (player != null) player.InputLocked = true;
        if (look != null) look.LookLocked = true;
        Request(popup, true, true);   // (it takes clicks: its own buttons, if any)

        // A click during Min Time isn't lost: it closes it as soon as Min Time is over
        bool clicked = false;
        for (float shownFor = 0f; ; )
        {
            if (!UIManager.GameIsPaused)
            {
                Time.timeScale = 0f;   // (again every frame: leaving the pause sets it back to 1)
                shownFor += Time.unscaledDeltaTime;
                bool mouse = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
                bool key = Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape) && !Input.GetKeyDown(KeyCode.P) &&
                           !Input.GetKeyDown(KeyCode.JoystickButton7);   // (not the pause keys)
                if ((mouse || key) && shownFor >= 0.15f) clicked = true;   // (not the click that opened it)
                if (clicked && shownFor >= minTime) break;
            }
            yield return null;
        }

        Release(popup, true);
        if (!UIManager.GameIsPaused) Time.timeScale = 1f;
        yield return null;   // (that click doesn't grab or jump too)
        if (player != null) player.InputLocked = wasInput;
        if (look != null) look.LookLocked = wasLook;
        ModalOpen = false;
        closed?.Invoke();
    }
}
