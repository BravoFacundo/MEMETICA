// Memetica / Meme Window
// The Content layer of the meme window (behind your frame PNG): its own background (_PaperTex, your PNG for the
// empty window, tinted by _Paper) and, once it has a meme (_Filled), the whole meme on it, as big as it fits inside
// _Margin, centered, whatever its proportion (the quad's proportion comes from its scale).
// Loading a change (MemeWindow sets it): _Pixelate turns the meme into ever bigger squares (an old image loading).
// Unlit, so it always reads well; _Glow brightens the meme (the flash after a change).

Shader "Memetica/MemeWindow"
{
    Properties
    {
        _MainTex ("Meme", 2D) = "white" {}
        _PaperTex ("Background (the empty window)", 2D) = "white" {}
        _Paper ("Background Tint", Color) = (1, 1, 1, 1)
        _Filled ("Has A Meme", Range(0, 1)) = 0
        _Margin ("Margin Around The Meme (of the height)", Range(0, 0.3)) = 0.03
        _Fill ("Fill (cover the window, cut the rest)", Range(0, 1)) = 0
        [Header(Loading)]
        _Pixelate ("Pixelate", Range(0, 1)) = 0
        _Glow ("Glow (flash)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex, _PaperTex;
            float4 _MainTex_TexelSize, _PaperTex_ST;
            fixed4 _Paper;
            half _Filled, _Pixelate, _Glow, _Margin, _Fill;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float aspect : TEXCOORD1;   // the quad's width / height
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                float sx = length(unity_ObjectToWorld._m00_m10_m20);
                float sy = length(unity_ObjectToWorld._m01_m11_m21);
                o.aspect = sx / max(sy, 1e-5);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_PaperTex, TRANSFORM_TEX(i.uv, _PaperTex)) * _Paper;

                // The whole meme, as big as it fits inside the margin, centered
                half fill = step(0.5, _Fill);   // Fill: no margin, covering it all (the rest cut, centered)
                float2 inner = lerp(float2(1 - 2 * _Margin / i.aspect, 1 - 2 * _Margin), float2(1, 1), fill);
                float2 panel = (i.uv - 0.5) / max(inner, 0.01) + 0.5;
                float panelAspect = i.aspect * inner.x / max(inner.y, 0.01);
                float memeAspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1);
                float2 fit = float2(min(1, memeAspect / panelAspect), min(1, panelAspect / memeAspect));
                float2 cover = float2(max(1, memeAspect / panelAspect), max(1, panelAspect / memeAspect));
                float2 shown = lerp(fit, cover, fill);
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

                half show = meme.a * inside * step(0.5, _Filled);
                col.rgb = lerp(col.rgb, meme.rgb * (1 + _Glow), show);
                col.a = 1;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
