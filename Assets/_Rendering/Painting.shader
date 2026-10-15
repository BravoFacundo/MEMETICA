// Memetica / Painting
// Lienzo: muestra una imagen o un video (VideoPlayer en modo Material Override sobre _MainTex)
// con una máscara de esquinas redondeadas y borde difuminado que se funde con el color del lienzo.
// La máscara es del shader, así que sirve igual para la imagen y para el video:
// no hace falta hornear bordes en los PNG ni en los videos.

Shader "Memetica/Painting"
{
    Properties
    {
        _MainTex ("Imagen / Video", 2D) = "white" {}
        _CanvasColor ("Color del lienzo", Color) = (0.93, 0.92, 0.89, 1)

        [Header(Mascara)]
        _Aspect ("Proporción del lienzo (ancho / alto)", Float) = 1.6
        _Inset ("Margen hasta el borde", Range(0, 0.2)) = 0.04
        _Radius ("Redondeo de esquinas", Range(0, 0.5)) = 0.06
        _Feather ("Difuminado del borde", Range(0.001, 0.2)) = 0.04

        [Header(Superficie)]
        _Glossiness ("Smoothness", Range(0, 1)) = 0.05
        _Emission ("Brillo propio de la imagen", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _CanvasColor;
        float _Aspect, _Inset, _Radius, _Feather;
        half _Glossiness, _Emission;

        struct Input
        {
            float2 uv_MainTex;
        };

        // Distancia a un rectángulo redondeado (negativa adentro, positiva afuera)
        float RoundedBox(float2 p, float2 halfSize, float r)
        {
            float2 q = abs(p) - (halfSize - r);
            return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // UV centrado y corregido por la proporción, así las esquinas son redondas y no ovaladas
            float2 scale = float2(_Aspect, 1);
            float2 p = (IN.uv_MainTex - 0.5) * scale;
            float2 halfSize = scale * 0.5 - _Inset;
            float d = RoundedBox(p, halfSize, _Radius);
            float mask = 1 - smoothstep(-_Feather, 0, d);   // 1 adentro, se desvanece hasta el borde

            fixed3 img = tex2D(_MainTex, IN.uv_MainTex).rgb;
            fixed3 col = lerp(_CanvasColor.rgb, img, mask);

            o.Albedo = col;
            o.Emission = img * mask * _Emission;             // la imagen se ve un poco aunque esté en sombra
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
