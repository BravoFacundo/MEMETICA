// Memetica / Meme Sticker
// The image inside an orb, as a die-cut sticker: a white border that follows the image's shape (from its alpha),
// a thin ink line around it like the orb's outline, and a slight bulge, as seen through the glass.
// Unlit, so the image always reads well. _EmissionColor brightens it (MemeOrb's flash sets it).
// The sticker casts the orb's shadow.
// Depth Pull: it is drawn as if it were that much closer to the camera (same place and size on screen): it faces
// the camera from the middle of the orb, so grass or a rock under a resting orb used to cut its lower part.

Shader "Memetica/MemeSticker"
{
    Properties
    {
        _MainTex ("Image (alpha = shape)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Flash (MemeOrb sets it)", Color) = (0, 0, 0, 1)

        [Header(Sticker)]
        _Margin ("Space Around The Image", Range(0, 0.3)) = 0.07
        _Border ("White Border", Range(0, 0.15)) = 0.04
        _BorderColor ("Border Color", Color) = (1, 1, 1, 1)
        _Ink ("Ink Line", Range(0, 0.05)) = 0.012
        _InkColor ("Ink", Color) = (0.13, 0.11, 0.16, 1)
        _Bulge ("Bulge (seen through the glass)", Range(0, 1)) = 0.35
        _Cutoff ("Shape Threshold", Range(0.05, 0.95)) = 0.5
        _DepthPull ("Depth Pull (meters toward the camera)", Range(0, 0.5)) = 0.2
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Unlit noforwardadd noambient nolightmap addshadow vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color, _EmissionColor, _BorderColor, _InkColor;
        half _Margin, _Border, _Ink, _Bulge, _Cutoff;
        float _DepthPull;

        // Slides each vertex along its own line of sight toward the camera: it looks exactly the same,
        // but it is in front of what pokes into the orb (not in the shadow pass: the shadow stays where it is)
        void vert (inout appdata_full v)
        {
        #ifndef UNITY_PASS_SHADOWCASTER
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 toVertex = world - _WorldSpaceCameraPos;
            float distance = length(toVertex);
            world = _WorldSpaceCameraPos + toVertex * (max(distance - _DepthPull, 0.05) / max(distance, 0.0001));
            v.vertex = mul(unity_WorldToObject, float4(world, 1));
        #endif
        }

        struct Input
        {
            float2 uv_MainTex;
        };

        half4 LightingUnlit (SurfaceOutput s, half3 lightDir, half atten)
        {
            return half4(s.Albedo, s.Alpha);
        }

        // The image's alpha, 0 outside it; blurred a bit (mip bias) so the borders come out round and smooth
        half ShapeAt (float2 uv, half blur)
        {
            half inside = all(uv == saturate(uv));
            return tex2Dbias(_MainTex, float4(uv, 0, blur)).a * inside;
        }

        // The shape grown by radius: the most alpha around, in 12 directions
        half Grown (float2 uv, half radius)
        {
            half a = 0;
            [unroll] for (int k = 0; k < 12; k++)
            {
                float angle = k * 0.5235988;   // 30 degrees
                a = max(a, ShapeAt(uv + float2(cos(angle), sin(angle)) * radius, 2.5));
            }
            return a;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            // Bulge: the middle a bit bigger, the sides squeezed, like through the glass
            float2 c = IN.uv_MainTex - 0.5;
            c *= 1 + _Bulge * dot(c, c) * 2;
            // The image a bit smaller than the plane, so the border fits around it
            float2 uv = 0.5 + c * (1 + 2 * _Margin);

            fixed4 image = tex2D(_MainTex, uv) * _Color;
            image.a *= all(uv == saturate(uv));
            half border = Grown(uv, _Border);
            half ink = Grown(uv, _Border + _Ink);
            clip(ink - _Cutoff);

            half w = 0.08;
            fixed3 col = lerp(_InkColor.rgb, _BorderColor.rgb, smoothstep(_Cutoff - w, _Cutoff + w, border));
            col = lerp(col, image.rgb, smoothstep(_Cutoff - w, _Cutoff + w, image.a));

            o.Albedo = col * (1 + _EmissionColor.rgb);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
