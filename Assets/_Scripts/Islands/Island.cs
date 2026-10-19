using System.Collections;
using UnityEngine;

// Island: rises from the void when its pedestal is solved (see Lift). The camera shakes while it moves.
// Place it in its final place (up): when the game starts it goes down and waits, unless Start Raised is on.
// At the end of the game every island falls away (FallAway).
public class Island : Lift
{
    [Header("Island")]
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
        if (Camera.main != null) look = Camera.main.GetComponent<PlayerLook>();
        if (startRaised) SetUp(true);
        else SetHeight(BottomY);
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
            SetHeight(transform.position.y - speed * Time.deltaTime);
            yield return null;
        }
    }
}
