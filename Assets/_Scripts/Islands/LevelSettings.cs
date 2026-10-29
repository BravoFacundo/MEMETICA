using UnityEngine;

// A level's settings, for all its stone paths at once. Goes on the level's root (Level_TrollFace, with its islands and
// bridges inside). Right click the component (or its ⋮) > Apply …: these values go to every stone path, stone and
// bridge inside the level that they fit (Slide values to the Slide stones…). Then change any one by hand: Apply again
// overwrites them. Read From Level fills these with what its first ones have. Ctrl+Z undoes an Apply.
// While playing, LevelManager > Platform Speed speeds up or slows down every stone of every level.
public class LevelSettings : MonoBehaviour
{
    [Section("Appearance (stone paths)")]
    [Tooltip("Seconds each stone takes to rise")]
    public float riseTime = 1f;
    [Tooltip("Seconds between one stone starting to rise and the next")]
    public float stoneGap = 0.12f;
    [Tooltip("Once all are up: seconds between one moving stone starting its motion and the next")]
    public float startGap = 0.35f;
    [Tooltip("Seconds each stone takes to sink when the bridge goes away")]
    public float sinkTime = 0.7f;

    [Section("Slide")]
    [Tooltip("Seconds from one point to the next")]
    public float travelTime = 2.5f;
    [Tooltip("Seconds it stops at each point")]
    public float pauseTime = 0.5f;

    [Section("Hop")]
    [Tooltip("Seconds it stays at each point: x = y fixed, else random between them")]
    public Vector2 hopInterval = new Vector2(1.5f, 1.5f);
    [Tooltip("Seconds the jump takes")]
    public float hopTime = 0.15f;

    [Section("Blink")]
    [Tooltip("Seconds it stays at each point: x = y fixed, else random between them")]
    public Vector2 blinkInterval = new Vector2(1.5f, 1.5f);
    [Tooltip("Seconds it shakes before vanishing")]
    public float warningTime = 0.6f;
    [Tooltip("Meters it shakes")]
    public float blinkShake = 0.06f;
    [Tooltip("Seconds it is gone before showing up at the next point")]
    public float gapTime = 0.5f;

    [Section("Fall (stones that drop when stepped on)")]
    [Tooltip("Seconds it shakes before dropping")]
    public float fallWarning = 0.2f;
    [Tooltip("How fast it speeds up dropping (m/s²)")]
    public float fallAcceleration = 25f;
    [Tooltip("Its top speed dropping (m/s)")]
    public float maxFallSpeed = 20f;
    [Tooltip("Seconds it stays down before coming back")]
    public float returnDelay = 2.5f;
    [Tooltip("Meters per second it comes back up")]
    public float riseSpeed = 3f;

    [Section("All Moving Stones")]
    [Tooltip("They push the player when they hit them (not when standing on them)")]
    public bool pushPlayer = true;

#if UNITY_EDITOR
    [ContextMenu("Apply All")]
    void ApplyAll()
    {
        int count = ApplyAppearance(false) + ApplyStones(false) + ApplyFall(false);
        Debug.Log($"[Level] '{name}': settings applied ({count} changed).", this);
    }

    [ContextMenu("Apply Appearance")] void MenuAppearance() => ApplyAppearance(true);
    [ContextMenu("Apply Stone Motions (Slide, Hop, Blink, Push)")] void MenuStones() => ApplyStones(true);
    [ContextMenu("Apply Fall")] void MenuFall() => ApplyFall(true);

    int ApplyAppearance(bool log)
    {
        int count = 0;
        foreach (StonePath path in GetComponentsInChildren<StonePath>(true))
        {
            UnityEditor.Undo.RecordObject(path, "Apply Level Settings");
            path.riseTime = riseTime;
            path.stoneGap = stoneGap;
            path.startGap = startGap;
            path.sinkTime = sinkTime;
            UnityEditor.EditorUtility.SetDirty(path);
            count++;
        }
        if (log) Debug.Log($"[Level] '{name}': appearance on {count} stone path(s).", this);
        return count;
    }

    int ApplyStones(bool log)
    {
        int count = 0;
        foreach (PathStone stone in GetComponentsInChildren<PathStone>(true))
        {
            UnityEditor.Undo.RecordObject(stone, "Apply Level Settings");
            switch (stone.motion)
            {
                case PathStone.Motion.Slide: stone.travelTime = travelTime; stone.pauseTime = pauseTime; break;
                case PathStone.Motion.Hop: stone.interval = hopInterval; stone.hopTime = hopTime; break;
                case PathStone.Motion.Blink:
                    stone.interval = blinkInterval; stone.warningTime = warningTime; stone.blinkShake = blinkShake; stone.gapTime = gapTime;
                    break;
            }
            if (stone.motion != PathStone.Motion.None && stone.motion != PathStone.Motion.Fall) stone.pushPlayer = pushPlayer;
            UnityEditor.EditorUtility.SetDirty(stone);
            count++;
        }
        if (log) Debug.Log($"[Level] '{name}': motions on {count} stone(s).", this);
        return count;
    }

    int ApplyFall(bool log)
    {
        int count = 0;
        foreach (Bridge bridge in GetComponentsInChildren<Bridge>(true))
        {
            if (bridge.platformSettings == null) continue;
            UnityEditor.Undo.RecordObject(bridge, "Apply Level Settings");
            bridge.platformSettings.warningTime = fallWarning;
            bridge.platformSettings.fallAcceleration = fallAcceleration;
            bridge.platformSettings.maxFallSpeed = maxFallSpeed;
            bridge.platformSettings.returnDelay = returnDelay;
            bridge.platformSettings.riseSpeed = riseSpeed;
            UnityEditor.EditorUtility.SetDirty(bridge);
            count++;
        }
        if (log) Debug.Log($"[Level] '{name}': fall on {count} bridge(s).", this);
        return count;
    }

    // These values from the first stone path, stone of each motion and bridge inside the level
    [ContextMenu("Read From Level")]
    void ReadFromLevel()
    {
        UnityEditor.Undo.RecordObject(this, "Read Level Settings");
        StonePath path = GetComponentInChildren<StonePath>(true);
        if (path != null) { riseTime = path.riseTime; stoneGap = path.stoneGap; startGap = path.startGap; sinkTime = path.sinkTime; }
        foreach (PathStone stone in GetComponentsInChildren<PathStone>(true))
        {
            switch (stone.motion)
            {
                case PathStone.Motion.Slide: travelTime = stone.travelTime; pauseTime = stone.pauseTime; pushPlayer = stone.pushPlayer; break;
                case PathStone.Motion.Hop: hopInterval = stone.interval; hopTime = stone.hopTime; pushPlayer = stone.pushPlayer; break;
                case PathStone.Motion.Blink:
                    blinkInterval = stone.interval; warningTime = stone.warningTime; blinkShake = stone.blinkShake; gapTime = stone.gapTime;
                    pushPlayer = stone.pushPlayer;
                    break;
            }
        }
        foreach (Bridge bridge in GetComponentsInChildren<Bridge>(true))
            if (bridge.platformSettings != null)
            {
                fallWarning = bridge.platformSettings.warningTime; fallAcceleration = bridge.platformSettings.fallAcceleration;
                maxFallSpeed = bridge.platformSettings.maxFallSpeed; returnDelay = bridge.platformSettings.returnDelay; riseSpeed = bridge.platformSettings.riseSpeed;
                break;
            }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[Level] '{name}': settings read from its stones and bridges.", this);
    }
#endif
}
