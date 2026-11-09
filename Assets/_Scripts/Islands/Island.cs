using System.Collections;
using UnityEngine;

// Island: rises from the void when its pedestal is solved (see Lift). The camera shakes while it moves.
// Place it in its final place (up): when the game starts it goes down and waits, unless Start Raised is on.
// At the end of the game every island falls away (FallAway).
public class Island : Lift
{
    // The island something is part of: the nearest Island or Elevator holding it (itself included), or null (a
    // bridge, the void). An elevator counts as an island: you stand on it, it has its respawn
    public static Transform Of(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent) if (IsIsland(p)) return p;
        return null;
    }
    public static bool IsIsland(Transform t) => t.GetComponent<Island>() != null || t.GetComponent<Elevator>() != null;

    // Every island in the scene (LevelManager: is any still rising?)
    public static readonly System.Collections.Generic.List<Island> All = new System.Collections.Generic.List<Island>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    [Section("Island")]
    [Tooltip("Already up when the game starts (Island_01, the tutorial). Off: it goes down at start and rises when its pedestal is solved")]
    public bool startRaised = false;
    [Tooltip("Camera shake while it moves. 0 = off")]
    public float cameraShakeAmount = 0.1f;
    [Tooltip("Speeds up to this when falling away at the end (m/s)")]
    public float fallAwaySpeed = 6f;

    private PlayerLook look;
    private Vector3 groundBottom;      // the middle of its ground's underside, from its pivot (measured at the start)
    private float groundRadius, groundHeight;
    private Material groundLook;

    // How deep it waits: the LevelManager's Island Depth (0: Drop Distance)
    protected override float DropFrom(LevelManager lm) => lm.islandDepth > 0f ? lm.islandDepth : lm.dropDistance;

    protected override void Start()
    {
        base.Start();
        look = PlayerLook.Instance;
        if (OwnBounds(true, out Bounds ground, out MeshRenderer main))   // (now, while it is still seen)
        {
            groundBottom = new Vector3(ground.center.x, ground.min.y, ground.center.z) - transform.position;
            groundRadius = Mathf.Max(ground.extents.x, ground.extents.z);
            groundHeight = ground.size.y;
            groundLook = main != null ? main.sharedMaterial : null;
        }
        if (startRaised || DebugTools.AllIslandsUp) { SetUp(true); StartCoroutine(Drip()); }
        else { SetHeight(BottomY); SetPresent(false); }   // waits below, invisible
    }

    // One rumble for the whole move (before: many 0.1 s shakes in a row, felt like hits); rising, rocks fall off it
    protected override void OnMoveStarted(bool rising)
    {
        if (cameraShakeAmount > 0f && look != null)
            look.StartCoroutine(look.ShakeTheCamera(rising ? RiseDuration : LowerDuration, cameraShakeAmount));
        if (rising) StartCoroutine(Shed(RiseDuration));
    }

    protected override void OnMoveFinished(bool rising)
    {
        if (rising) StartCoroutine(Drip());
    }

    protected override void OnArriving() => Burst();
    protected override float ArriveLead => LevelManager.Instance != null ? LevelManager.Instance.arriveDebrisLead : 0f;

    // Arriving: it stops at its place and a lot of rocks are shaken off at once, down and out (only that time)
    void Burst()
    {
        LevelManager lm = LevelManager.Instance;
        if (lm == null || lm.riseDebris == null || lm.riseDebris.Length == 0 || lm.arriveDebrisCount <= 0 || groundRadius <= 0f) return;
        Vector3 bottom = transform.position + groundBottom;   // (seen from anywhere: it is only once)
        for (int i = 0; i < lm.arriveDebrisCount; i++)
        {
            Vector2 around = Random.insideUnitCircle * groundRadius * 0.6f;
            Vector3 from = bottom + new Vector3(around.x, Random.Range(0f, groundHeight * 0.5f), around.y);
            Vector3 outward = new Vector3(around.x, 0f, around.y) / Mathf.Max(groundRadius * 0.6f, 0.01f);
            DebrisBurst.Spawn(lm.riseDebris, 1, from, Vector3.down + outward * lm.riseDebrisCone * 1.3f, lm.riseDebrisSpeed * 1.6f,
                              lm.riseDebrisCone * 0.4f, groundRadius * lm.riseDebrisScale, lm.riseDebrisLife, 0f, groundLook);
        }
    }

    // Up and still: a pebble falls off its underside now and then, slowly (seen only near)
    IEnumerator Drip()
    {
        LevelManager lm = LevelManager.Instance;
        if (lm == null || lm.riseDebris == null || lm.riseDebris.Length == 0 || lm.idleDebrisRate <= 0f || groundRadius <= 0f) yield break;
        Camera cam = Camera.main;
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(0.5f, 1.5f) / lm.idleDebrisRate);
            if (IsMoving) continue;
            Vector3 bottom = transform.position + groundBottom;
            if (cam == null) cam = Camera.main;
            if (cam != null && (cam.transform.position - bottom).sqrMagnitude > lm.riseDebrisRange * lm.riseDebrisRange) continue;
            Vector2 around = Random.insideUnitCircle * groundRadius * 0.6f;
            Vector3 from = bottom + new Vector3(around.x, Random.Range(0f, groundHeight * 0.5f), around.y);
            DebrisBurst.Spawn(lm.riseDebris, 1, from, Vector3.down, 0.6f, 0.2f, groundRadius * lm.idleDebrisScale, lm.riseDebrisLife * 1.5f, 0f, groundLook);
        }
    }

    // Rocks falling off its underside while it rises: from near its middle, thrown down and out in a cone, shrinking
    // to nothing (the stones' trail, bigger). Fewer at the end, as it slows down
    IEnumerator Shed(float seconds)
    {
        LevelManager lm = LevelManager.Instance;
        if (lm == null || lm.riseDebris == null || lm.riseDebris.Length == 0 || lm.riseDebrisRate <= 0f || groundRadius <= 0f) yield break;
        Camera cam = Camera.main;
        float due = 0f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float p = t / seconds;
            due += lm.riseDebrisRate * Time.deltaTime * (1f - p * p);
            Vector3 bottom = transform.position + groundBottom;
            bool near = cam == null || (cam.transform.position - bottom).sqrMagnitude < lm.riseDebrisRange * lm.riseDebrisRange;
            for (; due >= 1f; due -= 1f)
            {
                if (!near) continue;
                Vector2 around = Random.insideUnitCircle * groundRadius * 0.4f;
                Vector3 from = bottom + new Vector3(around.x, Random.Range(0f, groundHeight * 0.4f), around.y);
                Vector3 outward = new Vector3(around.x, 0f, around.y) / Mathf.Max(groundRadius * 0.4f, 0.01f);
                Vector3 direction = Vector3.down + outward * lm.riseDebrisCone;
                DebrisBurst.Spawn(lm.riseDebris, 1, from, direction, lm.riseDebrisSpeed, lm.riseDebrisCone * 0.3f,
                                  groundRadius * lm.riseDebrisScale, lm.riseDebrisLife, 0f, groundLook);
            }
            yield return null;
        }
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
