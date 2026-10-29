// Memetica / Stone
// The stones' material (StonePath): its texture and color (tinted by StonePath > Look) and, for the stones that fall,
// cracks that appear on the stone itself while it warns (PathStone sets _Crack 0-1, _CrackTex and _CrackFit).
// The crack picture is laid once over the stone's top, seen from above, fitted to the stone's size and centered on it
// (each stone turns it its own way), and it runs a little down the sides. No repeating: the picture doesn't need to
// tile. It darkens the stone and dims its shine where the cracks are.
// Use it on the stone models' material (it keeps their texture and color: same property names as Standard).

Shader "Memetica/Stone"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.2
        [Header(Cracks)]
        _CrackTex ("Cracks (PNG with transparency)", 2D) = "black" {}
        _Crack ("Cracked (0-1, set by the stone)", Range(0, 1)) = 0
        _CrackColor ("Crack Color", Color) = (0.08, 0.06, 0.05, 1)
        _CrackSize ("Picture size (times the stone's width)", Range(0.5, 2)) = 1.1
        [HideInInspector] _CrackFit ("Fit (set by the stone)", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _CrackTex;
        fixed4 _Color, _CrackColor;
        half _Glossiness, _Crack, _CrackSize;
        float4 _CrackFit;   // x, z: its top's middle from the model's origin (meters, world) | y: its width | w: turn

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        // The picture over the top, from above: 0 outside it and on the stone's underside
        half Cracks(float3 worldPos, float3 normal)
        {
            float3 origin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
            float2 d = worldPos.xz - origin.xz - float2(_CrackFit.x, _CrackFit.z);
            float s = sin(_CrackFit.w), c = cos(_CrackFit.w);
            d = float2(c * d.x - s * d.y, s * d.x + c * d.y);
            float2 uv = d / max(_CrackFit.y * _CrackSize, 0.01) + 0.5;
            half inside = all(uv == saturate(uv));
            half top = saturate(normal.y * 2.0 + 0.3);   // the top, and partway down the sides
            return tex2D(_CrackTex, uv).a * inside * top;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half crack = _Crack > 0.001 ? Cracks(IN.worldPos, normalize(IN.worldNormal)) * _Crack : 0;
            o.Albedo = lerp(albedo.rgb, _CrackColor.rgb, crack);
            o.Smoothness = _Glossiness * (1 - crack);
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
