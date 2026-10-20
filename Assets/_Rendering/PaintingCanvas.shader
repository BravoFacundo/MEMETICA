// Memetica / Painting Canvas
// The bare canvas board of a painting: its color with the cloth weave (PaintingCloth.cginc), the same cloth that shows
// through the painted image. One shared material (PaintingCanvas); each painting sets its own color and weave
// from the Painting component (per renderer, without touching the material).

Shader "Memetica/PaintingCanvas"
{
    Properties
    {
        _Color ("Canvas Color", Color) = (0.93, 0.92, 0.89, 1)
        _CanvasTex ("Canvas Texture (R weave)", 2D) = "gray" {}
        _ClothScale ("Cloth Weave Tiles Per Meter", Float) = 4
        _Weave ("Cloth Weave", Range(0, 1)) = 0.2
        _Glossiness ("Smoothness", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        #include "PaintingCloth.cginc"

        fixed4 _Color;
        half _Glossiness;

        struct Input
        {
            float2 clothUV;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.clothUV = ClothUV(v.vertex.xyz, v.normal);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb * (1 + ClothWeave(IN.clothUV) * _Weave * 2);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
