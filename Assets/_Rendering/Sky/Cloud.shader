// Memetica / Cloud
// Nube que cambia de forma despacio: se estira en un eje mientras se encoge en otro, y respira.
// Es una deformación "en bloque" (escala sobre su centro), así nunca aparecen picos ni caras abiertas.
// Los bordes brillan un poco (rim).
// IMPORTANTE: las nubes con este material NO deben tener "Batching Static" tildado.

Shader "Memetica/Cloud"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _EmissionColor ("Emisión base", Color) = (0.5,0.5,0.5,1)

        [Header(Forma)]
        _Morph ("Estiramiento (0.05 = 5 por ciento)", Range(0, 0.15)) = 0.015
        _MorphSpeed ("Velocidad (ciclos por segundo)", Range(0, 0.2)) = 0.015

        [Header(Respiracion)]
        _Breath ("Cuánto se infla (0.05 = 5 por ciento)", Range(0, 0.1)) = 0.008
        _BreathSpeed ("Velocidad (ciclos por segundo)", Range(0, 0.2)) = 0.03

        [Header(Borde brillante)]
        _RimColor ("Color", Color) = (1,1,1,1)
        _RimPower ("Finura (más alto = borde más fino)", Range(0.5, 8)) = 3
        _RimStrength ("Intensidad", Range(0, 2)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        fixed4 _Color, _EmissionColor, _RimColor;
        float _Morph, _MorphSpeed;
        float _Breath, _BreathSpeed;
        float _RimPower, _RimStrength;

        struct Input
        {
            float3 viewDir;
        };

        void vert (inout appdata_full v)
        {
            float3 pivot = unity_ObjectToWorld._m03_m13_m23;
            float phase = dot(pivot, float3(0.13, 0.07, 0.21));         // cada nube a su ritmo

            // Estiramiento: cada eje con su ritmo (cuando uno crece, otro achica)
            float t = _Time.y * _MorphSpeed * 6.2832 + phase;
            float3 stretch = float3(sin(t), sin(t * 0.8 + 2.1), sin(t * 1.1 + 4.2)) * _Morph;

            // Respiración: toda la nube se infla y desinfla
            float breath = sin(_Time.y * _BreathSpeed * 6.2832 + phase * 3.0) * _Breath;

            // Escala sobre el centro del objeto (espacio del objeto): deformación suave, sin picos
            v.vertex.xyz *= 1 + stretch + breath;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = 0;
            o.Normal = float3(0, 0, 1);   // así viewDir llega en espacio tangente y el rim funciona
            // Borde brillante: más fuerte donde la superficie mira de costado a la cámara
            float rim = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower);
            o.Emission = _EmissionColor.rgb + _RimColor.rgb * rim * _RimStrength;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
