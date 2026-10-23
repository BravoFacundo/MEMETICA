// Memetica / Meme Window
// The meme window as a flat picture (a quad facing the camera): the window drawn in Art/Models/MemeWindow/
// MemeWindow_Frame.png (its left window, the one with _ □ X) and the meme inside its panel.
//   _Frame / _FrameRect   the PNG and where the window is in it (UVs: min x, min y, max x, max y). Outside the window's
//                         rounded corners is cut out (the PNG's dark background)
//   _Content              the panel inside the window (0-1 of the window: min x, min y, max x, max y)
//   _Filled               0: empty, the panel as drawn. 1: the meme, the whole of it as big as it fits, centered on paper
// Loading a change (MemeWindow sets it): _Pixelate turns the meme into ever bigger squares (an old image loading).
// Unlit, so it always reads well; _Glow brightens the meme (the flash after a change).

Shader "Memetica/MemeWindow"
{
    Properties
    {
        _MainTex ("Meme", 2D) = "white" {}
        _Paper ("Behind The Meme", Color) = (1, 1, 1, 1)
        _Filled ("Has A Meme", Range(0, 1)) = 0
        [Header(Window)]
        [NoScaleOffset] _Frame ("Window Picture", 2D) = "white" {}
        _FrameRect ("Window In The Picture (UV min x, min y, max x, max y)", Vector) = (0.001953, 0.140625, 0.376953, 0.996094)
        _Aspect ("Window Width / Height", Float) = 0.8767
        _Corner ("Window Corner Radius (of its height)", Float) = 0.018
        _Content ("Panel In The Window (min x, min y, max x, max y)", Vector) = (0.0391, 0.0342, 0.9609, 0.8425)
        _ContentCorner ("Panel Corner Radius (of the window's height)", Float) = 0.009
        [Header(Loading)]
        _Pixelate ("Pixelate", Range(0, 1)) = 0
        _Glow ("Glow (flash)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off   // seen from behind too (lying on the ground, on a branch)

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex, _Frame;
            float4 _MainTex_TexelSize;
            fixed4 _Paper;
            half _Filled, _Pixelate, _Glow;
            float4 _FrameRect, _Content;
            float _Aspect, _Corner, _ContentCorner;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // Distance outside a rounded rectangle (negative inside), in window heights (x scaled by the aspect)
            float RoundedRect(float2 uv, float4 rect, float radius)
            {
                float2 scale = float2(_Aspect, 1);
                float2 center = (rect.xy + rect.zw) * 0.5 * scale;
                float2 extent = (rect.zw - rect.xy) * 0.5 * scale;
                float2 q = abs(uv * scale - center) - (extent - radius);
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                clip(-RoundedRect(i.uv, float4(0, 0, 1, 1), _Corner));   // the PNG's background at the corners
                fixed4 col = tex2D(_Frame, lerp(_FrameRect.xy, _FrameRect.zw, i.uv));

                // The whole meme: as big as it fits in the panel, centered (paper on the sides or above and below)
                float2 panel = (i.uv - _Content.xy) / (_Content.zw - _Content.xy);
                float panelAspect = (_Content.z - _Content.x) * _Aspect / (_Content.w - _Content.y);
                float memeAspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1);
                float2 shown = float2(min(1, memeAspect / panelAspect), min(1, panelAspect / memeAspect));
                float2 uv = 0.5 + (panel - 0.5) / shown;
                half inside = all(uv == saturate(uv));

                fixed4 meme;
                if (_Pixelate > 0.001)
                {
                    float cells = lerp(128, 6, _Pixelate * _Pixelate);
                    float2 cell = (floor(uv * cells) + 0.5) / cells;
                    float lod = max(0, log2(_MainTex_TexelSize.z / cells));
                    meme = tex2Dlod(_MainTex, float4(cell, 0, lod));
                }
                else meme = tex2D(_MainTex, uv);
                fixed3 memeColor = lerp(_Paper.rgb, meme.rgb, meme.a * inside) * (1 + _Glow);

                half inPanel = RoundedRect(i.uv, _Content, _ContentCorner) <= 0;
                col.rgb = lerp(col.rgb, memeColor, inPanel * step(0.5, _Filled));

                col.a = 1;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
