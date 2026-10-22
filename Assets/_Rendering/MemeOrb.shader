// Memetica / Meme Orb
// The orb's shell, drawn instead of real glass (before: black transparent Standard, it looked dark):
// a thin ink outline, a toon rim of light, a barely tinted body you see through, and a painted highlight
// (the cartoon glass "comma") that always faces the camera. Same for every orb: they differ only by their image.
// No shadow of its own: the sticker inside casts it.
// Frost: during a change the glass clouds over, grey and milky (MemeOrb sets _Frost 0..1 on each orb), while a fog
// swirls inside (MemeOrb's particles). Thick enough, the image swaps unseen, then both clear. The highlight stays on top.

Shader "Memetica/MemeOrb"
{
    Properties
    {
        [Header(Body)]
        _BodyColor ("Body (alpha = how much it shows)", Color) = (0.9, 0.96, 1, 0.1)
        _ShadeSide ("Darker Away From The Sun", Range(0, 1)) = 0.35

        [Header(Rim)]
        _RimColor ("Rim (alpha = strength)", Color) = (1, 1, 1, 0.65)
        _RimWidth ("Rim Width", Range(0, 0.6)) = 0.22

        [Header(Ink Outline)]
        _InkColor ("Ink", Color) = (0.13, 0.11, 0.16, 1)
        _InkWidth ("Ink Width", Range(0, 0.3)) = 0.07

        [Header(Highlight)]
        _HighlightColor ("Highlight", Color) = (1, 1, 1, 0.95)
        _HighlightPos ("Highlight Position (view, -1..1)", Vector) = (-0.45, 0.48, 0, 0)
        _HighlightSize ("Highlight Size", Range(0, 0.5)) = 0.085
        _HighlightStretch ("Highlight Stretch", Range(1, 3)) = 1.8
        _DotPos ("Small Dot Position (view, -1..1)", Vector) = (-0.2, 0.66, 0, 0)
        _DotSize ("Small Dot Size", Range(0, 0.2)) = 0.03

        [Header(Frost)]
        _FrostColor ("Frosted Glass (alpha = how opaque)", Color) = (0.55, 0.57, 0.62, 0.65)
        _FrostRimColor ("Rim While Frosted", Color) = (0.72, 0.74, 0.78, 1)
        _Frost ("Frost Amount (set by code)", Range(0, 1)) = 0
        _FrostDetail ("Frost Detail", Float) = 4
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _BodyColor, _RimColor, _InkColor, _HighlightColor, _FrostColor, _FrostRimColor;
            half _ShadeSide, _RimWidth, _InkWidth, _HighlightSize, _HighlightStretch, _DotSize;
            half _Frost, _FrostDetail;
            float4 _HighlightPos, _DotPos;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // 1 inside a shape whose distance is d (crisp but antialiased)
            half Inside (half d)
            {
                half w = max(fwidth(d), 1e-4);
                return saturate(0.5 - d / w);
            }

            // Soft value noise (two layers) for the frost
            float Hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            float Fbm (float2 p) { return Noise(p) * 0.65 + Noise(p * 2.13 + 7.1) * 0.35; }

            // Painted highlight: an ellipse turned toward the center of the orb, on the view-space normal
            half Ellipse (float2 p, float2 center, half size, half stretch)
            {
                float2 d = p - center;
                float2 along = normalize(-center + 1e-4);                   // its long side goes around the orb
                float2 q = float2(dot(d, float2(-along.y, along.x)) / stretch, dot(d, along));
                return Inside(length(q) - size);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 v = normalize(i.viewDir);
                half edge = 1 - saturate(dot(n, v));                        // 0 facing you, 1 at the silhouette
                half sun = dot(n, normalize(_WorldSpaceLightPos0.xyz)) * 0.5 + 0.5;
                half shade = lerp(1 - _ShadeSide, 1, sun);

                fixed4 col = fixed4(_BodyColor.rgb * shade, _BodyColor.a);

                half rim = Inside((1 - _InkWidth - _RimWidth) - edge) * _RimColor.a * lerp(0.5, 1, sun);
                col.rgb = lerp(col.rgb, lerp(_RimColor.rgb, _FrostRimColor.rgb, saturate(_Frost)), rim);   // white → light grey while frosted
                col.a = max(col.a, rim);

                float2 nv = mul((float3x3)UNITY_MATRIX_V, n).xy;   // where on the orb's disc, as you see it

                // Frosted glass: clouds over evenly, a bit patchy, as the change goes on
                half frost = saturate(_Frost * (0.8 + 0.4 * Fbm(nv * _FrostDetail)));
                col.rgb = lerp(col.rgb, _FrostColor.rgb * shade, frost);
                col.a = lerp(col.a, max(col.a, _FrostColor.a), frost);

                half highlight = max(Ellipse(nv, _HighlightPos.xy, _HighlightSize, _HighlightStretch),
                                     Ellipse(nv, _DotPos.xy, _DotSize, 1)) * _HighlightColor.a;
                col.rgb = lerp(col.rgb, _HighlightColor.rgb, highlight);
                col.a = max(col.a, highlight);

                half ink = Inside((1 - _InkWidth) - edge) * _InkColor.a;
                col.rgb = lerp(col.rgb, _InkColor.rgb, ink);
                col.a = max(col.a, ink);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
