// Memetica / Wind Sway
// Standard (metálico) + balanceo por viento en el vertex shader.
// Los valores del viento son GLOBALES: los pone el componente WindController de la escena.
// Así todos los materiales (y todas las partes de un árbol con varios materiales) se mueven igual.
// La altura se mide en METROS desde la base de cada objeto (su pivote), así sirve para cualquier paquete.
// IMPORTANTE: los objetos con este material NO deben tener "Batching Static" tildado.

Shader "Memetica/Wind Sway"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
        _Metallic ("Metallic", Range(0,1)) = 0.0

        [Header(Viento)]
        _WindMultiplier ("Multiplicador (1 = como el WindController)", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // addshadow: la sombra también se mueve con el viento
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        float _WindMultiplier;

        // Globales (WindController)
        float4 _MW_Direction;
        float _MW_Strength, _MW_Speed, _MW_Lean;
        float _MW_BaseHeight, _MW_HeightRange, _MW_Stiffness;
        float _MW_FlutterStrength, _MW_FlutterSpeed, _MW_FlutterStart, _MW_FlutterScale;

        struct Input
        {
            float2 uv_MainTex;
        };

        void vert (inout appdata_full v)
        {
            float3 pivot = unity_ObjectToWorld._m03_m13_m23;           // base del objeto
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

            // Peso por altura (metros sobre el pivote): 0 abajo, 1 arriba; la rigidez endurece el medio
            float h = saturate((worldPos.y - pivot.y - _MW_BaseHeight) / max(_MW_HeightRange, 0.001));
            float weight = pow(h, max(_MW_Stiffness, 1));
            float flutterWeight = saturate((h - _MW_FlutterStart) / max(1 - _MW_FlutterStart, 0.001));   // solo la copa tiembla

            // Balanceo: una onda lenta + una más rápida, con desfase por objeto
            float phase = dot(pivot, float3(0.31, 0, 0.47));
            float t = _Time.y * _MW_Speed;
            float sway = sin(t + phase) + 0.35 * sin(t * 2.3 + phase * 1.7);

            float3 dir = normalize(float3(_MW_Direction.x, 0, _MW_Direction.z) + float3(1e-5, 0, 0));
            float3 offset = dir * (_MW_Lean + sway * _MW_Strength) * weight;

            // Temblor de hojas: depende SOLO de la posición (no de la normal), así las caras low poly no se abren
            float3 p = worldPos * _MW_FlutterScale;
            float ft = _Time.y * _MW_FlutterSpeed;
            float3 flutter = float3(sin(ft + p.x + p.z * 1.3),
                                    sin(ft * 1.17 + p.y + p.x * 0.7) * 0.5,
                                    cos(ft * 0.93 + p.z + p.y * 1.1));
            offset += flutter * _MW_FlutterStrength * flutterWeight;

            // El desplazamiento está en mundo: lo paso a espacio del objeto
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offset * _WindMultiplier);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
