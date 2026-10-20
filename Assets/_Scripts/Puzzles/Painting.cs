using UnityEngine;

// The painted frame of a painting (the image/video plane, next to its VideoPlayer): which mask cuts its border
// and how it is mirrored. Chosen here and written to its material (Memetica/Painting shader), so it shows in the editor too.
// Masks in _Rendering: PaintingMask (brush strokes), PaintingFrame_Splatter, PaintingFrame_Torn, PaintingFrame_Wavy.
// The material is the one that keeps it: two paintings sharing a material share the frame (the last one edited wins),
// and the same painting in both scenes uses the same material, so set it in one scene only.
[RequireComponent(typeof(Renderer))]
public class Painting : MonoBehaviour
{
    private static readonly int MaskId = Shader.PropertyToID("_MaskTex");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

    [Section("Frame")]
    [Tooltip("White = paint. Any of the PaintingFrame textures, or PaintingMask")]
    public Texture2D frame;
    public bool mirrorX;
    public bool mirrorY;
    [Tooltip("Higher = the frame eats more of the image, lower = it grows toward the border")]
    [Range(0.05f, 0.95f)] public float paintEdge = 0.5f;

    // Added to a painting: starts from what its material already has, so adding it changes nothing
    private void Reset()
    {
        Material material = GetComponent<Renderer>().sharedMaterial;
        if (material == null || !material.HasProperty(MaskId)) return;
        frame = material.GetTexture(MaskId) as Texture2D;
        Vector2 scale = material.GetTextureScale(MaskId);
        mirrorX = scale.x < 0f;
        mirrorY = scale.y < 0f;
        paintEdge = material.GetFloat(CutoffId);
    }

    private void OnValidate()
    {
        Renderer surface = GetComponent<Renderer>();
        Material material = surface != null ? surface.sharedMaterial : null;
        if (frame == null || material == null) return;
        if (!material.HasProperty(MaskId))
        {
            Debug.LogWarning($"[Painting] {name}: its material '{material.name}' doesn't use the Memetica/Painting shader.", this);
            return;
        }

        // Tiling -1 / Offset 1 mirrors the mask on that axis
        Vector2 scale = new Vector2(mirrorX ? -1f : 1f, mirrorY ? -1f : 1f);
        Vector2 offset = new Vector2(mirrorX ? 1f : 0f, mirrorY ? 1f : 0f);
        if (material.GetTexture(MaskId) == frame && material.GetTextureScale(MaskId) == scale
            && material.GetTextureOffset(MaskId) == offset && Mathf.Approximately(material.GetFloat(CutoffId), paintEdge)) return;   // nothing new (scene load)

        material.SetTexture(MaskId, frame);
        material.SetTextureScale(MaskId, scale);
        material.SetTextureOffset(MaskId, offset);
        material.SetFloat(CutoffId, paintEdge);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(material);
#endif
    }
}
