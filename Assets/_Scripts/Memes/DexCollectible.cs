using System.Collections;
using UnityEngine;

// An optional meme (MemeDex: Optional): a meme window at the end of an optional course. Click it: it flies to its own
// place on the HUD (the HudWindow marked For Dex, e.g. MemeWindow_Dex), plays its meme for Show Time (a long or looping
// sound is cut then), sinks off the bottom of the screen and is gone for good, saved in the MemeDex. Its Minimize or
// Close button sinks it right away.
// It never goes into your hand: you can be carrying your own meme window at the same time.
// Goes on that meme window (a MemeWindow with its MemeData: only its Base).
[RequireComponent(typeof(MemeWindow))]
public class DexCollectible : MonoBehaviour
{
    [Tooltip("On: an object with physics (as the orbs): it rises frozen with its island, then falls and lies face up. Off: it stays exactly where you placed it (hidden in the ground, on a branch…), as your meme window")]
    public bool usePhysics = true;
    public enum ShowFor { Fixed, Content }

    [Tooltip("Fixed: Show Time. Content: as long as its sound (one play, even a loop), else its video; neither: Show Time")]
    public ShowFor showFor = ShowFor.Fixed;
    [Tooltip("Seconds it shows on your screen (its sound is cut then, even a long or looping one)")]
    public float showTime = 3.5f;
    [Tooltip("Content: never longer than this (0: no limit)")]
    public float maxTime = 0f;
    [Tooltip("Seconds it takes to sink off the screen (its sound fades out meanwhile)")]
    public float sinkTime = 0.4f;
    public AudioClip unlockClip;

    private MemeWindow window;
    private bool taken, closeNow;

    void Awake() => window = GetComponent<MemeWindow>();

    // Clicked (PlayerLook)
    public void Collect()
    {
        if (taken) return;
        HudWindow hud = HudWindow.Dex;
        MemeDock dock = MemeDock.Instance;
        if (hud == null || dock == null) { Debug.LogWarning("[DexCollectible] No HUD window marked For Dex (or no MemeDock).", this); return; }
        taken = true;
        window.StartVideo();   // (Video Start = On Pick Up: it starts now)
        StartCoroutine(Collecting(hud, dock));
    }

    IEnumerator Collecting(HudWindow hud, MemeDock dock)
    {
        if (window.Data != null && MemeDex.Instance != null) MemeDex.Instance.Unlock(new MemeDex.Entry { meme = window.Data, kind = MemeDex.Kind.Optional });

        // Out of the world: no physics, riding with the camera
        Vector3 worldScale = transform.lossyScale;
        window.StopFacing();
        if (TryGetComponent(out Rigidbody body)) { body.interpolation = RigidbodyInterpolation.None; body.isKinematic = true; }
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        transform.SetParent(dock.Cam.transform, true);

        // Its sound (its own source: cut when it goes)
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        AudioClip clip = window.Data != null ? window.Data.BaseSound : null;
        if (clip != null) { source.clip = clip; source.loop = window.Data.baseImage != null && window.Data.baseImage.loop; source.Play(); }
        if (unlockClip != null) source.PlayOneShot(unlockClip);

        // To its HUD place; near the end the 3D one hides and the HUD one shows (as MemeDock)
        Vector3 fromPosition = transform.localPosition;
        Quaternion fromRotation = transform.localRotation;
        Vector3 fromScale = transform.localScale;
        bool swapped = false;
        for (float t = 0f; t < dock.flyTime; t += Time.deltaTime)
        {
            float p = MemeDock.Ease(t / dock.flyTime);
            dock.HudPose(hud, window, worldScale, out Vector3 position, out Quaternion rotation, out Vector3 scale);
            transform.localPosition = Vector3.LerpUnclamped(fromPosition, position, p);
            transform.localRotation = Quaternion.SlerpUnclamped(fromRotation, rotation, p);
            transform.localScale = Vector3.LerpUnclamped(fromScale, scale, p);
            if (!swapped && t / dock.flyTime >= dock.swapAt) { Swap(hud, renderers); swapped = true; }
            yield return null;
        }
        if (!swapped) Swap(hud, renderers);

        // Showing (a video's frames keep coming: mirrored every frame)
        closeNow = false;
        hud.closed = () => closeNow = true;
        float seconds = ShowSeconds();
        for (float t = 0f; t < seconds && !closeNow; t += Time.deltaTime)
        {
            window.MirrorTo(hud);
            yield return null;
        }
        hud.closed = null;

        // Off the bottom of the screen, its sound fading out, then gone
        float volume = source.volume;
        Coroutine sinking = hud.StartCoroutine(hud.SinkAway(sinkTime));
        for (float t = 0f; t < sinkTime; t += Time.deltaTime)
        {
            source.volume = volume * (1f - t / sinkTime);
            window.MirrorTo(hud);
            yield return null;
        }
        yield return sinking;
        source.Stop();
        gameObject.SetActive(false);
    }

    // How long it shows (Show For)
    float ShowSeconds()
    {
        if (showFor == ShowFor.Fixed || window.Data == null) return showTime;
        float content = 0f;
        AudioClip sound = window.Data.BaseSound;
        if (sound != null) content = sound.length;
        else if (window.Data.baseImage != null && window.Data.baseImage.video != null) content = (float)window.Data.baseImage.video.length;
        if (content <= 0f) return showTime;
        return maxTime > 0f ? Mathf.Min(content, maxTime) : content;
    }

    void Swap(HudWindow hud, Renderer[] renderers)
    {
        foreach (Renderer r in renderers) if (r != null) r.enabled = false;
        window.MirrorTo(hud);
        hud.Show(true);
    }
}
