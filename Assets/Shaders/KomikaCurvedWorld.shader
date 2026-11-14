Shader "Komika/Curved World Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_NORMALMAP)] _EnableNormalMap("Enable Normal Map", Float) = 0.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [Toggle] _ReceiveShadows("Receive Shadows", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma shader_feature_local _NORMALMAP

            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 tangentWS    : TEXCOORD5;
                float2 uv           : TEXCOORD2;
                float4 shadowCoord  : TEXCOORD3;
                float  fogFactor    : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float _BumpScale;
                float _Smoothness;
                float _Metallic;
                float _ReceiveShadows;
            CBUFFER_END

            float4 _KomikaCurveOrigin;
            float4 _KomikaCurveParams;

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);

                // Curved World vertex displacement
                float dist = clamp(worldPos.z - _KomikaCurveOrigin.z, -_KomikaCurveParams.z, _KomikaCurveParams.z);
                float curveAmount = dist * dist;
                worldPos.y -= curveAmount * _KomikaCurveParams.x;
                worldPos.x += curveAmount * _KomikaCurveParams.y;

                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), sign);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.shadowCoord = TransformWorldToShadowCoord(worldPos);

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = normalize(input.normalWS);

            #if defined(_NORMALMAP)
                half4 bumpSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(bumpSample, _BumpScale);
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS), true);
            #endif

                // Directional / Main Light
                Light mainLight = GetMainLight(input.shadowCoord);
                half NdotL = saturate(dot(normalWS, mainLight.direction));

                // Spherical harmonics ambient light (sky/ground ambient) with stylized floor
                half3 ambient = max(SampleSH(normalWS), half3(0.2, 0.2, 0.2));

                // Shadow attenuation with ReceiveShadows toggle support
                half shadowAtten = lerp(1.0h, (half)mainLight.shadowAttenuation, (half)_ReceiveShadows);

                // Direct lighting with shadow and distance attenuation
                half3 directDiffuse = mainLight.color * (NdotL * shadowAtten * (half)mainLight.distanceAttenuation);

                // Specular reflection (Blinn-Phong)
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPow = exp2(10.0 * _Smoothness + 1.0);
                float spec = pow(NdotH, specPow) * _Smoothness;
                half3 directSpecular = mainLight.color * (spec * shadowAtten * NdotL);

                half3 totalLighting = ambient + directDiffuse;

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    half addNdotL = saturate(dot(normalWS, addLight.direction));
                    half addShadow = lerp(1.0h, (half)addLight.shadowAttenuation, (half)_ReceiveShadows);
                    totalLighting += addLight.color * (addNdotL * (half)addLight.distanceAttenuation * addShadow);
                }
            #endif

                half3 finalRgb = albedo.rgb * totalLighting + directSpecular + _EmissionColor.rgb;
                finalRgb = MixFog(finalRgb, input.fogFactor);

                return half4(finalRgb, albedo.a);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 _KomikaCurveOrigin;
            float4 _KomikaCurveParams;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float dist = clamp(worldPos.z - _KomikaCurveOrigin.z, -_KomikaCurveParams.z, _KomikaCurveParams.z);
                float curveAmount = dist * dist;
                worldPos.y -= curveAmount * _KomikaCurveParams.x;
                worldPos.x += curveAmount * _KomikaCurveParams.y;

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - worldPos);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                output.positionCS = positionCS;

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _KomikaCurveOrigin;
            float4 _KomikaCurveParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float dist = clamp(worldPos.z - _KomikaCurveOrigin.z, -_KomikaCurveParams.z, _KomikaCurveParams.z);
                float curveAmount = dist * dist;
                worldPos.y -= curveAmount * _KomikaCurveParams.x;
                worldPos.x += curveAmount * _KomikaCurveParams.y;

                output.positionCS = TransformWorldToHClip(worldPos);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _KomikaCurveOrigin;
            float4 _KomikaCurveParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float dist = clamp(worldPos.z - _KomikaCurveOrigin.z, -_KomikaCurveParams.z, _KomikaCurveParams.z);
                float curveAmount = dist * dist;
                worldPos.y -= curveAmount * _KomikaCurveParams.x;
                worldPos.x += curveAmount * _KomikaCurveParams.y;

                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
