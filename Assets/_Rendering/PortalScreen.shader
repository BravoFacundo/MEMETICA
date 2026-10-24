// The portal window's screen (PortalWindow): what a camera above the hub sees, drawn where it falls on your screen, so
// it looks like a hole into the sky above the hub rather than a picture. On top: the empty meme window's PNG while it
// loads (or won't take you), pixels (an old image loading), rings that bend the view around you while you stand on
// it, faint scanlines and darker edges (a screen, an OS window).
Shader "Memetica/PortalScreen"
{
    Properties
    {
        _MainTex ("View (set while playing)", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _EmptyTex ("Empty (the empty meme window's PNG)", 2D) = "gray" {}
        _Static ("Empty Shown", Range(0, 1)) = 1
        _Pixelate ("Pixelate", Range(0, 1)) = 0
        _Ripple ("Ripple", Range(0, 1)) = 0
        _RippleCenter ("Ripple Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _Scanlines ("Scanlines", Range(0, 1)) = 0.12
        _Edge ("Dark Edges", Range(0, 1)) = 0.4
        _FlipY ("Flip View (upside down?)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _EmptyTex;
            float4 _EmptyTex_ST;
            float _Pixelate;
            fixed4 _Tint;
            float _Static, _Ripple, _Scanlines, _Edge, _FlipY;
            float4 _RippleCenter;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; float2 uv : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.pos);
                o.uv = v.uv;
                return o;
            }


            fixed4 frag (v2f i) : SV_Target
            {
                float2 s = i.screen.xy / i.screen.w;
                s.y = lerp(s.y, 1 - s.y, _FlipY);

                // Rings around where you stand, bending the view
                float2 d = i.uv - _RippleCenter.xy;
                float r = length(d);
                float wave = sin(r * 45 - _Time.y * 10) * exp(-r * 5) * _Ripple * 0.012;
                s += (r > 0.0001 ? d / r : 0) * wave;

                // Pixels: big squares on the screen, smaller as it loads (0: sharp)
                if (_Pixelate > 0.001)
                {
                    float cells = lerp(360, 12, _Pixelate * _Pixelate);
                    float2 count = float2(cells * _ScreenParams.x / _ScreenParams.y, cells);
                    s = (floor(s * count) + 0.5) / count;
                }
                fixed4 view = tex2D(_MainTex, s);

                // The empty window's PNG while it loads (or won't take you)
                fixed4 empty = tex2D(_EmptyTex, TRANSFORM_TEX(i.uv, _EmptyTex));
                view.rgb = lerp(view.rgb, empty.rgb, _Static);

                // Scanlines and darker edges
                view.rgb *= 1 - _Scanlines * (0.5 + 0.5 * sin(s.y * _ScreenParams.y * 1.2));
                float2 e = abs(i.uv - 0.5) * 2;
                view.rgb *= 1 - _Edge * pow(saturate(max(e.x, e.y)), 6);
                return view * _Tint;
            }
            ENDCG
        }
    }
}
