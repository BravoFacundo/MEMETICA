// Memetica / Low Poly Smoke
// Solid, flat-shaded puffs (the particles are icosahedrons) lit by the sun like the clouds; the particle color tints them.
// Depth Pull: drawn as if that much closer to the camera (same place and size on screen). The fog inside an orb uses
// the same pull as its sticker, so the puffs in front of the image cover it and the ones behind stay behind.

Shader "Memetica/LowPolySmoke"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _DepthPull ("Depth Pull (meters toward the camera)", Range(0, 0.5)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0

        fixed4 _Color;
        float _DepthPull;

        struct Input
        {
            float4 color : COLOR;   // the particle's color
        };

        // Slides each vertex along its line of sight toward the camera: it looks the same, only its depth changes
        void vert (inout appdata_full v)
        {
        #ifndef UNITY_PASS_SHADOWCASTER
            if (_DepthPull > 0)
            {
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 toVertex = world - _WorldSpaceCameraPos;
                float distance = length(toVertex);
                world = _WorldSpaceCameraPos + toVertex * (max(distance - _DepthPull, 0.05) / max(distance, 0.0001));
                v.vertex = mul(unity_WorldToObject, float4(world, 1));
            }
        #endif
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb * _Color.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
