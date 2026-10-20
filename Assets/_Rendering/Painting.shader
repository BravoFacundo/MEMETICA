// Memetica / Painting
// The image or video on a canvas (VideoPlayer in Material Override mode, on _MainTex), painted on with brush strokes:
// Painted Mask (white = paint) cuts its border with dry, ragged strokes, so the canvas board shows around it.
// Where the paint gets thin (near the border) it fades a bit toward the canvas color, like dry brush.
// Cloth: the same weave as the board (PaintingCloth.cginc), showing through the paint, more where it is thin.
// Brush Strokes: dried paint over the image, catching the light as relief.
// Set from the Painting component on the painting (parent), not here: it writes these values.

Shader "Memetica/Painting"
{
    Properties
    {
        _MainTex ("Image / Video", 2D) = "white" {}

        [Header(Painted Mask)]
        _MaskTex ("Painted Mask (white = paint)", 2D) = "white" {}
        _Cutoff ("Paint Edge", Range(0.05, 0.95)) = 0.5
        _CanvasColor ("Canvas Color (thin paint)", Color) = (0.93, 0.92, 0.89, 1)
        _ThinPaint ("Thin Paint At The Border", Range(0, 1)) = 0.35

        [Header(Canvas Texture)]
        _CanvasTex ("Canvas Texture (R weave, G brush strokes)", 2D) = "gray" {}
        _ClothScale ("Cloth Weave Tiles Per Meter", Float) = 4
        _Weave ("Cloth Weave", Range(0, 1)) = 0.2
        _Strokes ("Brush Strokes", Range(0, 1)) = 0.15
        _StrokeScale ("Brush Stroke Size", Range(0.05, 1)) = 0.25
        _Relief ("Relief (catches the light)", Range(0, 2)) = 0.5

        [Header(Surface)]
        _Glossiness ("Smoothness", Range(0, 1)) = 0.1
        _Emission ("Self Light (seen in shadow)", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        #include "PaintingCloth.cginc"

        sampler2D _MainTex, _MaskTex;
        float4 _CanvasTex_TexelSize;
        fixed4 _CanvasColor;
        half _Cutoff, _ThinPaint, _Strokes, _StrokeScale, _Relief, _Glossiness, _Emission;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_MaskTex;
            float2 uv_CanvasTex;
            float2 clothUV;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.clothUV = ClothUV(v.vertex.xyz, v.normal);
        }

        // Dried paint: bigger than the threads, -0.5..0.5
        half StrokeHeight(float2 uv)
        {
            return (tex2D(_CanvasTex, uv * _StrokeScale).g - 0.5) * _Strokes;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half paint = tex2D(_MaskTex, IN.uv_MaskTex).r;
            clip(paint - _Cutoff);                                        // no paint: the board behind shows

            half thickness = saturate((paint - _Cutoff) / (1 - _Cutoff)); // 0 at the edge of a stroke, 1 where it is full
            fixed3 image = tex2D(_MainTex, IN.uv_MainTex).rgb;
            fixed3 col = lerp(image, _CanvasColor.rgb, (1 - thickness) * _ThinPaint);

            // The cloth under the paint (more where it is thin), and the dried strokes on top
            half weave = ClothWeave(IN.clothUV) * _Weave * lerp(2, 1, thickness);
            float2 uv = IN.uv_CanvasTex;
            half h = StrokeHeight(uv);
            half hx = StrokeHeight(uv + float2(_CanvasTex_TexelSize.x, 0));
            half hy = StrokeHeight(uv + float2(0, _CanvasTex_TexelSize.y));
            col *= 1 + (weave + h) * 2;

            o.Albedo = col;
            o.Normal = normalize(float3((h - hx) * _Relief * 8, (h - hy) * _Relief * 8, 1));
            o.Emission = col * _Emission;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
