using System.Collections;
using UnityEngine;

// Island: rises from the void when its pedestal is solved (see Lift). The camera shakes while it moves.
// Place it in its final place (up): when the game starts it goes down and waits, unless Start Raised is on.
// At the end of the game every island falls away (FallAway).
public class Island : Lift
{
    // The island something is part of: the nearest Island or Elevator holding it (itself included), or null (a
    // bridge, the void). An elevator counts as an island: you stand on it, it has its respawn. Before: the "Isla" tag
    public static Transform Of(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent) if (IsIsland(p)) return p;
        return null;
    }
    public static bool IsIsland(Transform t) => t.GetComponent<Island>() != null || t.GetComponent<Elevator>() != null;

    [Section("Island")]
    [Tooltip("Already up when the game starts (Island_01, the tutorial). Off: it goes down at start and rises when its pedestal is solved")]
    public bool startRaised = false;
    [Tooltip("Camera shake while it moves. 0 = off")]
    public float cameraShakeAmount = 0.1f;
    [Tooltip("Speeds up to this when falling away at the end (m/s)")]
    public float fallAwaySpeed = 6f;

    private PlayerLook look;

    protected override void Start()
    {
        base.Start();
        look = PlayerLook.Instance;
        if (startRaised || DebugTools.AllIslandsUp) SetUp(true);
        else { SetHeight(BottomY); SetPresent(false); }   // waits below, invisible
    }

    // One rumble for the whole move (before: many 0.1 s shakes in a row, felt like hits)
    protected override void OnMoveStarted(bool rising)
    {
        if (cameraShakeAmount > 0f && look != null)
            look.StartCoroutine(look.ShakeTheCamera(rising ? RiseDuration : LowerDuration, cameraShakeAmount));
    }

    // End of the game: falls forever, speeding up
    public void FallAway()
    {
        StopAllCoroutines();
        StartCoroutine(FallAwayRoutine());
    }

    IEnumerator FallAwayRoutine()
    {
        for (float speed = 0f; ; speed = Mathf.Min(speed + fallAwaySpeed * Time.deltaTime, fallAwaySpeed))
        {
            SetHeight(CurrentHeight - speed * Time.deltaTime);
            yield return null;
        }
    }
}
