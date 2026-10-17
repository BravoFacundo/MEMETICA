using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Dense grass painted on an island's ground. Positions are stored local to this object, so the grass rises with the island.
// Each card takes the ground color (texture/atlas x material color) and normal under it, so it always matches the island.
// Drawn with GPU instancing (no GameObjects), split in cells that are skipped when far or off screen.
[ExecuteAlways]
public class GrassField : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Material grassMaterial;
    [SerializeField] private float bladeWidth = 0.6f, bladeHeight = 0.45f;
    [SerializeField] private Vector2 scaleRange = new(0.7f, 1.3f);
    [SerializeField] private float shrinkStart = 30f, shrinkEnd = 45f;    // grass shrinks to nothing between these camera distances
    [SerializeField] private float thinDistance = 25f;         // farther than this, only part of the grass is drawn
    [SerializeField, Range(0.1f, 1f)] private float farDensity = 0.5f;
    [SerializeField] private float cellSize = 8f;

    [Header("Interaction")]
    [SerializeField] private float playerRadius = 0.8f;
    [SerializeField] private float orbRadius = 0.5f;

    [Header("Paint")]
    [SerializeField] private bool useChildMeshes = true;              // every mesh under this object is ground
    [SerializeField] private List<MeshFilter> extraGroundMeshes = new();
    [SerializeField, HideInInspector] private List<Collider> groundColliders = new();    // old setup, still read
    [SerializeField] private float testDensity = 6f;    // cards per square meter
    [SerializeField, Range(0, 1)] private float minUpNormal = 0.7f;
    [SerializeField] private bool onlyTopSurface = true;    // skip ground covered by rocks, props or other meshes

    [Header("Clearings")]
    [SerializeField] private bool clearAroundGameplay = true;    // pedestals, bridges, paintings, elevators
    [SerializeField] private float clearRadius = 0.8f;           // no grass at all
    [SerializeField] private float clearFalloff = 1.5f;          // sparse grass, getting denser
    [SerializeField] private List<Clearing> extraClearings = new();

    [Serializable]
    private class Clearing
    {
        public Transform target;
        public float radius = 0.5f;
        public float falloff = 1f;
    }

    [Header("Debug")]
    [SerializeField] private int instanceCount;

    [SerializeField, HideInInspector] private List<Vector4> points = new();    // xyz local position, w scale
    [SerializeField, HideInInspector] private List<Color> colors = new();      // ground color under each point
    [SerializeField, HideInInspector] private List<Vector3> normals = new();   // ground normal under each point (local)

    private class Cell
    {
        public Bounds bounds;
        public List<Matrix4x4[]> batches = new();
        public List<MaterialPropertyBlock> blocks = new();
    }

    private const int MaxBatch = 1023;
    private static readonly int GroundColorID = Shader.PropertyToID("_GroundColor");
    private static readonly int GroundNormalID = Shader.PropertyToID("_GroundNormal");
    private static readonly int FadeStartID = Shader.PropertyToID("_FadeStart");
    private static readonly int FadeEndID = Shader.PropertyToID("_FadeEnd");
    private static Mesh cardMesh;

    private const int MaxInteractors = 16;
    private static readonly Vector4[] interactorData = new Vector4[MaxInteractors];
    private static readonly List<Transform> orbs = new();
    private static Transform player;
    private static int lastInteractorFrame = -1;
    private readonly List<Cell> cells = new();
    private bool isDirty = true;

    private void OnEnable()
    {
        isDirty = true;
        Camera.onPreCull += DrawForCamera;
    }
    private void OnDisable() => Camera.onPreCull -= DrawForCamera;
    private void OnValidate() => isDirty = true;

    //-------------------------------------------------------------------------------------------------------------------

    private void DrawForCamera(Camera cam)
    {
        if (grassMaterial == null || points.Count == 0 || cam.cameraType == CameraType.Preview) return;
        if (isDirty || transform.hasChanged) RebuildCells();
        if (cardMesh == null) cardMesh = CreateCardMesh();
        UpdateInteractors();

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
        Vector3 camPos = cam.transform.position;

        foreach (Cell cell in cells)
        {
            float sqrDistance = cell.bounds.SqrDistance(camPos);
            if (sqrDistance > shrinkEnd * shrinkEnd) continue;
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, cell.bounds)) continue;

            // Cards were placed in random order, so drawing only the first part thins the cell evenly
            float density = sqrDistance > thinDistance * thinDistance ? farDensity : 1f;
            for (int i = 0; i < cell.batches.Count; i++)
            {
                int count = Mathf.CeilToInt(cell.batches[i].Length * density);
                Graphics.DrawMeshInstanced(cardMesh, 0, grassMaterial, cell.batches[i], count, cell.blocks[i], ShadowCastingMode.Off, true, gameObject.layer, cam);
            }
        }
    }

    private void RebuildCells()
    {
        isDirty = false;
        transform.hasChanged = false;
        cells.Clear();
        if (!grassMaterial.enableInstancing) grassMaterial.enableInstancing = true;

        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        var cellMatrices = new Dictionary<Vector2Int, List<Matrix4x4>>();
        var cellColors = new Dictionary<Vector2Int, List<Vector4>>();
        var cellNormals = new Dictionary<Vector2Int, List<Vector4>>();
        var cellBounds = new Dictionary<Vector2Int, Bounds>();
        Vector3 cardSize = new(bladeWidth, bladeHeight, bladeWidth);

        for (int i = 0; i < points.Count; i++)
        {
            Vector4 point = points[i];
            Vector3 worldPos = transform.TransformPoint(point);
            float yaw = Mathf.Abs(Mathf.Sin(point.x * 12.9898f + point.z * 78.233f) * 43758.5453f) % 1f * 360f;
            Matrix4x4 matrix = Matrix4x4.TRS(worldPos, Quaternion.Euler(0f, yaw, 0f), cardSize * point.w);
            Color color = i < colors.Count ? colors[i] : Color.green;
            Vector3 normal = i < normals.Count ? transform.TransformDirection(normals[i]).normalized : Vector3.up;

            Vector2Int key = new(Mathf.FloorToInt(worldPos.x / cellSize), Mathf.FloorToInt(worldPos.z / cellSize));
            if (!cellMatrices.ContainsKey(key))
            {
                cellMatrices[key] = new List<Matrix4x4>();
                cellColors[key] = new List<Vector4>();
                cellNormals[key] = new List<Vector4>();
                cellBounds[key] = new Bounds(worldPos, cardSize * 2f);
            }
            cellMatrices[key].Add(matrix);
            cellColors[key].Add(linear ? color.linear : color);
            cellNormals[key].Add(normal);

            Bounds bounds = cellBounds[key];
            bounds.Encapsulate(new Bounds(worldPos + Vector3.up * bladeHeight * 0.5f, cardSize * 2f));
            cellBounds[key] = bounds;
        }

        foreach (var pair in cellMatrices)
        {
            Cell newCell = new() { bounds = cellBounds[pair.Key] };
            for (int i = 0; i < pair.Value.Count; i += MaxBatch)
            {
                int count = Mathf.Min(MaxBatch, pair.Value.Count - i);
                newCell.batches.Add(pair.Value.GetRange(i, count).ToArray());

                MaterialPropertyBlock block = new();
                block.SetFloat(FadeStartID, shrinkStart);
                block.SetFloat(FadeEndID, shrinkEnd);
                block.SetVectorArray(GroundColorID, cellColors[pair.Key].GetRange(i, count));
                block.SetVectorArray(GroundNormalID, cellNormals[pair.Key].GetRange(i, count));
                newCell.blocks.Add(block);
            }
            cells.Add(newCell);
        }
        instanceCount = points.Count;
    }

    // Player and orbs, sent once per frame to every grass material
    private void UpdateInteractors()
    {
        if (lastInteractorFrame == Time.frameCount) return;
        lastInteractorFrame = Time.frameCount;

        if (!Application.isPlaying)
        {
            Shader.SetGlobalFloat("_GrassInteractorCount", 0);
            return;
        }

        if (player == null || Time.frameCount % 120 == 0) FindInteractors();

        int count = 0;
        if (player != null) interactorData[count++] = new Vector4(player.position.x, player.position.y, player.position.z, playerRadius);
        foreach (Transform orb in orbs)
        {
            if (count >= MaxInteractors) break;
            if (orb != null && orb.gameObject.activeInHierarchy) interactorData[count++] = new Vector4(orb.position.x, orb.position.y, orb.position.z, orbRadius);
        }

        Shader.SetGlobalVectorArray("_GrassInteractors", interactorData);
        Shader.SetGlobalFloat("_GrassInteractorCount", count);
    }

    private static void FindInteractors()
    {
        PlayerMove playerMove = FindObjectOfType<PlayerMove>();
        player = playerMove != null ? playerMove.transform : null;

        orbs.Clear();
        foreach (Meme meme in FindObjectsOfType<Meme>()) orbs.Add(meme.transform);
    }

    // Three quads crossed like a star; base at y = 0, tip at y = 1, normals up for soft even lighting
    private static Mesh CreateCardMesh()
    {
        var vertices = new List<Vector3>();
        var meshNormals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        for (int i = 0; i < 3; i++)
        {
            Quaternion rotation = Quaternion.Euler(0f, i * 60f, 0f);
            int start = vertices.Count;
            vertices.Add(rotation * new Vector3(-0.5f, 0f, 0f));
            vertices.Add(rotation * new Vector3(0.5f, 0f, 0f));
            vertices.Add(rotation * new Vector3(-0.5f, 1f, 0f));
            vertices.Add(rotation * new Vector3(0.5f, 1f, 0f));
            meshNormals.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
        }

        Mesh mesh = new() { name = "GrassCard", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices);
        mesh.SetNormals(meshNormals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    //-------------------------------------------------------------------------------------------------------------------

#if UNITY_EDITOR
    private readonly Dictionary<Texture, Texture2D> readableTextures = new();
    private int groundMisses;

    [ContextMenu("Test Fill")]
    public void TestFill()
    {
        Undo.RecordObject(this, "Grass Test Fill");
        Physics.SyncTransforms();
        points.Clear();
        colors.Clear();
        normals.Clear();
        groundMisses = 0;
        var clearings = CollectClearings();
        CreateRaycastColliders();
        int attemptsTotal = 0, missedMesh = 0, tooSteep = 0, covered = 0, cleared = 0;

        foreach (MeshCollider ground in raycastColliders.Keys)
        {
            Bounds bounds = ground.bounds;
            int attempts = Mathf.RoundToInt(bounds.size.x * bounds.size.z * testDensity);

            attemptsTotal += attempts;
            for (int i = 0; i < attempts; i++)
            {
                Vector3 origin = new(UnityEngine.Random.Range(bounds.min.x, bounds.max.x), bounds.max.y + 1f, UnityEngine.Random.Range(bounds.min.z, bounds.max.z));
                if (!ground.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, bounds.size.y + 2f)) { missedMesh++; continue; }
                if (hit.normal.y < minUpNormal) { tooSteep++; continue; }
                if (onlyTopSurface && !IsTopSurface(origin, hit.distance, out string coveredBy)) { covered++; lastCoveredBy = coveredBy; continue; }
                float clearingDensity = ClearingDensity(hit.point, clearings);
                if (UnityEngine.Random.value > clearingDensity) { cleared++; continue; }

                // Near a clearing the grass is also shorter, so it fades into it instead of ending in a wall
                float scale = UnityEngine.Random.Range(scaleRange.x, scaleRange.y) * Mathf.Lerp(0.4f, 1f, clearingDensity);
                Vector3 local = transform.InverseTransformPoint(hit.point);
                points.Add(new Vector4(local.x, local.y, local.z, scale));
                colors.Add(SampleGroundColor(hit));
                normals.Add(transform.InverseTransformDirection(hit.normal));
            }
        }

        int meshCount = raycastColliders.Count;
        DestroyRaycastColliders();
        FinishEdit();
        Debug.Log($"[Grass] {PathOf(transform)}: {points.Count} cards from {meshCount} ground meshes " +
                  $"({attemptsTotal} tries: {missedMesh} outside the mesh, {tooSteep} too steep, {covered} covered{(covered > 0 ? " by " + lastCoveredBy : "")}, {cleared} in clearings)", this);
        if (groundMisses > 0) Debug.LogWarning($"[Grass] {name}: {groundMisses} cards could not read the ground color (the ground mesh needs a MeshRenderer with a material)", this);
    }

    // Bounds of everything the grass should avoid, each with its own clear radius and falloff
    private List<(Bounds bounds, float radius, float falloff)> CollectClearings()
    {
        var clearings = new List<(Bounds, float, float)>();
        Bounds area = new(transform.position, Vector3.zero);
        foreach (MeshFilter ground in GetGroundMeshes())
            if (ground.TryGetComponent(out Renderer groundRenderer)) area.Encapsulate(groundRenderer.bounds);
        area.Expand(clearRadius + clearFalloff);

        if (clearAroundGameplay)
        {
            var targets = new List<Component>();
            targets.AddRange(FindObjectsOfType<Pedestal>());
            targets.AddRange(FindObjectsOfType<Bridge>());
            foreach (Pedestal pedestal in FindObjectsOfType<Pedestal>())
                if (pedestal.cluePainting != null) targets.Add(pedestal.cluePainting);
            targets.AddRange(FindObjectsOfType<Elevator>());

            // Each mesh piece clears its own spot, so a long or diagonal bridge clears only under its planks, not a whole box
            foreach (Component target in targets)
            {
                if (transform.IsChildOf(target.transform)) continue;    // the grass grows on it (elevator, bridge): don't clear it
                foreach (MeshRenderer piece in target.GetComponentsInChildren<MeshRenderer>())
                    if (piece.bounds.Intersects(area)) clearings.Add((piece.bounds, clearRadius, clearFalloff));
            }
        }

        foreach (Clearing clearing in extraClearings)
        {
            if (clearing.target == null) continue;
            foreach (Transform child in clearing.target.GetComponentsInChildren<Transform>())
            {
                if (child.GetComponent<Renderer>() == null && child.GetComponent<Collider>() == null) continue;
                clearings.Add((GetObjectBounds(child, false), clearing.radius, clearing.falloff));
            }
        }
        return clearings;
    }

    // 0 inside a clearing, rising smoothly to 1 at radius + falloff
    private static float ClearingDensity(Vector3 point, List<(Bounds bounds, float radius, float falloff)> clearings)
    {
        float density = 1f;
        foreach (var clearing in clearings)
        {
            Vector3 closest = clearing.bounds.ClosestPoint(point);
            float distance = new Vector2(point.x - closest.x, point.z - closest.z).magnitude;
            float t = Mathf.Clamp01((distance - clearing.radius) / Mathf.Max(clearing.falloff, 0.01f));
            density = Mathf.Min(density, t * t * (3f - 2f * t));
        }
        return density;
    }

    private static Bounds GetObjectBounds(Transform target, bool includeChildren = true)
    {
        Renderer[] renderers = includeChildren ? target.GetComponentsInChildren<Renderer>() : target.GetComponents<Renderer>();
        Collider[] colliders = includeChildren ? target.GetComponentsInChildren<Collider>() : target.GetComponents<Collider>();

        Bounds bounds = new(target.position, Vector3.zero);
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        foreach (Collider col in colliders) if (!col.isTrigger) bounds.Encapsulate(col.bounds);
        return bounds;
    }

    // Nothing but ground above this point: the first thing hit is one of the ground colliders, or at the same height
    private string lastCoveredBy;

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    private bool IsTopSurface(Vector3 origin, float groundDistance, out string coveredBy)
    {
        coveredBy = null;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit firstHit, groundDistance + 0.1f, ~0, QueryTriggerInteraction.Ignore)) return true;
        coveredBy = PathOf(firstHit.transform);
        if (firstHit.collider is MeshCollider meshCollider && raycastColliders.ContainsKey(meshCollider)) return true;
        return firstHit.distance > groundDistance - 0.05f;
    }

    // After changing the island's material or atlas: grass takes the new ground colors, same positions
    [ContextMenu("Refresh Colors")]
    private void RefreshColors()
    {
        Undo.RecordObject(this, "Grass Refresh Colors");
        Physics.SyncTransforms();
        colors.Clear();
        normals.Clear();
        CreateRaycastColliders();

        foreach (Vector4 point in points)
        {
            Vector3 worldPos = transform.TransformPoint(point);
            Color color = Color.green;
            Vector3 normal = Vector3.up;
            foreach (MeshCollider ground in raycastColliders.Keys)
            {
                if (ground.Raycast(new Ray(worldPos + Vector3.up * 0.5f, Vector3.down), out RaycastHit hit, 1f))
                {
                    color = SampleGroundColor(hit);
                    normal = hit.normal;
                    break;
                }
            }
            colors.Add(color);
            normals.Add(transform.InverseTransformDirection(normal));
        }

        DestroyRaycastColliders();
        FinishEdit();
    }

    [ContextMenu("Clear")]
    private void ClearGrass()
    {
        Undo.RecordObject(this, "Grass Clear");
        points.Clear();
        colors.Clear();
        normals.Clear();
        FinishEdit();
    }

    private void FinishEdit()
    {
        foreach (Texture2D copy in readableTextures.Values) DestroyImmediate(copy);
        readableTextures.Clear();

        isDirty = true;
        instanceCount = points.Count;
        EditorUtility.SetDirty(this);
    }

    private Color SampleGroundColor(RaycastHit hit)
    {
        MeshCollider meshCollider = hit.collider as MeshCollider;
        MeshRenderer renderer = meshCollider != null && raycastColliders.TryGetValue(meshCollider, out MeshFilter ground) ? ground.GetComponent<MeshRenderer>() : null;
        if (renderer == null)
        {
            groundMisses++;
            return Color.green;
        }

        Material material = renderer.sharedMaterials[Mathf.Min(GetSubMeshIndex(meshCollider.sharedMesh, hit.triangleIndex), renderer.sharedMaterials.Length - 1)];
        if (material == null) return Color.green;

        Color color = material.HasProperty("_Color") ? material.color : Color.white;
        if (material.mainTexture == null) return color;

        Vector2 uv = hit.textureCoord * material.mainTextureScale + material.mainTextureOffset;
        return GetReadableTexture(material.mainTexture).GetPixelBilinear(uv.x, uv.y) * color;
    }

    // The grass meshes don't need colliders of their own (the player and orbs can go through them):
    // hidden temporary MeshColliders are created only while placing, then removed
    private readonly Dictionary<MeshCollider, MeshFilter> raycastColliders = new();

    private List<MeshFilter> GetGroundMeshes()
    {
        var meshes = new List<MeshFilter>();
        if (useChildMeshes) meshes.AddRange(GetComponentsInChildren<MeshFilter>());
        meshes.AddRange(extraGroundMeshes);
        foreach (Collider oldCollider in groundColliders)
            if (oldCollider != null && oldCollider.TryGetComponent(out MeshFilter filter)) meshes.Add(filter);
        return meshes.Where(m => m != null && m.sharedMesh != null).Distinct().ToList();
    }

    private void CreateRaycastColliders()
    {
        DestroyRaycastColliders();
        foreach (MeshFilter ground in GetGroundMeshes())
        {
            GameObject temporary = new("GrassRaycast") { hideFlags = HideFlags.HideAndDontSave };
            temporary.transform.SetParent(ground.transform, false);
            MeshCollider raycastCollider = temporary.AddComponent<MeshCollider>();
            raycastCollider.sharedMesh = ground.sharedMesh;
            raycastColliders[raycastCollider] = ground;
        }
        Physics.SyncTransforms();
    }

    private void DestroyRaycastColliders()
    {
        foreach (MeshCollider raycastCollider in raycastColliders.Keys)
            if (raycastCollider != null) DestroyImmediate(raycastCollider.gameObject);
        raycastColliders.Clear();
    }

    private static int GetSubMeshIndex(Mesh mesh, int triangleIndex)
    {
        int index = triangleIndex * 3;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            SubMeshDescriptor subMesh = mesh.GetSubMesh(i);
            if (index >= subMesh.indexStart && index < subMesh.indexStart + subMesh.indexCount) return i;
        }
        return 0;
    }

    // Atlas textures are not Read/Write: copy them through the GPU once per fill
    private Texture2D GetReadableTexture(Texture source)
    {
        if (readableTextures.TryGetValue(source, out Texture2D copy)) return copy;

        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, temporary);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;

        copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        readableTextures[source] = copy;
        return copy;
    }
#endif
}
