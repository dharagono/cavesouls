Shader "CaveSouls/URP/SoulsVertexBlendLit"
{
    Properties
    {
        [Header(Primary Rock Layer)]
        _BaseMap("Rock Base Map", 2D) = "white" {}
        _BaseColor("Rock Tint", Color) = (0.42, 0.46, 0.52, 1.0)
        [Normal] _BumpMap("Rock Normal Map", 2D) = "bump" {}
        _BumpScale("Rock Normal Scale", Range(0.0, 2.5)) = 1.35
        _MaskMap("Rock Mask Map (R:Met G:AO B:Det A:Smooth)", 2D) = "white" {}

        [Header(Secondary Moss and Ash Layer)]
        _MossBaseMap("Moss Base Map", 2D) = "white" {}
        _MossColor("Moss/Ash Tint", Color) = (0.28, 0.34, 0.30, 1.0)
        [Normal] _MossBumpMap("Moss Normal Map", 2D) = "bump" {}
        _MossBumpScale("Moss Normal Scale", Range(0.0, 2.5)) = 1.20
        _MossMaskMap("Moss Mask Map (R:Met G:AO B:Det A:Smooth)", 2D) = "white" {}
        _MossBlendStrength("Vertex R Moss Blend Strength", Range(0.0, 2.0)) = 1.15
        _MossSharpness("Moss Blend Contrast", Range(1.0, 8.0)) = 2.8

        [Header(Edge Weathering and FromSoftware Rim Light)]
        _EdgeWearColor("Vertex G Edge Wear Tint", Color) = (0.62, 0.68, 0.76, 1.0)
        _EdgeWearStrength("Edge Wear Strength", Range(0.0, 1.0)) = 0.42
        [HDR] _RimColor("Cold Rim Light Color", Color) = (0.55, 0.70, 0.92, 1.0)
        _RimPower("Rim Fresnel Power", Range(1.0, 8.0)) = 3.5
        _RimIntensity("Rim Light Intensity", Range(0.0, 2.0)) = 0.65
        _SmoothnessMultiplier("Smoothness Multiplier", Range(0.1, 1.5)) = 0.78
        _OcclusionStrength("AO Strength", Range(0.0, 1.0)) = 0.90
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float2 lightmapUV   : TEXCOORD1;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 tangentWS    : TEXCOORD3;
                float4 vertexColor  : TEXCOORD4;
                half   fogFactor    : TEXCOORD5;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskMap);        SAMPLER(sampler_MaskMap);

            TEXTURE2D(_MossBaseMap);    SAMPLER(sampler_MossBaseMap);
            TEXTURE2D(_MossBumpMap);    SAMPLER(sampler_MossBumpMap);
            TEXTURE2D(_MossMaskMap);    SAMPLER(sampler_MossMaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _BumpScale;
                float4 _MossBaseMap_ST;
                half4  _MossColor;
                half   _MossBumpScale;
                half   _MossBlendStrength;
                half   _MossSharpness;
                half4  _EdgeWearColor;
                half   _EdgeWearStrength;
                half4  _RimColor;
                half   _RimPower;
                half   _RimIntensity;
                half   _SmoothnessMultiplier;
                half   _OcclusionStrength;
            CBUFFER_END

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.tangentWS = float4(normInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.vertexColor = input.color;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                float2 uvRock = input.uv;
                float2 uvMoss = input.uv * 1.35;

                half4 rockAlbedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvRock);
                half4 rockMask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uvRock);
                half3 rockNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvRock), _BumpScale);

                half4 mossAlbedoSample = SAMPLE_TEXTURE2D(_MossBaseMap, sampler_MossBaseMap, uvMoss);
                half4 mossMask = SAMPLE_TEXTURE2D(_MossMaskMap, sampler_MossMaskMap, uvMoss);
                half3 mossNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_MossBumpMap, sampler_MossBumpMap, uvMoss), _MossBumpScale);

                // Compute organic height/cavity-modulated vertex blend factor from Vertex Color R
                half vertR = saturate(input.vertexColor.r * _MossBlendStrength);
                half upNormalBias = saturate((input.normalWS.y - 0.35) * 1.4);
                half heightMod = saturate((mossMask.g - rockMask.g * 0.65) + vertR * 0.85 + upNormalBias * 0.35);
                half mossBlend = saturate(pow(saturate(vertR * heightMod * 1.35), _MossSharpness));

                half3 baseAlbedo = lerp(rockAlbedoSample.rgb * _BaseColor.rgb, mossAlbedoSample.rgb * _MossColor.rgb, mossBlend);

                // Apply sharp-edge weathering from Vertex Color G (painted in Blender from dihedral angle)
                half edgeWear = saturate(input.vertexColor.g * _EdgeWearStrength * (0.5 + 0.5 * rockMask.g));
                baseAlbedo = lerp(baseAlbedo, _EdgeWearColor.rgb * rockAlbedoSample.rgb * 1.35, edgeWear);

                half3 blendedNormalTS = normalize(lerp(rockNormalTS, mossNormalTS, mossBlend));
                half4 blendedMask = lerp(rockMask, mossMask, mossBlend);

                half metallic = saturate(blendedMask.r);
                half ao = lerp(1.0h, saturate(blendedMask.g), _OcclusionStrength);
                half smoothness = saturate((blendedMask.a * _SmoothnessMultiplier) + edgeWear * 0.15h);

                float sgn = input.tangentWS.w;
                float3 bitangentWS = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS.xyz, input.normalWS.xyz);
                float3 normalWS = NormalizeNormalPerPixel(mul(blendedNormalTS, tangentToWorld));

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // Subtle FromSoftware Cold Rim Light (Fresnel) to separate dark rock silhouettes
                half ndotv = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0h - ndotv, _RimPower);
                // Bias rim light towards upper/right grazing direction matching the cavern chasm light
                half directionalMask = saturate(dot(normalWS, normalize(float3(0.55, 0.72, -0.42))) * 0.65 + 0.35);
                half3 rimEmission = _RimColor.rgb * (fresnel * _RimIntensity * directionalMask * ao);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseAlbedo;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = blendedNormalTS;
                surfaceData.emission = rimEmission;
                surfaceData.occlusion = ao;
                surfaceData.alpha = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
