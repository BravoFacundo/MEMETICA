// What the window tools share (MemeWindowPrefabTool, HudSetupTool): your PNG layers on quads, and the box behind
// them (the depth). Everything is built in "window space": +Z toward whoever looks at it, X to their right.
//   Analyze   reads a PNG: where it is opaque, how round its corners are, and the transparent hole in its middle
//   Layer     a quad showing one PNG (Unlit/Transparent, _Rendering/Windows/<png>.mat)
//   Back      a PNG named with Back / Reverse / Reverso: the box's back, a quad facing backward
//   Body      the rounded rectangle pushed back Depth Ratio x its width, all in the border's color
//             (_Rendering/Window_Sides.mat, #313131, lit; your Back PNG goes on its back). Its sides reach the front: the front layer sits flush on them (no gap to see through)
//             and the layers behind it are recessed inside, in front of an inner cap (nothing hollow shows through
//             the transparent parts). Its mesh: Art/Models/Windows/<name>_Body.asset. Change the materials' colors freely.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WindowBuilder
{
    public const float DepthRatio = 0.19f;   // depth = this x the window's width (0.62 m wide → 0.12 m deep)
    const string MaterialFolder = "Assets/_Rendering/Windows";
    const string MeshFolder = "Assets/Art/Models/Windows";
    const string SidesPath = "Assets/_Rendering/Window_Sides.mat";
    const int CornerSteps = 6;

    public struct Shape
    {
        public Vector2Int size;   // the PNG's pixels
        public RectInt opaque;    // where it is opaque (pixels from the bottom left)
        public int radius;        // its corners' radius (pixels)
        public bool hasHole;
        public RectInt hole;      // the transparent hole in its middle (pixels from the bottom left)
    }

    //---- Reading the PNG

    public static Shape Analyze(Texture2D asset)
    {
        var shape = new Shape();
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(asset)));
        int w = texture.width, h = texture.height;
        Color32[] pixels = texture.GetPixels32();
        Object.DestroyImmediate(texture);
        shape.size = new Vector2Int(w, h);
        bool Solid(int x, int y) => pixels[y * w + x].a > 127;

        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (Solid(x, y)) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
        if (maxX < 0) { shape.opaque = new RectInt(0, 0, w, h); return shape; }   // all transparent
        shape.opaque = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);

        // Corners: from each corner, steps along the diagonal until opaque (a round corner of radius r leaves
        // r (1 - 1/√2) of them). The average of the four (antialiasing would make the top row guess too small)
        int limit = Mathf.Min(shape.opaque.width, shape.opaque.height) / 2;
        int Steps(int x, int y, int dx, int dy)
        {
            int k = 0;
            while (k < limit && !Solid(x + dx * k, y + dy * k)) k++;
            return k;
        }
        float steps = (Steps(minX, maxY, 1, -1) + Steps(maxX, maxY, -1, -1) + Steps(minX, minY, 1, 1) + Steps(maxX, minY, -1, 1)) / 4f;
        shape.radius = Mathf.Clamp(Mathf.RoundToInt(steps / (1f - 1f / Mathf.Sqrt(2f))), 0, limit);

        // Hole: transparent at the middle → as far as it stays transparent each way
        int cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        if (!Solid(cx, cy))
        {
            int left = cx, right = cx, down = cy, up = cy;
            while (left > minX && !Solid(left - 1, cy)) left--;
            while (right < maxX && !Solid(right + 1, cy)) right++;
            while (down > minY && !Solid(cx, down - 1)) down--;
            while (up < maxY && !Solid(cx, up + 1)) up++;
            shape.hasHole = true;
            shape.hole = new RectInt(left, down, right - left + 1, up - down + 1);
        }
        return shape;
    }

    // A rect in pixels → its middle and size in meters, from the PNG's middle (x right, y up)
    public static void ToMeters(RectInt rect, Vector2Int size, float metersPerPixel, out Vector2 center, out Vector2 meters)
    {
        center = (rect.center - (Vector2)size * 0.5f) * metersPerPixel;
        meters = new Vector2(rect.width, rect.height) * metersPerPixel;
    }

    // Window space: +Z toward the viewer, X to their right (the quads are turned to face it)
    public static Vector3 InWindow(Vector2 viewer, float z) => new Vector3(-viewer.x, viewer.y, z);

    //---- Building

    public static void SetUpTexture(Texture2D texture)
    {
        if (!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is TextureImporter importer)) return;
        if (importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp && importer.npotScale == TextureImporterNPOTScale.None) return;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;   // any proportion, as drawn
        importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, 2048);
        importer.SaveAndReimport();
    }

    // _Rendering/Windows/<png>.mat: the PNG with its transparency
    public static Material LayerMaterial(Texture2D texture)
    {
        Folder(MaterialFolder);
        string path = $"{MaterialFolder}/{texture.name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Unlit/Transparent")) { name = texture.name, mainTexture = texture };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // A quad showing it, its front toward the viewer
    public static Transform Layer(Transform parent, string name, Material material, Vector2 center, Vector2 size, float z, int layer)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.layer = layer;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = InWindow(center, z);
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return quad.transform;
    }

    // A PNG for the box's back (its name says so)
    public static bool IsBack(Texture2D texture)
    {
        string n = texture.name.ToLowerInvariant();
        return n.Contains("back") || n.Contains("revers");
    }

    // The back PNG on the box's back, facing backward, a hair behind it (exported from the same Figma frame: centered)
    public static Transform BackLayer(Transform parent, Texture2D texture, float metersPerPixel, float depth, int layer)
    {
        Transform quad = Layer(parent, texture.name, LayerMaterial(texture), Vector2.zero,
                               new Vector2(texture.width, texture.height) * metersPerPixel, -depth / 2 - depth * 0.01f, layer);
        quad.localRotation = Quaternion.identity;   // seen from behind, not mirrored
        return quad;
    }

    // The box: from z -depth/2 (back) to +depth/2 (front: the front layer goes there), its inner cap Cap Depth behind the front
    public static Transform Body(Transform parent, string name, Vector2 center, Vector2 size, float radius, float depth, float capDepth, int layer)
    {
        var body = new GameObject("Body") { layer = layer };
        body.transform.SetParent(parent, false);
        body.transform.localPosition = InWindow(center, 0f);
        body.AddComponent<MeshFilter>().sharedMesh = BodyMesh(name, size.x, size.y, depth, capDepth, Mathf.Min(radius, Mathf.Min(size.x, size.y) * 0.5f));
        Material dark = Shared(SidesPath, "Legacy Shaders/Diffuse", new Color32(0x31, 0x31, 0x31, 0xFF));
        body.AddComponent<MeshRenderer>().sharedMaterials = new[] { dark, dark };   // the sides; the back and the inner cap
        return body.transform;
    }

    // The rounded rectangle (w x h, corner radius r) pushed back: its sides (submesh 0), its back and its inner cap (submesh 1)
    static Mesh BodyMesh(string name, float w, float h, float depth, float capDepth, float r)
    {
        var outline = new List<Vector2>();   // counterclockwise seen from the front
        Vector2[] centers = { new Vector2(w / 2 - r, h / 2 - r), new Vector2(-w / 2 + r, h / 2 - r),
                              new Vector2(-w / 2 + r, -h / 2 + r), new Vector2(w / 2 - r, -h / 2 + r) };
        int steps = r > 0.0001f ? CornerSteps : 0;
        for (int c = 0; c < 4; c++)
            for (int k = 0; k <= steps; k++)
            {
                float a = (c * 90f + (steps > 0 ? 90f * k / steps : 45f)) * Mathf.Deg2Rad;
                outline.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var sides = new List<int>();
        var back = new List<int>();
        float front = depth / 2, rear = -depth / 2;

        for (int i = 0; i < outline.Count; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
            if ((b - a).sqrMagnitude < 1e-10f) continue;
            Vector2 d = (b - a).normalized;
            var outward = new Vector3(d.y, -d.x, 0f);
            int v = vertices.Count;
            vertices.AddRange(new[] { new Vector3(a.x, a.y, front), new Vector3(b.x, b.y, front), new Vector3(b.x, b.y, rear), new Vector3(a.x, a.y, rear) });
            normals.AddRange(new[] { outward, outward, outward, outward });
            Facing(vertices, sides, outward, v, v + 1, v + 2);
            Facing(vertices, sides, outward, v, v + 2, v + 3);
        }

        int middle = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, rear)); normals.Add(Vector3.back);
        int first = vertices.Count;
        foreach (Vector2 p in outline) { vertices.Add(new Vector3(p.x, p.y, rear)); normals.Add(Vector3.back); }
        for (int i = 0; i < outline.Count; i++)
            Facing(vertices, back, Vector3.back, middle, first + i, first + (i + 1) % outline.Count);

        // Inner cap: behind the layers, facing the front
        float capZ = front - Mathf.Clamp(capDepth, 0f, depth);
        middle = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, capZ)); normals.Add(Vector3.forward);
        first = vertices.Count;
        foreach (Vector2 p in outline) { vertices.Add(new Vector3(p.x, p.y, capZ)); normals.Add(Vector3.forward); }
        for (int i = 0; i < outline.Count; i++)
            Facing(vertices, back, Vector3.forward, middle, first + i, first + (i + 1) % outline.Count);

        Folder(MeshFolder);
        string path = $"{MeshFolder}/{name}_Body.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear();
        mesh.name = name + "_Body";
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(sides, 0);
        mesh.SetTriangles(back, 1);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // A triangle wound so it is seen from the side 'facing' points to
    static void Facing(List<Vector3> v, List<int> tris, Vector3 facing, int a, int b, int c)
    {
        bool toward = Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), facing) > 0f;
        tris.AddRange(toward ? new[] { a, b, c } : new[] { a, c, b });
    }

    // A shared material, made with that color if it isn't there (a color you changed stays)
    static Material Shared(string path, string shaderName, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find(shaderName)) { name = Path.GetFileNameWithoutExtension(path), color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    public static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
