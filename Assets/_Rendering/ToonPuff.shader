// Memetica / Toon Puff
// Flat cartoon puff particles (ToonPuff.png: cel shaded, outlined), tinted by the particle color, unlit like the orb.
// Used by the fog inside the orbs. Depth Pull: drawn as if that much closer to the camera (same place and size on
// screen), the same as the orb's sticker, so the puffs in front of the image cover it and the ones behind stay behind.

Shader "Memetica/ToonPuff"
{
    Properties
    {
        _MainTex ("Puff", 2D) = "white" {}
        _DepthPull ("Depth Pull (meters toward the camera)", Range(0, 0.5)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }   // just before the orb's glass
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _DepthPull;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                // Slides the vertex along its line of sight toward the camera: same look, only its depth changes
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 toVertex = world - _WorldSpaceCameraPos;
                float distance = length(toVertex);
                world = _WorldSpaceCameraPos + toVertex * (max(distance - _DepthPull, 0.05) / max(distance, 0.0001));
                o.pos = UnityWorldToClipPos(world);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
