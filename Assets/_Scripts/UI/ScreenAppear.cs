using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A screen's parts appearing one after another (UIManager > Screens > Appear In Order calls it when a screen opens):
// its children that are on, in hierarchy order (or reversed), one every Interval seconds however many they are, each
// popping in (growing in place from its middle, a little past its size, and settling, fading in) with its sound: its
// UIWindow's (Window: Open Sound, Popup: Popup Sound); without one, silent. Skip: a click or a key shows the rest at
// once. Closing (Disappear): the other way round, the last one first, each shrinking into its middle and fading out.
// Only the animation: when it runs is UIManager's.
public class ScreenAppear
{
    private readonly List<Transform> parts = new List<Transform>();
    private readonly Dictionary<Transform, Vector3> scales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> places = new Dictionary<Transform, Vector3>();
    private readonly List<Transform> popping = new List<Transform>();
    private readonly Dictionary<Transform, float> poppedFor = new Dictionary<Transform, float>();
    private bool vanishing;

    public IEnumerator Play(Transform screen, bool reverse, float delay, float interval, float popTime, bool skippable)
    {
        parts.Clear();
        foreach (Transform child in screen) if (child.gameObject.activeSelf) parts.Add(child);   // (a window closed in it stays closed)
        if (reverse) parts.Reverse();
        foreach (Transform t in parts) { Remember(t); Hide(t); }
        vanishing = false;

        float started = Time.unscaledTime;
        bool skipped = false;
        bool Skip() => skipped = skipped || (skippable && Time.unscaledTime > started && (Input.anyKeyDown || Input.GetMouseButtonDown(0)));

        for (float t = 0f; t < delay && !Skip(); t += Time.unscaledDeltaTime) yield return null;
        float time = Mathf.Max(popTime, 0.01f);
        for (int i = 0; i < parts.Count; i++)
        {
            if (Skip()) break;
            Transform part = parts[i];
            if (part == null) continue;
            UISound? sound = UIWindow.SoundOf(part.gameObject);
            if (sound.HasValue) AudioManager.PlayUI(sound.Value);
            popping.Add(part);
            // This one pops while the wait for the next one goes by (the pops overlap, in a cascade)
            float until = Time.unscaledTime + interval;
            while (Time.unscaledTime < until && !Skip()) { Animate(time); yield return null; }
        }
        while (popping.Count > 0 && !Skip()) { Animate(time); yield return null; }
        ShowAll();
    }

    // Closing: the last one shown first, each shrinking into its middle, one every Interval. Then the caller turns the
    // screen off (and ShowAll leaves them as they are for next time)
    public IEnumerator Disappear(float interval, float popTime)
    {
        vanishing = true;
        popping.Clear();
        foreach (Transform t in parts) if (t != null) { Group(t).blocksRaycasts = Group(t).interactable = false; poppedFor.Remove(t); }
        float time = Mathf.Max(popTime, 0.01f);
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            if (parts[i] == null) continue;
            popping.Add(parts[i]);
            float until = Time.unscaledTime + interval;
            while (Time.unscaledTime < until) { Animate(time); yield return null; }
        }
        while (popping.Count > 0) { Animate(time); yield return null; }
    }

    // Every part as it is in the scene, clickable (done, skipped or closed)
    public void ShowAll()
    {
        foreach (Transform t in parts) if (t != null) Shown(t);
        popping.Clear();
        poppedFor.Clear();
        vanishing = false;
    }

    // The parts popping (in or out) now, a frame further (each from when it started)
    void Animate(float popTime)
    {
        for (int i = popping.Count - 1; i >= 0; i--)
        {
            Transform t = popping[i];
            float e = (poppedFor.TryGetValue(t, out float f) ? f : 0f) + Time.unscaledDeltaTime;
            poppedFor[t] = e;
            float p = Mathf.Clamp01(e / popTime);
            UIPop.Scale(t, scales[t], places[t], vanishing ? UIPop.Shrink(p) : UIPop.Grow(p));
            Group(t).alpha = vanishing ? 1f - p : Mathf.Clamp01(p * 2f);
            if (p < 1f) continue;
            popping.RemoveAt(i);
            poppedFor.Remove(t);
            if (!vanishing) Shown(t);
        }
    }

    void Remember(Transform t)
    {
        if (!scales.ContainsKey(t)) scales[t] = t.localScale;
        if (!places.ContainsKey(t)) places[t] = t.localPosition;
    }

    void Hide(Transform t)
    {
        CanvasGroup group = Group(t);
        group.alpha = 0f;
        group.blocksRaycasts = group.interactable = false;
        poppedFor.Remove(t);
    }

    void Shown(Transform t)
    {
        CanvasGroup group = Group(t);
        if (scales.TryGetValue(t, out Vector3 s)) t.localScale = s;
        if (places.TryGetValue(t, out Vector3 p)) t.localPosition = p;
        group.alpha = 1f;
        group.blocksRaycasts = group.interactable = true;
    }

    static CanvasGroup Group(Transform t)
    {
        CanvasGroup group = t.GetComponent<CanvasGroup>();
        return group != null ? group : t.gameObject.AddComponent<CanvasGroup>();
    }
}

// A UI panel growing or shrinking in place, from its middle (whatever its pivot: a corner too). Screens (ScreenAppear)
// and popups (PopupManager) use it
public static class UIPop
{
    // 0 → 1 a little past it, then settling
    public static float Grow(float p)
    {
        float q = Mathf.Clamp01(p) - 1f;
        return 1f + 2.70158f * q * q * q + 1.70158f * q * q;
    }

    // 1 → 0: a little bigger first, then into its middle
    public static float Shrink(float p)
    {
        p = Mathf.Clamp01(p);
        return 1f - (2.70158f * p * p * p - 1.70158f * p * p);
    }

    // Its size × k, its middle where it was (scale: its own size; position: its place at that size)
    public static void Scale(Transform t, Vector3 scale, Vector3 position, float k)
    {
        t.localScale = scale * k;
        if (t is RectTransform r)
        {
            Vector2 middle = r.rect.center;   // (from its pivot to its middle, in its own units)
            Vector3 offset = new Vector3(middle.x * scale.x, middle.y * scale.y, 0f) * (1f - k);
            t.localPosition = position + t.localRotation * offset;
        }
    }
}

// A single window growing in place as it opens and shrinking into its middle as it closes (UIManager.OpenWindow /
// CloseWindow: the Menu's windows, the tutorial question…). Remembers each one's own size and place
public class WindowPop
{
    private readonly Dictionary<Transform, Vector3> scales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> places = new Dictionary<Transform, Vector3>();

    // Open: from nothing to its size. Closed: to nothing, and it turns off at the end
    public IEnumerator Run(Transform t, bool open, float popTime)
    {
        if (!scales.ContainsKey(t)) { scales[t] = t.localScale; places[t] = t.localPosition; }
        CanvasGroup group = t.GetComponent<CanvasGroup>();
        if (group == null) group = t.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = open;   // (closing: not clickable any more)
        float time = Mathf.Max(popTime, 0.01f);
        for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
        {
            float p = e / time;
            UIPop.Scale(t, scales[t], places[t], open ? UIPop.Grow(p) : UIPop.Shrink(p));
            group.alpha = open ? Mathf.Clamp01(p * 2f) : 1f - p;
            yield return null;
        }
        Restore(t);
        if (!open) t.gameObject.SetActive(false);
    }

    // As it is in the scene (done, or cut halfway)
    public void Restore(Transform t)
    {
        if (scales.TryGetValue(t, out Vector3 s)) { t.localScale = s; t.localPosition = places[t]; }
        CanvasGroup group = t.GetComponent<CanvasGroup>();
        if (group != null) { group.alpha = 1f; group.blocksRaycasts = group.interactable = true; }
    }
}
