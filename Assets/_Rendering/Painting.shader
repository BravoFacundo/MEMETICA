// Memetica / Painting
// The image or video on a canvas (VideoPlayer in Material Override mode, on _MainTex), painted on with brush strokes:
// Painted Mask (white = paint) cuts its border with dry, ragged strokes, so the real canvas shows around it.
// Where the paint gets thin (near the border) it fades a bit toward the canvas color, like dry brush.
// Canvas Texture (subtle): the cloth weave and dried brush strokes over the image, darkening it a little and
// catching the light as relief. The weave shows more where the paint is thin.
// The mask belongs to the material, so it works the same for the first-frame image and for the video.
// Tiling -1 / Offset 1 on the mask mirrors it: each painting can look different with the same texture.

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
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex, _MaskTex, _CanvasTex;
        float4 _CanvasTex_TexelSize;
        fixed4 _CanvasColor;
        half _Cutoff, _ThinPaint, _Weave, _Strokes, _StrokeScale, _Relief, _Glossiness, _Emission;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_MaskTex;
            float2 uv_CanvasTex;
        };

        // -0.5..0.5 around mid gray: how much the cloth and the dried paint stand out here
        float CanvasHeight(float2 uv, half weave)
        {
            half w = tex2D(_CanvasTex, uv).r - 0.5;
            half s = tex2D(_CanvasTex, uv * _StrokeScale).g - 0.5;   // strokes are bigger than the threads
            return w * weave + s * _Strokes;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half paint = tex2D(_MaskTex, IN.uv_MaskTex).r;
            clip(paint - _Cutoff);                                        // no paint: the canvas behind shows

            half thickness = saturate((paint - _Cutoff) / (1 - _Cutoff)); // 0 at the edge of a stroke, 1 where it is full
            fixed3 image = tex2D(_MainTex, IN.uv_MainTex).rgb;
            fixed3 col = lerp(image, _CanvasColor.rgb, (1 - thickness) * _ThinPaint);

            // Cloth and dried paint: shade the color, and bend the normal so they catch the light
            half weave = _Weave * lerp(2, 1, thickness);                  // thin paint shows more cloth
            float2 uv = IN.uv_CanvasTex;
            float h = CanvasHeight(uv, weave);
            float hx = CanvasHeight(uv + float2(_CanvasTex_TexelSize.x, 0), weave);
            float hy = CanvasHeight(uv + float2(0, _CanvasTex_TexelSize.y), weave);
            col *= 1 + h * 2;

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
