// Memetica / Stone
// The stones' material (StonePath): its texture and color (tinted by StonePath > Look) and, for the stones that fall,
// cracks that appear on the stone itself while it warns (PathStone sets _Crack 0-1, _CrackTex and _CrackFit).
// The crack picture is laid once over the stone's top, seen from above, fitted to the stone's size and centered on it
// (each stone turns it its own way), and it runs a little down the sides. No repeating: the picture doesn't need to
// tile. It darkens the stone and dims its shine where the cracks are.
// Mark: a second texture over the whole stone, tiled in meters from the model's origin (seen from above on the top,
// from the side on the sides; it moves with the stone, never slides over it), so every model and size shows the same
// pattern: what tells each kind of stone apart (Resources/StoneTypes,
// set by the stone: _MarkTex, _MarkColor, _MarkSize, _MarkRelief). Relief makes it look carved into the stone.
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
        _CrackOpen ("Split apart (0-1, set by the stone: as it drops)", Range(0, 1)) = 0
        _CrackColor ("Crack Color", Color) = (0.08, 0.06, 0.05, 1)
        _CrackSize ("Picture size (times the stone's width: bigger, fewer and bigger pieces)", Range(0.5, 6)) = 2.25
        [HideInInspector] _CrackFit ("Fit (set by the stone)", Vector) = (0, 1, 0, 0)
        [Header(Mark (set by the stone from StoneTypes))]
        _MarkTex ("Mark", 2D) = "black" {}
        _MarkColor ("Mark Color (alpha: strength)", Color) = (0, 0, 0, 0)
        _MarkSize ("Mark Size (meters per tile)", Float) = 1
        _MarkRelief ("Mark Relief", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _CrackTex, _MarkTex;
        float4 _MarkTex_TexelSize;
        fixed4 _Color, _CrackColor, _MarkColor;
        half _Glossiness, _Crack, _CrackOpen, _CrackSize, _MarkRelief;
        float _MarkSize;
        float4 _CrackFit;   // x, z: its top's middle from the model's origin (meters, world) | y: its width | w: turn

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA   // (it sets o.Normal: the relief)
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
            half lines = tex2D(_CrackTex, uv).a;
            lines = saturate(lines * (1 + _CrackOpen * 6));   // Open: the lines widen into gaps (it comes apart)
            return lines * inside * lerp(top, 1, _CrackOpen);   // (and down the sides too)
        }

        // Where the mark is laid: meters from the model's origin, in tiles (it travels with the stone)
        float3 MarkSpace(float3 worldPos)
        {
            float3 origin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
            return (worldPos - origin) / max(_MarkSize, 0.01);
        }

        // The mark at this point: seen from the three sides, blended by which way the surface faces (no stretching)
        half Mark(float3 p, float3 weights)
        {
            return tex2D(_MarkTex, p.zy).a * weights.x + tex2D(_MarkTex, p.xz).a * weights.y + tex2D(_MarkTex, p.xy).a * weights.z;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 normal = normalize(WorldNormalVector(IN, float3(0, 0, 1)));
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            half mark = 0;
            if (_MarkColor.a > 0.001)
            {
                float3 weights = pow(abs(normal), 4);
                weights /= weights.x + weights.y + weights.z;
                float3 p = MarkSpace(IN.worldPos);
                mark = Mark(p, weights);
                // Relief: its slope, one texel to each side (the top's pattern; the sides keep it flat)
                float2 texel = _MarkTex_TexelSize.xy * 1.5;
                half here = tex2D(_MarkTex, p.xz).a;
                half2 slope = half2(here - tex2D(_MarkTex, p.xz + float2(texel.x, 0)).a, here - tex2D(_MarkTex, p.xz + float2(0, texel.y)).a);
                o.Normal = normalize(float3(slope * _MarkRelief * 6 * weights.y, 1));
                albedo.rgb = lerp(albedo.rgb, _MarkColor.rgb, mark * _MarkColor.a);
            }
            else o.Normal = float3(0, 0, 1);

            half crack = _Crack > 0.001 ? Cracks(IN.worldPos, normal) * _Crack : 0;
            o.Albedo = lerp(albedo.rgb, _CrackColor.rgb, crack);
            o.Smoothness = _Glossiness * (1 - crack) * (1 - mark * _MarkColor.a * 0.5);
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
