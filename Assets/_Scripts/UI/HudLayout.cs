using System.Collections.Generic;
using UnityEngine;

// The player's pivots laid out on the HUD canvas. Put this on the HUD canvas (or an object inside it): each image
// (Image, RawImage, any rect) named like a pivot places it, so it shows at that place on the screen:
//   DropPivot   where a carried meme is let go (its middle, at Drop Distance)
//   GrabPivot   where the other memes (the orb) are carried (the distance where a window Window Height tall looks
//               as tall as the image)
// (The meme window doesn't need one: it goes to its HUD version, HudWindow.)
// Live: it follows the images while you edit. Playing, its images are hidden and it places the pivots again from
// them at the game's real resolution (and whenever it changes): anchor each image as it should stay. It can be inside
// another canvas: the screen is its root canvas. It must be in the build (not EditorOnly).
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class HudLayout : MonoBehaviour
{
    [Tooltip("The 3D elements follow the images while you edit")]
    public bool live = true;
    [Tooltip("Meters from the camera of the Drop Pivot")]
    public float dropDistance = 1f;
    [Tooltip("Meters tall of the meme window (set by the layout canvas menu, from the MemeWindow prefab)")]
    public float windowHeight = 0.7f;

    public enum Kind { Grab, Drop }

    public class Element
    {
        public string name;
        public Transform target;
        public Camera camera;
        public float height;   // meters tall at scale 1
        public Kind kind;
    }

    private Vector2Int placedFor;   // the screen size it last placed everything for (playing)
    private bool hidden;

    void Update()
    {
        if (Application.isPlaying) { WhilePlaying(); return; }
#if UNITY_EDITOR
        if (!live) return;
        bool changed = false;
        foreach (RectTransform child in transform) if (child.hasChanged) { changed = true; child.hasChanged = false; }
        if (changed) Apply(false);
#endif
    }

    // Playing: the images hidden, everything placed from them for this screen (again if its size changes)
    void WhilePlaying()
    {
        if (!hidden)
        {
            foreach (UnityEngine.UI.Graphic image in GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) image.enabled = false;
            hidden = true;
        }
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size == placedFor) return;
        Canvas.ForceUpdateCanvases();
        if (Apply(false) > 0) placedFor = size;
    }

    //---- Images → 3D

    // Puts each element where its image is. Returns how many
    public int Apply(bool recordUndo)
    {
        if (!Elements(out List<Element> elements)) return 0;
        RectTransform canvas = RootRect();
        Vector2 screen = ScreenSize();
        int placed = 0;

        foreach (Element e in elements)
        {
            RectTransform child = Image(e);
            if (child == null) continue;

            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = Normalized(canvas, corners[0]), max = Normalized(canvas, corners[2]);
            Vector2 center = (min + max) * 0.5f;
            float tall = Mathf.Max(max.y - min.y, 0.001f);   // of the screen

            float distance = e.kind == Kind.Grab ? e.height / (tall * 2f * HalfHeight(e.camera, 1f)) : dropDistance;
            float half = HalfHeight(e.camera, distance);
            var local = new Vector3((center.x - 0.5f) * 2f * half * screen.x / screen.y, (center.y - 0.5f) * 2f * half, distance);

#if UNITY_EDITOR
            if (recordUndo) UnityEditor.Undo.RecordObject(e.target, "Apply HUD layout");
#endif
            e.target.position = e.camera.transform.TransformPoint(local);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(e.target)) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(e.target);
                UnityEditor.EditorUtility.SetDirty(e.target);
            }
#endif
            placed++;
        }
        return placed;
    }

    //---- 3D → images

    // An image for each element that has none yet, where the element shows now (the ones you placed stay).
    // Texture: what the new image shows
    public void AddMissingImages(System.Func<Element, Texture> texture)
    {
        if (!Elements(out List<Element> elements)) return;
        RectTransform canvas = RootRect();
        Vector2 screen = ScreenSize();

        foreach (Element e in elements)
        {
            if (Image(e) != null) continue;
            RectTransform child;
            {
                var go = new GameObject(e.name, typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "HUD layout");
#endif
                go.transform.SetParent(transform, false);
                child = (RectTransform)go.transform;
                var image = go.GetComponent<UnityEngine.UI.RawImage>();
                image.raycastTarget = false;
                image.texture = texture(e);
                image.color = image.texture != null ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 0.3f, 0.2f, 0.85f);
            }

            Vector3 local = e.camera.transform.InverseTransformPoint(e.target.position);
            float half = HalfHeight(e.camera, Mathf.Max(local.z, 0.01f));
            float tall = e.kind == Kind.Drop ? 0.04f
                       : e.height * (e.kind == Kind.Grab ? 1f : e.target.localScale.y) / (2f * half);
            var center = new Vector2(local.x / (2f * half * screen.x / screen.y) + 0.5f, local.y / (2f * half) + 0.5f);
            Texture shown = child.TryGetComponent(out UnityEngine.UI.RawImage raw) ? raw.texture : null;
            float aspect = shown != null ? (float)shown.width / shown.height : child.rect.height > 0f ? child.rect.width / child.rect.height : 1f;

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(child, "HUD layout");
#endif
            float h = tall * canvas.rect.height;
            child.pivot = new Vector2(0.5f, 0.5f);
            child.sizeDelta = new Vector2(h * aspect, h) / Mathf.Max(child.lossyScale.y / canvas.lossyScale.y, 1e-4f);
            child.position = canvas.TransformPoint(new Vector3((center.x - 0.5f) * canvas.rect.width + canvas.rect.center.x,
                                                               (center.y - 0.5f) * canvas.rect.height + canvas.rect.center.y, 0f));
            child.hasChanged = false;
        }
    }

    //---- What there is to place

    public bool Elements(out List<Element> elements)
    {
        elements = new List<Element>();
        Camera main = Camera.main;
        if (main == null) return false;

        Transform grab = Find(main.transform, "GrabPivot"), drop = Find(main.transform, "DropPivot");
        if (grab != null) elements.Add(new Element { name = "GrabPivot", target = grab, camera = main, height = windowHeight, kind = Kind.Grab });
        if (drop != null) elements.Add(new Element { name = "DropPivot", target = drop, camera = main, kind = Kind.Drop });

        return true;
    }

    // The image that places it (named as it)
    RectTransform Image(Element e) => transform.Find(e.name) as RectTransform;

    //---- Measures

    // The whole screen: the root canvas (this one, or the one it is inside)
    RectTransform RootRect()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? (RectTransform)canvas.rootCanvas.transform : (RectTransform)transform;
    }

    static float HalfHeight(Camera camera, float distance) => distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

    // The screen's proportion as the canvas sees it (the Game view)
    Vector2 ScreenSize()
    {
        Vector2 size = RootRect().rect.size;
        return size.x > 1f && size.y > 1f ? size : new Vector2(1920f, 1080f);
    }

    static Vector2 Normalized(RectTransform canvas, Vector3 world)
    {
        Vector2 local = canvas.InverseTransformPoint(world);
        Rect r = canvas.rect;
        return new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
    }

    // Meters tall of what is drawn under it, at its own scale 1
    public static float Height(Transform root)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                float y = root.InverseTransformPoint(filter.transform.TransformPoint(corner)).y;
                min = Mathf.Min(min, y); max = Mathf.Max(max, y);
            }
        }
        return max > min ? max - min : 0.5f;
    }

    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        GameObject found = GameObject.Find(name);
        return found != null ? found.transform : null;
    }
}
