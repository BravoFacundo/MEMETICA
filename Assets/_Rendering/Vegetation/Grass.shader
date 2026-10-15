// Memetica / Grass
// Dense grass cards drawn by GrassField (GPU instancing). Wind comes from the WindController globals.
// Each card gets the ground color and normal under it (sampled by GrassField): the root is colored and lit
// exactly like the island, and blends towards an upward normal at the tip for soft, even light.

Shader "Memetica/Grass"
{
    Properties
    {
        _MainTex ("Blades (white + alpha)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.4

        [Header(Color)]
        _RootBrightness ("Root Brightness (match the ground)", Range(0.5, 1.5)) = 1
        _TipTint ("Tip Tint", Color) = (1, 1, 0.85, 1)
        _TipBrightness ("Tip Brightness", Range(1, 3)) = 1.6
        _TipColor ("Tip Color", Color) = (0.72, 0.86, 0.36, 1)
        _TipColorMix ("Tip Color Mix (0 from ground, 1 Tip Color)", Range(0, 1)) = 0
        _GradientStart ("Gradient Start (0 base, 1 tip)", Range(0, 0.95)) = 0.4
        _GradientPower ("Gradient Power", Range(0.2, 3)) = 1
        _PatchColor ("Patch Color", Color) = (0.85, 0.85, 0.45, 1)
        _PatchScale ("Patch Size", Range(0.01, 1)) = 0.08
        _PatchAmount ("Patch Amount", Range(0, 1)) = 0.3
        _TipGlow ("Tip Glow", Range(0, 1)) = 0.2

        [Header(Wind)]
        _WindMultiplier ("Wind Multiplier", Range(0, 5)) = 1.5
        _WaveScale ("Wave Frequency", Range(0.01, 1)) = 0.15
        _WindFadeStart ("Wind Stops From", Float) = 20
        _WindFadeEnd ("Wind Stopped At", Float) = 30

        [Header(View From Above)]
        _TopViewTilt ("Tilt Away From Camera", Range(0, 1.5)) = 0.6
        _TiltFromDistance ("Tilt Starts At Distance", Float) = 4
        _TiltFullDistance ("Full Tilt At Distance", Float) = 15

        [Header(Interaction)]
        _InteractStrength ("Push Strength", Range(0, 2)) = 1

        [Header(Distance)]
        _FadeStart ("Shrink Start", Float) = 30
        _FadeEnd ("Shrink End", Float) = 45
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert alphatest:_Cutoff addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _TipTint, _TipColor, _PatchColor;
        float _RootBrightness, _TipColorMix, _TopViewTilt, _TiltFromDistance, _TiltFullDistance, _InteractStrength;
        float _TipBrightness, _GradientStart, _GradientPower, _PatchScale, _PatchAmount, _TipGlow;
        float _WindMultiplier, _WaveScale, _WindFadeStart, _WindFadeEnd, _FadeStart, _FadeEnd;

        float4 _MW_Direction;
        float _MW_Strength, _MW_Speed, _MW_Lean, _MW_FlutterStrength, _MW_FlutterSpeed;

        // Set by GrassField: player and orbs (xyz position, w radius)
        float4 _GrassInteractors[16];
        float _GrassInteractorCount;

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _GroundColor)
            UNITY_DEFINE_INSTANCED_PROP(float4, _GroundNormal)
        UNITY_INSTANCING_BUFFER_END(Props)

        struct Input
        {
            float2 uv_MainTex;
            float height;
            float patch;
        };

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                        lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
        }

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float h = v.texcoord.y;
            float3 basePos = unity_ObjectToWorld._m03_m13_m23;

            float cameraDistance = distance(_WorldSpaceCameraPos, basePos);
            float distanceFade = 1 - saturate((cameraDistance - _FadeStart) / max(_FadeEnd - _FadeStart, 0.01));
            float windFade = 1 - saturate((cameraDistance - _WindFadeStart) / max(_WindFadeEnd - _WindFadeStart, 0.01));
            v.vertex.xyz *= distanceFade;

            float3 dir = normalize(float3(_MW_Direction.x, 0, _MW_Direction.z) + float3(1e-5, 0, 0));
            float t = _Time.y * _MW_Speed;
            float along = dot(basePos.xz, dir.xz) * _WaveScale * 6.2832;
            float wave = sin(t * 1.3 - along) * 0.5 + 0.5;
            float gust = sin(t * 0.4 - along * 0.27) * 0.5 + 0.5;
            float bend = (_MW_Lean + wave * _MW_Strength * (0.5 + gust) * windFade) * _WindMultiplier;

            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float flutter = sin(_Time.y * _MW_FlutterSpeed + worldPos.x * 3.1 + worldPos.z * 2.3) * _MW_FlutterStrength * windFade;

            float3 offset = dir * (bend + flutter) * h * h;
            offset.y -= bend * bend * h * h * 0.5;

            float cardHeight = length(unity_ObjectToWorld._m01_m11_m21);

            // Seen from above and far (the menu camera), the tips lean away from the camera and cover the ground between cards.
            // Close to the camera (the player looking at their feet) there is no tilt, so the grass doesn't move when you look around.
            float3 toCamera = _WorldSpaceCameraPos - basePos;
            float tiltByDistance = saturate((cameraDistance - _TiltFromDistance) / max(_TiltFullDistance - _TiltFromDistance, 0.01));
            float fromAbove = saturate(toCamera.y / max(length(toCamera), 1e-4)) * tiltByDistance;
            float2 away = normalize(-toCamera.xz + float2(1e-4, 0));
            offset.xz += away * fromAbove * _TopViewTilt * cardHeight * h;

            // Player and orbs push the grass aside
            for (int i = 0; i < 16; i++)
            {
                if (i >= (int)_GrassInteractorCount) break;
                float4 interactor = _GrassInteractors[i];
                float2 diff = basePos.xz - interactor.xz;
                float push = saturate(1 - length(diff) / max(interactor.w, 0.01)) * step(abs(basePos.y - interactor.y), 2);
                float2 pushDir = normalize(diff + float2(1e-4, 0));
                offset.xz += pushDir * push * _InteractStrength * cardHeight * h;
                offset.y -= push * _InteractStrength * cardHeight * h * 0.5;
            }
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offset);
            float3 groundNormal = normalize(UNITY_ACCESS_INSTANCED_PROP(Props, _GroundNormal).xyz + float3(0, 1e-4, 0));
            float gradient = saturate((h - _GradientStart) / (1 - _GradientStart));
            float3 worldNormal = normalize(lerp(groundNormal, float3(0, 1, 0), gradient));
            v.normal = normalize(mul(worldNormal, (float3x3)unity_ObjectToWorld));

            o.height = h;
            o.patch = ValueNoise(basePos.xz * _PatchScale);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            fixed3 ground = UNITY_ACCESS_INSTANCED_PROP(Props, _GroundColor).rgb * _RootBrightness;
            fixed3 tip = lerp(saturate(ground * _TipBrightness * _TipTint.rgb), _TipColor.rgb, _TipColorMix);

            float gradient = pow(saturate((IN.height - _GradientStart) / (1 - _GradientStart)), _GradientPower);

            fixed3 color = lerp(ground, tip, gradient);
            color = lerp(color, color * _PatchColor.rgb * 1.4, IN.patch * _PatchAmount * gradient);

            o.Albedo = color * lerp(1, tex.rgb, gradient);
            o.Alpha = tex.a;
            o.Emission = o.Albedo * gradient * _TipGlow * _LightColor0.rgb;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}
