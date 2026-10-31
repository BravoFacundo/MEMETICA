using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// The randomizer: a pedestal that gives the meme window its meme (the hub). Put a meme window (MemeWindow, empty or
// not) on it: the axes still to draw go by on the window, faster and then slower and slower, it stops on one at random
// (or always on Force Result), the window becomes that meme (MemeChanger.SetMeme) and that axis' Sequence rises: its
// islands and bridges by name (Island_09, Bridge_T1; or just the ID if only one of them has it).
// Timing: the platform turns (Turn Time) with the pedestal up, the pedestal slams in, the target (the first island listed) rises; once it
// arrived the other islands start in their listed order (Island Gap apart, overlapping), and once the last one arrived
// the bridges, in their listed order, each one once the previous one is in place (stone by stone, in order). Then you
// can walk from the first bridge on. Timeline (in the Inspector) adds it all up for each axis.
// Drawn axes don't come out again; once every axis was drawn, all of them can again.
// Goes on a Pedestal (black, Keyless, with nothing in its own lists): the pedestal snaps the meme and calls Draw
// instead of judging it.
// On an AimPlatform (the pedestal inside it): after the raffle the platform turns toward the winner's first island
// (or its Aim At), and then its way rises in front of you. With a PedestalRetire it slams into the ground first (meme
// and all), so you see it on the horizon. Lock Player: you can only look around until the whole way is up.
[RequireComponent(typeof(Pedestal))]
public class MemeRandomizer : MonoBehaviour
{
    // (old: kept only to bring its values into Sequence)
    [System.Serializable]
    public class Step
    {
        public enum Kind { Island, Bridge }
        public Kind kind = Kind.Island;
        public string id;
    }

    [System.Serializable]
    public class Axis
    {
        public MemeData meme;
        [Tooltip("What rises when this axis comes out. Names as in the scene: Island_09, Bridge_T1 (or just the ID, 09 / T1, " +
                 "if no island and bridge share it). The first island is the target; then islands in this order, then bridges in this order")]
        public string[] sequence = new string[0];
        [Tooltip("Where the platform points when this axis comes out (empty: the first island of its Sequence)")]
        public Transform aimAt;

        // Older ways to list it (kept only to bring their values into Sequence)
        [HideInInspector] public string[] islands;
        [HideInInspector] public string[] bridges;
        [HideInInspector, FormerlySerializedAs("way")] public List<Step> steps;
    }

    [Section("Axes")]
    public List<Axis> axes = new List<Axis>();
    [Tooltip("Always this one comes out (the raffle still shows them all going by). Empty: at random")]
    public MemeData forceResult;

    [Section("Raffle")]
    [Tooltip("Seconds the images go by before it stops")]
    public float raffleTime = 3f;
    [Tooltip("Each image going by (empty: MemeSettings' sweep tick)")]
    public AudioClip tickClip;
    [Tooltip("When it stops on the winner (empty: MemeSettings' change sound). The winner's grab sound plays too")]
    public AudioClip winClip;

    [Section("Timing (after the raffle)")]
    [Tooltip("Seconds the platform takes to turn (0: its own Turn Speed)")]
    public float turnTime = 3f;
    [Tooltip("Seconds the target island (the first island listed) takes to rise")]
    public float targetRise = 2.5f;
    [Tooltip("Seconds after the target arrived that the next island starts")]
    public float islandsAfter = 0.1f;
    [Tooltip("Seconds each of the other islands takes to rise")]
    public float islandRise = 2f;
    [Tooltip("Seconds between one island starting and the next")]
    public float islandGap = 0.5f;
    [Tooltip("Seconds after the last island arrived that the first bridge starts")]
    public float bridgesAfter = 0.2f;
    [Tooltip("Seconds each bridge takes to rise (Rise From Below; a stone path: its own StonePath > Appearance)")]
    public float bridgeRise = 1.5f;
    [Tooltip("Seconds after one bridge is in place before the next one starts")]
    public float bridgeGap = 0.1f;
    [Tooltip("When each part happens, added up for each axis (seconds from placing the meme)")]
    [ReadOnlyField, TextArea(2, 12)] public string timeline;

    [Section("Snap & Stand (on its platform)")]
    [Tooltip("Carrying a meme this close to the platform's middle (meters, flat), it snaps in by itself (MemeRecall's " +
             "Snap In Range). 0: the usual reach from the pedestal. A bit less than the platform's radius: only once you are on it")]
    public float snapRadius = 0f;
    [Tooltip("When the raffle starts you walk by yourself to Stand Point, facing the pedestal: you see the raffle, then " +
             "the platform turns you toward your way")]
    public bool walkToStand = true;
    [Tooltip("Where you end up (a child of the platform). Empty: Stand Distance behind the pedestal, opposite the " +
             "platform's front: once it turns, you look past the pedestal at your way")]
    public Transform standPoint;
    [Tooltip("Meters from the pedestal where you stand (no Stand Point)")]
    public float standDistance = 1f;
    [Tooltip("Meters per second of that walk (slow: you see yourself taken there)")]
    public float standWalkSpeed = 1.5f;

    [Section("After The Raffle")]
    [Tooltip("The player can't walk (only look around) from the raffle until its first bridge starts")]
    public bool lockPlayer = true;

    private readonly List<Axis> drawn = new List<Axis>();
    private AimPlatform platform;
    private PedestalRetire retire;

    void Awake()
    {
        platform = GetComponentInParent<AimPlatform>();
        retire = GetComponent<PedestalRetire>();
    }

    void Reset()
    {
        Pedestal pedestal = GetComponent<Pedestal>();
        pedestal.KeylessPedestal = true;   // it doesn't judge, it gives
        pedestal.ApplyVisual();
    }

    // Old lists into Sequence, as full names (Island_09, Bridge_T1)
    void OnValidate()
    {
        LevelManager level = FindObjectOfType<LevelManager>();
        string island = level != null ? level.islandPrefix : "Island_", bridge = level != null ? level.bridgePrefix : "Bridge_";
        foreach (Axis axis in axes)
        {
            if (axis.sequence == null) axis.sequence = new string[0];
            if (axis.sequence.Length > 0) continue;
            var names = new List<string>();
            if (axis.steps != null && axis.steps.Count > 0)
                foreach (Step step in axis.steps) names.Add((step.kind == Step.Kind.Island ? island : bridge) + step.id);
            else
            {
                if (axis.islands != null) foreach (string id in axis.islands) names.Add(island + id);
                if (axis.bridges != null) foreach (string id in axis.bridges) names.Add(bridge + id);
            }
            if (names.Count == 0) continue;
            axis.sequence = names.ToArray();
            axis.steps = null;
            axis.islands = new string[0];
            axis.bridges = new string[0];
        }
        timeline = Timeline(island, bridge);
    }

    // Seconds from placing the meme to each part, for each axis (an estimate: the scene's objects found by name)
    string Timeline(string islandPrefix, string bridgePrefix)
    {
        var text = new System.Text.StringBuilder();
        PedestalRetire slam = GetComponent<PedestalRetire>();
        bool turns = GetComponentInParent<AimPlatform>() != null;
        foreach (Axis axis in axes)
        {
            if (axis.sequence == null || axis.sequence.Length == 0) continue;
            float t = raffleTime;
            text.Append($"{(axis.meme != null ? axis.meme.name : "?")}: raffle 0-{t:0.#}");
            if (turns) { t += turnTime; text.Append($" · turn →{t:0.#}{(turnTime > 0f ? "" : " (its speed)")}"); }
            if (slam != null && slam.mode == PedestalRetire.Mode.Slammed) { t += slam.delay + slam.slamTime; text.Append($" · slam →{t:0.#}"); }

            int islandCount = 0, bridgeCount = 0;
            float islandsEnd = t, bridgesStart = 0f, end = t, start = t;
            foreach (string entry in axis.sequence)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                bool isBridge = entry.StartsWith(bridgePrefix) || (!entry.StartsWith(islandPrefix) && GameObject.Find(islandPrefix + entry) == null);
                if (isBridge) continue;
                start = islandCount == 0 ? t : t + targetRise + islandsAfter + (islandCount - 1) * islandGap;
                islandsEnd = Mathf.Max(islandsEnd, start + (islandCount == 0 ? targetRise : islandRise));
                if (islandCount == 0) text.Append($" · target {t:0.#}-{t + targetRise:0.#}");
                islandCount++;
            }
            if (islandCount > 1) text.Append($" · {islandCount - 1} island(s) {t + targetRise + islandsAfter:0.#}-{islandsEnd:0.#}");
            end = islandsEnd;
            bridgesStart = islandsEnd + bridgesAfter;
            foreach (string entry in axis.sequence)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                bool isBridge = entry.StartsWith(bridgePrefix) || (!entry.StartsWith(islandPrefix) && GameObject.Find(islandPrefix + entry) == null);
                if (!isBridge) continue;
                GameObject found = GameObject.Find(entry.StartsWith(bridgePrefix) ? entry : bridgePrefix + entry);
                float rise = bridgeRise;
                if (found != null && found.TryGetComponent(out StonePath stones)) rise = Mathf.Max(0, stones.Stones.Count - 1) * stones.stoneGap + stones.riseTime;
                end = (bridgeCount == 0 ? bridgesStart : end + bridgeGap) + rise;   // one after the other
                bridgeCount++;
            }
            float walk = bridgeCount > 0 ? bridgesStart : islandsEnd;
            if (bridgeCount > 0) text.Append($" · {bridgeCount} bridge(s) {bridgesStart:0.#}-{end:0.#}");
            text.Append($"  →  walk at {walk:0.#} s, all up at {end:0.#} s\n");
        }
        return text.ToString().TrimEnd();
    }

    // Called by the pedestal once the meme snapped in. Ends when the winner is shown (its way rises after)
    public IEnumerator Draw(Meme meme)
    {
        if (!(meme is MemeChanger window)) yield break;   // a plain meme (the tutorial orb): nothing to show it on
        List<Axis> usable = axes.FindAll(a => a.meme != null && MemeImage.Exists(a.meme.baseImage));
        if (usable.Count == 0) { Debug.LogWarning($"[MemeRandomizer] '{name}' has no axes with a meme (MemeData with a base image).", this); yield break; }

        List<Axis> left = usable.FindAll(a => !drawn.Contains(a));
        if (left.Count == 0) { drawn.Clear(); left = usable; }

        Axis winner = forceResult != null ? usable.Find(a => a.meme == forceResult) : null;
        if (forceResult != null && winner == null)
            Debug.LogWarning($"[MemeRandomizer] '{name}': Force Result '{forceResult.name}' isn't one of its axes: at random.", this);
        if (winner != null) { if (!left.Contains(winner)) left.Add(winner); }
        else
        {
            List<Axis> pool = left.Count > 1 ? left.FindAll(a => a.meme != window.Data) : left;   // never the one it holds, if there is another
            winner = pool[Random.Range(0, pool.Count)];
        }

        // You stay on the platform from the raffle until your way starts (Lock Player)
        Hold(true);
        if (walkToStand) StartCoroutine(WalkToStand());
        raffling = true;
        yield return window.Raffle(left.ConvertAll(a => a.meme), winner.meme, raffleTime, tickClip, winClip);
        raffling = false;
        if (window.Data != winner.meme) { Hold(false); yield break; }   // taken off halfway

        drawn.Add(winner);
        StartCoroutine(OpenWay(winner));   // its own (taking the meme off now doesn't stop it); the pedestal says "right" meanwhile
    }

    bool raffling;
    PlayerMove held;

    void Hold(bool on)
    {
        if (!lockPlayer) return;
        if (held == null) held = FindObjectOfType<PlayerMove>();
        if (held != null) held.InputLocked = on;
    }

    // Snap Radius: you (the camera) are close enough to the platform's middle
    public bool InSnapArea(Vector3 position)
    {
        Vector3 d = position - Middle;
        d.y = 0f;
        return d.magnitude <= snapRadius;
    }

    Vector3 Middle => platform != null ? platform.transform.position : transform.position;

    // An inevitable little walk to Stand Point, turning to face the pedestal (you can't steer meanwhile)
    IEnumerator WalkToStand()
    {
        PlayerMove player = FindObjectOfType<PlayerMove>();
        if (player == null) yield break;
        PlayerLook look = FindObjectOfType<PlayerLook>();
        if (look != null) look.LookLocked = true;
        Vector3 start = player.transform.position;

        player.AutoWalkSpeed = standWalkSpeed;
        for (float time = 0f; time < 8f; time += Time.deltaTime)
        {
            Vector3 stand = StandPosition(player.transform.position);   // (it turns with the platform)
            player.AutoWalkTo = stand;
            Vector3 to = stand - player.transform.position;
            to.y = 0f;
            Vector3 face = transform.position - player.transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
                player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(face, Vector3.up), 240f * Time.deltaTime);
            bool facing = face.sqrMagnitude <= 0.01f || Vector3.Angle(player.transform.forward, face) < 2f;
            if (to.magnitude < 0.05f && facing) break;
            yield return null;
        }
        player.AutoWalkTo = null;
        if (look != null) look.LookLocked = false;   // (you can look around again; walking waits for your way)
        Debug.Log($"[MemeRandomizer] Walked you {Vector3.Distance(start, player.transform.position):F1} m to the stand point ({Vector3.ProjectOnPlane(StandPosition(player.transform.position) - player.transform.position, Vector3.up).magnitude:F2} m left).", this);
    }

    // Stand Point, or behind the pedestal opposite the platform's front (it turns with it); no platform: on your side
    Vector3 StandPosition(Vector3 from)
    {
        if (standPoint != null) return standPoint.position;
        Vector3 pedestal = transform.position;
        Vector3 away = platform != null ? -platform.Front : from - pedestal;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        return pedestal + away.normalized * standDistance;
    }

    // The meme taken off while it raffles (the pedestal stops it): free again
    void Update()
    {
        if (raffling && TryGetComponent(out Pedestal pedestal) && !pedestal.HasMeme) { raffling = false; Hold(false); }
    }

    // Into the ground (if it retires), the platform turns toward it (if on one), then its way rises
    IEnumerator OpenWay(Axis winner)
    {
        PlayerMove player = lockPlayer ? FindObjectOfType<PlayerMove>() : null;
        if (player != null) player.InputLocked = true;

        // Turns with the pedestal up, then the pedestal slams in, facing the target
        Transform target = Target(winner);
        if (platform != null)
        {
            if (target != null) yield return platform.AimAt(AimPlatform.CenterOf(target), turnTime);
            else Debug.LogWarning($"[MemeRandomizer] '{name}': nothing to point the platform at for '{winner.meme.name}' (no island in its Sequence, or it doesn't exist: check its ID, or set Aim At).", this);
        }
        if (retire != null)
        {
            retire.Retire();
            while (retire.IsMoving) yield return null;
        }

        // Islands: the target, then the rest in order, overlapping
        LevelManager level = LevelManager.Instance;
        Split(winner, out List<string> islandIds, out List<string> bridgeIds);
        var islandsUp = new List<Transform>();
        for (int i = 0; i < islandIds.Count; i++)
        {
            Transform raised = RaiseIsland(level, islandIds[i], i == 0 ? targetRise : islandRise);
            islandsUp.Add(raised);
            if (i == 0)   // the target: the rest once it arrived
            {
                yield return null;
                for (float time = 0f; time < 15f && Moving(raised); time += Time.deltaTime) yield return null;
                if (islandIds.Count > 1 && islandsAfter > 0f) yield return new WaitForSeconds(islandsAfter);
            }
            else if (i < islandIds.Count - 1 && islandGap > 0f) yield return new WaitForSeconds(islandGap);
        }

        // Bridges: once the last island arrived, in order, each one once the previous one is in place.
        // You can walk as soon as the first one starts (the rest go on while you go)
        yield return null;
        for (float time = 0f; time < 15f && islandsUp.Exists(Moving); time += Time.deltaTime) yield return null;
        if (bridgeIds.Count > 0 && bridgesAfter > 0f) yield return new WaitForSeconds(bridgesAfter);
        if (player != null) player.InputLocked = false;
        for (int i = 0; i < bridgeIds.Count; i++)
        {
            Transform t = level.FindBridge(bridgeIds[i]);
            if (t == null || !t.TryGetComponent(out Bridge bridge)) continue;
            bridge.waitForIslands = false;   // (the islands are up: this timing waited for them)
            bridge.RaiseIn(bridgeRise);
            yield return null;
            for (float time = 0f; time < 15f && Moving(t); time += Time.deltaTime) yield return null;
            if (i < bridgeIds.Count - 1 && bridgeGap > 0f) yield return new WaitForSeconds(bridgeGap);
        }

    }

    Transform RaiseIsland(LevelManager level, string id, float seconds)
    {
        Transform t = level.FindIsland(id);
        if (t != null && t.TryGetComponent(out Island island)) island.RaiseIn(seconds);
        else level.SubirIsla(id);   // an elevator (or a missing one: it warns)
        return t;
    }

    static bool Moving(Transform t)
    {
        if (t == null) return false;
        if (t.TryGetComponent(out Bridge bridge)) return bridge.IsAppearing || bridge.IsMoving;
        if (t.TryGetComponent(out Elevator elevator)) return elevator.IsRiding;
        return t.TryGetComponent(out Lift lift) && lift.IsMoving;
    }

    // Its Sequence as island IDs and bridge IDs, each in their listed order
    void Split(Axis axis, out List<string> islandIds, out List<string> bridgeIds)
    {
        islandIds = new List<string>();
        bridgeIds = new List<string>();
        foreach (string entry in axis.sequence)
            if (LevelManager.Instance.Resolve(entry, out string id, out bool island)) (island ? islandIds : bridgeIds).Add(id);
    }

#if UNITY_EDITOR
    // Selected: a line from the platform to each axis' target (what it will point at)
    void OnDrawGizmosSelected()
    {
        AimPlatform aim = GetComponentInParent<AimPlatform>();
        if (aim == null) return;
        if (snapRadius > 0f)   // Snap Radius: a circle around the platform's middle
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f);
            Vector3 c = aim.transform.position + Vector3.up * 0.1f;
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f, b = (i + 1) * Mathf.PI * 2f / 48f;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * snapRadius, c + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * snapRadius);
            }
        }
        Gizmos.color = new Color(0.4f, 1f, 0.6f);   // where you will stand
        Vector3 stand = standPoint != null ? standPoint.position : transform.position - aim.Front * standDistance;
        Gizmos.DrawWireSphere(new Vector3(stand.x, transform.position.y, stand.z), 0.4f);
        LevelManager level = FindObjectOfType<LevelManager>();
        string prefix = level != null ? level.islandPrefix : "Island_";
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        foreach (Axis axis in axes)
        {
            Transform target = axis.aimAt;
            if (target == null && axis.sequence != null)
                foreach (string entry in axis.sequence)
                {
                    if (string.IsNullOrEmpty(entry)) continue;
                    GameObject found = GameObject.Find(entry.StartsWith(prefix) ? entry : prefix + entry);
                    if (found != null) { target = found.transform; break; }
                }
            if (target == null) continue;
            Vector3 center = AimPlatform.CenterOf(target);
            Gizmos.DrawLine(aim.transform.position, center);
            Gizmos.DrawWireSphere(center, 1f);
            if (axis.meme != null) UnityEditor.Handles.Label(center + Vector3.up * 1.5f, axis.meme.name);
        }
    }
#endif

    Transform Target(Axis axis)
    {
        if (axis.aimAt != null) return axis.aimAt;
        LevelManager level = LevelManager.Instance;
        foreach (string entry in axis.sequence)
            if (level.Resolve(entry, out string id, out bool island) && island) return level.FindIsland(id);
        foreach (string entry in axis.sequence)
            if (level.Resolve(entry, out string id, out bool island) && !island) return level.FindBridge(id);
        return null;
    }
}
