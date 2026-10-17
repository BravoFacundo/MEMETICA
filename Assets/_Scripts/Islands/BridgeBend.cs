using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Bends the bridge between its two anchors (Bridge > Anchors) so each end stays on its island while they float and tilt.
// The bridge's two ends are pinned to the anchors (their world position, every frame) and a curve joins them,
// leaving each island the way that island is tilted right now. If an anchor isn't exactly at the end, the bridge stretches to it.
// It is always a single arch that bulges up (never an S, never sagging below the straight line).
// Every part of the bridge follows its point of that curve:
//   Meshes     every vertex bends (planks, ropes, railings together). The model needs Read/Write on in its import settings
//   Colliders  mesh colliders are cut into chunks along the bridge that move with the curve (what the player walks on)
//   Platforms  falling platforms follow it whole, as pieces (FallingPlatform asks for its place)
// The bend is worked out from where the bridge ends (its final place), so it rises or swings already bent
// and every animation lands right on the curve. It fades in as the animation starts, out as it goes away.
// Goes on the Bridge object. Anchors: at the bridge ends, inside each island's floating part (right click on Bridge > Create Anchors).
[DefaultExecutionOrder(-5)]   // after FloatMotion moved the islands, before the platforms and the player follow
[RequireComponent(typeof(Bridge))]
public class BridgeBend : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Seconds the bend takes to fade in when the bridge starts appearing (and out when it leaves)")]
    public float blendTime = 0.5f;
    [Tooltip("Pieces the collision is cut into along the bridge. More = follows the bend closer")]
    public int colliderChunks = 8;

    [Header("Debug")]
    [SerializeField] private float weight;

    class BentMesh
    {
        public Mesh mesh;
        public Matrix4x4 fromBridge;
        public Quaternion toBridgeRotation, fromBridgeRotation;
        public float[] t;
        public Vector3[] offsets, normals, vertices, outNormals;
        public Vector4[] tangents, outTangents;
    }

    class Chunk
    {
        public Transform transform;    // child of the bridge: its local space is the bridge space
        public Vector3 restPosition;
    }

    private Bridge bridge;
    private readonly List<BentMesh> meshes = new List<BentMesh>();
    private readonly List<Chunk> chunks = new List<Chunk>();
    private bool ready;

    // Curve in the bridge's space. Rest = straight, as designed
    private Vector3 restA, restB, axis;
    private float length;
    private Quaternion restRotationA, restRotationB;     // anchors' world rotation as designed
    private Vector3 targetA, targetB;                    // anchors now (frozen while it leaves)
    private Quaternion targetTurnA = Quaternion.identity, targetTurnB = Quaternion.identity;
    private Vector3 endA, endB, control;                 // curve this frame (one control point = one arch)
    private Quaternion turnA = Quaternion.identity, turnB = Quaternion.identity;
    private Vector3 appliedA, appliedB;
    private Quaternion appliedTurnA, appliedTurnB;

    public bool IsBending => ready && weight > 0f;

    // Start, before the Bridge's (execution order): the bridge is still where it was designed and nothing floats yet
    void Start()
    {
        bridge = GetComponent<Bridge>();
        if (bridge.anchorA == null || bridge.anchorB == null)
        {
            Debug.LogWarning($"[BridgeBend] '{name}' has no anchors: it won't bend (Bridge > right click > Create Anchors).", this);
            enabled = false;
            return;
        }

        // Straight bridge = from its own end to its own end. End A is the one closer to anchor A (seen from above)
        bridge.MeasureLocalEnds(out restA, out restB);
        if (FlatDistance(restA, bridge.anchorA) > FlatDistance(restB, bridge.anchorA)) (restA, restB) = (restB, restA);
        length = Vector3.Distance(restA, restB);
        if (length < 0.01f) { Debug.LogWarning($"[BridgeBend] '{name}': couldn't measure the bridge.", this); enabled = false; return; }
        axis = (restB - restA) / length;
        WarnIfNotFloating(bridge.anchorA);
        WarnIfNotFloating(bridge.anchorB);
        restRotationA = bridge.anchorA.rotation;
        restRotationB = bridge.anchorB.rotation;
        targetA = appliedA = endA = restA;
        targetB = appliedB = endB = restB;
        appliedTurnA = appliedTurnB = Quaternion.identity;
        control = (restA + restB) * 0.5f;   // straight until it is in place

        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            if (!IsPlatform(filter.transform)) PrepareMesh(filter);
        foreach (MeshCollider meshCollider in GetComponentsInChildren<MeshCollider>(true))
            if (!meshCollider.isTrigger && !IsPlatform(meshCollider.transform)) CutCollider(meshCollider);

        ready = true;
    }

    void Update()
    {
        if (!ready) return;

        bool appearing = bridge.IsUp && !bridge.IsWaiting;   // from the start of its animation, while it is up
        if (appearing) ReadAnchors();
        weight = Mathf.MoveTowards(weight, appearing ? 1f : 0f, Time.deltaTime / Mathf.Max(blendTime, 0.01f));

        Vector3 a = Vector3.Lerp(restA, targetA, weight);
        Vector3 b = Vector3.Lerp(restB, targetB, weight);
        Quaternion qa = Quaternion.Slerp(Quaternion.identity, targetTurnA, weight);
        Quaternion qb = Quaternion.Slerp(Quaternion.identity, targetTurnB, weight);
        if (!Changed(a, b, qa, qb)) return;

        endA = a; endB = b; turnA = qa; turnB = qb;
        control = ArchControl();
        appliedA = a; appliedB = b; appliedTurnA = qa; appliedTurnB = qb;

        foreach (BentMesh bent in meshes) ApplyMesh(bent);
        foreach (Chunk chunk in chunks) ApplyChunk(chunk);
    }

    //---- Used by FallingPlatform

    // A point and rotation of the straight bridge (bridge space) → where it goes on the bent bridge
    public void Deform(ref Vector3 position, ref Quaternion rotation)
    {
        float t = ParamOf(position);
        Quaternion turn = FrameAt(t);
        position = PointAt(t) + turn * (position - StraightAt(t));
        rotation = turn * rotation;
    }

    //---- Curve

    // The anchors as seen from the bridge's final place, not from where the animation has it now
    void ReadAnchors()
    {
        Quaternion finalRotation = bridge.FinalRotation;
        Matrix4x4 toBridge = Matrix4x4.TRS(bridge.FinalPosition, finalRotation, transform.lossyScale).inverse;
        Quaternion toBridgeRotation = Quaternion.Inverse(finalRotation);
        targetA = toBridge.MultiplyPoint3x4(bridge.anchorA.position);
        targetB = toBridge.MultiplyPoint3x4(bridge.anchorB.position);
        targetTurnA = toBridgeRotation * (bridge.anchorA.rotation * Quaternion.Inverse(restRotationA)) * finalRotation;
        targetTurnB = toBridgeRotation * (bridge.anchorB.rotation * Quaternion.Inverse(restRotationB)) * finalRotation;
    }

    bool Changed(Vector3 a, Vector3 b, Quaternion qa, Quaternion qb)
    {
        return (a - appliedA).sqrMagnitude > 0.000001f || (b - appliedB).sqrMagnitude > 0.000001f
            || Quaternion.Angle(qa, appliedTurnA) > 0.01f || Quaternion.Angle(qb, appliedTurnB) > 0.01f;
    }

    float ParamOf(Vector3 bridgePoint) => Mathf.Clamp01(Vector3.Dot(bridgePoint - restA, axis) / length);
    Vector3 StraightAt(float t) => restA + axis * (t * length);
    // How a slice of the bridge is turned at t: lined up with the curve, with each island's roll blended along it
    Quaternion FrameAt(float t)
    {
        Quaternion roll = Quaternion.Slerp(turnA, turnB, t);
        Vector3 tangent = 2f * (1f - t) * (control - endA) + 2f * t * (endB - control);
        if (tangent.sqrMagnitude < 0.000001f) return roll;
        return Quaternion.FromToRotation(roll * axis, tangent) * roll;
    }

    Vector3 PointAt(float t)
    {
        float u = 1f - t;
        return u * u * endA + 2f * u * t * control + t * t * endB;
    }

    // Where each island points the bridge, averaged, but never below the straight line between the ends:
    // one control point can't make an S, and keeping it up means the arch never sags
    Vector3 ArchControl()
    {
        float half = Vector3.Distance(endA, endB) * 0.5f;
        Vector3 fromA = endA + turnA * axis * half;
        Vector3 fromB = endB - turnB * axis * half;
        Vector3 point = (fromA + fromB) * 0.5f;
        float middleHeight = (endA.y + endB.y) * 0.5f;
        if (point.y < middleHeight) point.y = middleHeight;
        return point;
    }

    //---- Meshes: every vertex

    void PrepareMesh(MeshFilter filter)
    {
        Mesh source = filter.sharedMesh;
        if (source == null) return;
        if (!source.isReadable)
        {
            Debug.LogWarning($"[BridgeBend] '{filter.name}' can't bend: turn on Read/Write in the import settings of '{source.name}'.", filter);
            return;
        }

        Matrix4x4 toBridge = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        BentMesh bent = new BentMesh
        {
            mesh = Instantiate(source),
            fromBridge = toBridge.inverse,
            toBridgeRotation = Quaternion.Inverse(transform.rotation) * filter.transform.rotation,
        };
        bent.fromBridgeRotation = Quaternion.Inverse(bent.toBridgeRotation);
        bent.mesh.name = source.name + " (bent)";
        bent.mesh.MarkDynamic();

        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        Vector4[] tangents = source.tangents;
        int count = vertices.Length;
        bent.t = new float[count];
        bent.offsets = new Vector3[count];
        bent.vertices = new Vector3[count];
        bent.normals = new Vector3[normals.Length];
        bent.outNormals = new Vector3[normals.Length];
        bent.tangents = new Vector4[tangents.Length];
        bent.outTangents = new Vector4[tangents.Length];

        for (int i = 0; i < count; i++)
        {
            Vector3 point = toBridge.MultiplyPoint3x4(vertices[i]);
            bent.t[i] = ParamOf(point);
            bent.offsets[i] = point - StraightAt(bent.t[i]);
        }
        for (int i = 0; i < normals.Length; i++) bent.normals[i] = bent.toBridgeRotation * normals[i];
        for (int i = 0; i < tangents.Length; i++)
        {
            Vector3 tangent = bent.toBridgeRotation * (Vector3)tangents[i];
            bent.tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
        }

        filter.sharedMesh = bent.mesh;
        meshes.Add(bent);
    }

    void ApplyMesh(BentMesh bent)
    {
        for (int i = 0; i < bent.t.Length; i++)
        {
            float t = bent.t[i];
            Quaternion turn = FrameAt(t);
            bent.vertices[i] = bent.fromBridge.MultiplyPoint3x4(PointAt(t) + turn * bent.offsets[i]);
            if (i < bent.normals.Length) bent.outNormals[i] = bent.fromBridgeRotation * (turn * bent.normals[i]);
            if (i < bent.tangents.Length)
            {
                Vector3 tangent = bent.fromBridgeRotation * (turn * (Vector3)bent.tangents[i]);
                bent.outTangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, bent.tangents[i].w);
            }
        }
        bent.mesh.SetVertices(bent.vertices);
        if (bent.normals.Length > 0) bent.mesh.SetNormals(bent.outNormals);
        if (bent.tangents.Length > 0) bent.mesh.SetTangents(bent.outTangents);
    }

    //---- Colliders: chunks that move whole (rebuilding a mesh collider every frame is too slow)

    void CutCollider(MeshCollider source)
    {
        Mesh mesh = source.sharedMesh;
        if (mesh == null) return;
        if (!mesh.isReadable)
        {
            Debug.LogWarning($"[BridgeBend] Collider of '{source.name}' can't follow the bend: turn on Read/Write in the import settings of '{mesh.name}'.", source);
            return;
        }

        int count = Mathf.Max(colliderChunks, 1);
        Matrix4x4 toBridge = transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        // Each triangle goes to the chunk where its center is
        var pieces = new List<Vector3>[count];
        for (int i = 0; i < count; i++) pieces[i] = new List<Vector3>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = toBridge.MultiplyPoint3x4(vertices[triangles[i]]);
            Vector3 b = toBridge.MultiplyPoint3x4(vertices[triangles[i + 1]]);
            Vector3 c = toBridge.MultiplyPoint3x4(vertices[triangles[i + 2]]);
            int index = Mathf.Min((int)(ParamOf((a + b + c) / 3f) * count), count - 1);
            pieces[index].Add(a); pieces[index].Add(b); pieces[index].Add(c);
        }

        for (int i = 0; i < count; i++)
        {
            if (pieces[i].Count == 0) continue;
            Vector3 center = StraightAt((i + 0.5f) / count);

            var points = new Vector3[pieces[i].Count];
            var indices = new int[points.Length];
            for (int v = 0; v < points.Length; v++) { points[v] = pieces[i][v] - center; indices[v] = v; }
            Mesh piece = new Mesh { name = $"{mesh.name} chunk {i}", indexFormat = points.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            piece.SetVertices(points);
            piece.SetTriangles(indices, 0);

            GameObject chunkObject = new GameObject($"BendCollider_{i}");
            chunkObject.layer = source.gameObject.layer;
            chunkObject.tag = source.gameObject.tag;
            chunkObject.transform.SetParent(transform, false);   // straight under the bridge: no rotations or scales of the model in between
            MeshCollider chunkCollider = chunkObject.AddComponent<MeshCollider>();
            chunkCollider.sharedMesh = piece;
            chunkCollider.sharedMaterial = source.sharedMaterial;

            Chunk chunk = new Chunk { transform = chunkObject.transform, restPosition = center };
            ApplyChunk(chunk);
            chunks.Add(chunk);
        }

        DestroyImmediate(source);   // now, so the Bridge doesn't turn it back on
    }

    void ApplyChunk(Chunk chunk)
    {
        Vector3 position = chunk.restPosition;
        Quaternion rotation = Quaternion.identity;
        Deform(ref position, ref rotation);
        chunk.transform.localPosition = position;
        chunk.transform.localRotation = rotation;
    }

    float FlatDistance(Vector3 bridgePoint, Transform anchor)
    {
        Vector3 offset = transform.TransformPoint(bridgePoint) - anchor.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    void WarnIfNotFloating(Transform anchor)
    {
        if (anchor.GetComponentInParent<FloatMotion>() == null)
            Debug.LogWarning($"[BridgeBend] '{name}': anchor '{anchor.name}' is not inside a FloatMotion, so that end won't move with its island. Move it inside the island's Floating.", anchor);
    }

    bool IsPlatform(Transform part) => part.GetComponentInParent<FallingPlatform>(true) != null;

#if UNITY_EDITOR
    // Selected: the line between the anchors
    void OnDrawGizmosSelected()
    {
        Bridge b = GetComponent<Bridge>();
        if (b == null || b.anchorA == null || b.anchorB == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        if (Application.isPlaying && ready)
        {
            Vector3 previous = transform.TransformPoint(PointAt(0f));   // the curve it is following now
            for (int i = 1; i <= 16; i++)
            {
                Vector3 next = transform.TransformPoint(PointAt(i / 16f));
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
        else Gizmos.DrawLine(b.anchorA.position, b.anchorB.position);
        Gizmos.DrawWireSphere(b.anchorA.position, 0.3f);
        Gizmos.DrawWireSphere(b.anchorB.position, 0.3f);
    }
#endif
}
