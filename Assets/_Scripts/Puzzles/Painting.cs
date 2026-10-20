using UnityEngine;
using UnityEngine.Video;

// A painting, edited from here: the root of the painting (Painting_<KeyID>, with its Canvas, Easel, Chair…).
//   Board  (the Canvas, tagged Pintura, Memetica/PaintingCanvas, shared material): color and cloth, set per painting
//          on its renderer (not saved in the material), so every painting can differ.
//   Image  (inside the Canvas, with the VideoPlayer, Memetica/Painting): frame, mirror, paint edge, cloth and brush
//          strokes. Written to its material, which is the painting's own one: the same painting in both scenes
//          shares it, so set it in one scene only (the last one edited wins).
// A painting with no root (just the tagged canvas) carries it on the canvas itself.
// Masks in _Rendering: PaintingMask (brush strokes), PaintingFrame_Splatter, PaintingFrame_Torn, PaintingFrame_Wavy.
public class Painting : MonoBehaviour
{
    private static readonly int MaskId = Shader.PropertyToID("_MaskTex");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int ThinPaintId = Shader.PropertyToID("_ThinPaint");
    private static readonly int CanvasColorId = Shader.PropertyToID("_CanvasColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ClothScaleId = Shader.PropertyToID("_ClothScale");
    private static readonly int WeaveId = Shader.PropertyToID("_Weave");
    private static readonly int StrokesId = Shader.PropertyToID("_Strokes");
    private static readonly int StrokeScaleId = Shader.PropertyToID("_StrokeScale");
    private static readonly int ReliefId = Shader.PropertyToID("_Relief");

    [Section("Frame")]
    [Tooltip("White = paint. Any of the PaintingFrame textures, or PaintingMask")]
    public Texture2D frame;
    public bool mirrorX;
    public bool mirrorY;
    [Tooltip("Higher = the frame eats more of the image, lower = it grows toward the border")]
    [Range(0.05f, 0.95f)] public float paintEdge = 0.5f;
    [Tooltip("How much the paint fades toward the canvas color near the frame")]
    [Range(0f, 1f)] public float thinPaint = 0.35f;

    [Section("Canvas")]
    public Color canvasColor = new Color(0.93f, 0.92f, 0.89f);
    [Tooltip("Cloth weave tiles per meter: higher = finer threads")]
    public float clothScale = 4f;
    [Tooltip("How much the cloth weave shows, on the board and through the paint")]
    [Range(0f, 1f)] public float weave = 0.2f;
    [Tooltip("Dried brush strokes over the image")]
    [Range(0f, 1f)] public float brushStrokes = 0.15f;
    [Range(0.05f, 1f)] public float strokeSize = 0.25f;
    [Tooltip("How much the brush strokes catch the light")]
    [Range(0f, 2f)] public float relief = 0.5f;

    [Section("References")]
    [Tooltip("The canvas board (the object tagged Pintura; empty: only the image is styled)")]
    public Renderer board;
    [Tooltip("The image / video plane (the one with the VideoPlayer)")]
    public Renderer image;

    private MaterialPropertyBlock boardBlock;

    // Added to a painting: finds its parts and starts from what the image's material already has (changes nothing)
    private void Reset()
    {
        FindParts();
        ReadFromImage();
    }

    // The board is the canvas tagged Pintura (this object or one inside), the image the one with the VideoPlayer
    public void FindParts()
    {
        board = null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.CompareTag("Pintura") && t.TryGetComponent(out Renderer canvas)) { board = canvas; break; }
        VideoPlayer video = GetComponentInChildren<VideoPlayer>(true);
        image = video != null ? video.GetComponent<Renderer>() : null;
    }

    public void ReadFromImage()
    {
        Material material = image != null ? image.sharedMaterial : null;
        if (material == null || !material.HasProperty(MaskId)) return;
        frame = material.GetTexture(MaskId) as Texture2D;
        Vector2 scale = material.GetTextureScale(MaskId);
        mirrorX = scale.x < 0f;
        mirrorY = scale.y < 0f;
        paintEdge = material.GetFloat(CutoffId);
        thinPaint = material.GetFloat(ThinPaintId);
        canvasColor = material.GetColor(CanvasColorId);
        if (material.HasProperty(WeaveId)) weave = material.GetFloat(WeaveId);
        if (material.HasProperty(StrokesId)) brushStrokes = material.GetFloat(StrokesId);
        if (material.HasProperty(StrokeScaleId)) strokeSize = material.GetFloat(StrokeScaleId);
        if (material.HasProperty(ReliefId)) relief = material.GetFloat(ReliefId);
    }

    private void Awake() => ApplyBoard();   // the board's values live on the renderer: set again when the game starts

    private void OnValidate()
    {
        ApplyImage();
        ApplyBoard();
    }

    public void ApplyImage()
    {
        Material material = image != null ? image.sharedMaterial : null;
        if (frame == null || material == null) return;
        if (!material.HasProperty(MaskId))
        {
            Debug.LogWarning($"[Painting] {name}: the image material '{material.name}' doesn't use the Memetica/Painting shader.", this);
            return;
        }

        // Tiling -1 / Offset 1 mirrors the mask on that axis
        Vector2 scale = new Vector2(mirrorX ? -1f : 1f, mirrorY ? -1f : 1f);
        Vector2 offset = new Vector2(mirrorX ? 1f : 0f, mirrorY ? 1f : 0f);
        bool changed = false;
        if (material.GetTexture(MaskId) != frame) { material.SetTexture(MaskId, frame); changed = true; }
        if (material.GetTextureScale(MaskId) != scale) { material.SetTextureScale(MaskId, scale); changed = true; }
        if (material.GetTextureOffset(MaskId) != offset) { material.SetTextureOffset(MaskId, offset); changed = true; }
        if (material.GetColor(CanvasColorId) != canvasColor) { material.SetColor(CanvasColorId, canvasColor); changed = true; }
        changed |= SetFloat(material, CutoffId, paintEdge);
        changed |= SetFloat(material, ThinPaintId, thinPaint);
        changed |= SetFloat(material, ClothScaleId, clothScale);
        changed |= SetFloat(material, WeaveId, weave);
        changed |= SetFloat(material, StrokesId, brushStrokes);
        changed |= SetFloat(material, StrokeScaleId, strokeSize);
        changed |= SetFloat(material, ReliefId, relief);
#if UNITY_EDITOR
        if (changed) UnityEditor.EditorUtility.SetDirty(material);   // nothing new (scene load): the material stays clean
#endif
    }

    public void ApplyBoard()
    {
        if (board == null) return;
        if (boardBlock == null) boardBlock = new MaterialPropertyBlock();
        board.GetPropertyBlock(boardBlock);
        boardBlock.SetColor(ColorId, canvasColor);
        boardBlock.SetFloat(WeaveId, weave);
        boardBlock.SetFloat(ClothScaleId, clothScale);
        board.SetPropertyBlock(boardBlock);
    }

    private static bool SetFloat(Material material, int id, float value)
    {
        if (!material.HasProperty(id) || Mathf.Approximately(material.GetFloat(id), value)) return false;
        material.SetFloat(id, value);
        return true;
    }
}
