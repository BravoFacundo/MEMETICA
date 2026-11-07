// Memetica / Sky Gradient
// The skybox drawn from the sky's own gradient picture (rpgpp_lt_sky_a: the color of the sky from the horizon, at its
// bottom, up to the top of the sky, at its top), the same picture the old sky domes showed. By how high you look:
// straight ahead the horizon color, straight up the top color. Below the horizon it is blue too (Mirror: looking down
// is like looking up, the islands float in the middle of the sky); off: the horizon color all the way down.
// Lighting > Environment > Skybox Material (Memetica > Sky > Use Sky Gradient As Skybox sets it up).

Shader "Memetica/Sky Gradient"
{
    Properties
    {
        _MainTex ("Gradient (bottom: horizon, top: up)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 3)) = 1
        _Curve ("Curve (higher: the horizon color reaches higher)", Range(0.2, 4)) = 1
        _Horizon ("Horizon height (-: lower)", Range(-0.5, 0.5)) = 0
        [Toggle] _Mirror ("Below the horizon: blue down to the bottom (mirrored)", Float) = 1
        [Toggle] _Flip ("Upside down? (the top color at the horizon)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half4 _Tint;
            half _Exposure, _Curve, _Horizon, _Mirror, _Flip;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;   // (a skybox: its vertices are directions)
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float y = normalize(i.dir).y - _Horizon;
                y = _Mirror > 0.5 ? abs(y) : saturate(y);
                float height = pow(saturate(asin(saturate(y)) / 1.5707963), _Curve);   // 0 horizon, 1 straight up (by angle)
                if (_Flip > 0.5) height = 1 - height;
                float edge = _MainTex_TexelSize.y * 0.5;   // (never past the picture's edge: it would wrap to the other end)
                half3 color = tex2D(_MainTex, float2(0.5, clamp(height, edge, 1 - edge))).rgb;
                return half4(color * _Tint.rgb * _Exposure, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
