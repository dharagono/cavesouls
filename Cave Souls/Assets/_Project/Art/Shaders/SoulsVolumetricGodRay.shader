Shader "CaveSouls/URP/SoulsVolumetricGodRay"
{
    Properties
    {
        [HDR] _BeamColor("Beam HDR Color", Color) = (1.25, 1.38, 1.58, 0.28)
        [HDR] _CoreColor("Upper Core Warm/Cold Tint", Color) = (1.55, 1.62, 1.75, 1.0)
        _DepthFadeDistance("Scene Depth Fade (Soft Particles)", Range(0.1, 8.0)) = 2.2
        _FresnelFadePower("Edge Fresnel Power", Range(0.5, 5.0)) = 1.45
        _NoiseScale("Volumetric Dust Noise Scale", Range(0.5, 10.0)) = 3.2
        _ScrollSpeed("Dust Drift Speed", Range(0.0, 0.5)) = 0.045
        _BeamContrast("Ray Striation Contrast", Range(0.5, 4.0)) = 1.55
        [Toggle] _EnableWorldHeightFade("Enable World Height Fade", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VolumetricGodRay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 viewDirWS  : TEXCOORD3;
                float4 screenPos  : TEXCOORD4;
                float3 positionWS : TEXCOORD5;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
                half4 _CoreColor;
                float _DepthFadeDistance;
                float _FresnelFadePower;
                float _NoiseScale;
                float _ScrollSpeed;
                float _BeamContrast;
                float _EnableWorldHeightFade;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.uv = input.uv;
                o.color = input.color;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                o.screenPos = ComputeScreenPos(posInputs.positionCS);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. Smooth horizontal bell profile & vertical fade at both top and bottom so beams vanish into the upper black void
                float uFade = pow(saturate(sin(input.uv.x * 3.14159265)), 1.65);
                float vFade = pow(saturate(sin(input.uv.y * 3.14159265)), 1.85);

                // Fade out smoothly if height fade is enabled (e.g. in menu scene)
                float worldHeightFade = _EnableWorldHeightFade > 0.5 ? saturate((1.65 - input.positionWS.y) / 1.45) : 1.0;

                // 2. Fine crepuscular ray striations + drifting dust motes
                float t = _Time.y * _ScrollSpeed;
                float striation1 = ValueNoise(float2(input.uv.x * 10.0 + 1.3, input.uv.y * 1.4 - t));
                float striation2 = ValueNoise(float2(input.uv.x * 22.0 - 2.7, input.uv.y * 2.0 - t * 1.4));
                float dustCloud  = ValueNoise(input.uv * _NoiseScale + float2(t * 0.4, -t * 0.8));

                float rayPattern = pow(saturate(striation1 * 0.65 + striation2 * 0.35), _BeamContrast);
                float volumetricDensity = lerp(0.55, 1.35, rayPattern) * lerp(0.80, 1.20, dustCloud);

                // 3. View-angle Fresnel softness
                float ndotv = abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                float fresnelFade = lerp(0.35, 1.0, pow(saturate(ndotv), _FresnelFadePower));

                // 4. Scene Depth Fade (Soft Particles) against cavern rock geometry
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float partEyeDepth = input.screenPos.w;
                float depthDiff = sceneEyeDepth - partEyeDepth;
                float depthFade = (sceneEyeDepth <= 0.15) ? 1.0 : saturate(depthDiff / max(0.05, _DepthFadeDistance));

                float alpha = uFade * vFade * worldHeightFade * input.color.r * volumetricDensity * fresnelFade * depthFade * _BeamColor.a;
                half3 finalRGB = lerp(_BeamColor.rgb, _CoreColor.rgb, saturate(input.uv.y * 0.55));

                return half4(finalRGB, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
