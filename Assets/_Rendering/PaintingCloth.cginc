// Memetica / Painting cloth, shared by Painting.shader (the image) and PaintingCanvas.shader (the board around it).
// The weave is projected on the object in meters, from its own pivot: it doesn't depend on the UVs or the scale of
// each mesh, so the board and the painted image show the same cloth at the same size, and it travels with the
// painting when its island floats (world space would make it slide).

#ifndef MEMETICA_PAINTING_CLOTH
#define MEMETICA_PAINTING_CLOTH

sampler2D _CanvasTex;      // R: cloth weave, G: dried brush strokes (mid gray = neutral)
half _Weave;               // how much the weave shades the color
float _ClothScale;         // weave tiles per meter

// Object position in meters, flattened on the side it faces (a board's front, a plane): the weave coordinates
float2 ClothUV(float3 positionOS, float3 normalOS)
{
    float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20),
                          length(unity_ObjectToWorld._m01_m11_m21),
                          length(unity_ObjectToWorld._m02_m12_m22));
    float3 p = positionOS * scale;
    float3 n = abs(normalOS);
    float2 uv = n.x >= n.y && n.x >= n.z ? p.zy : (n.y >= n.z ? p.xz : p.xy);
    return uv * _ClothScale;
}

// -0.5..0.5: threads up, gaps down
half ClothWeave(float2 clothUV)
{
    return tex2D(_CanvasTex, clothUV).r - 0.5;
}

#endif
