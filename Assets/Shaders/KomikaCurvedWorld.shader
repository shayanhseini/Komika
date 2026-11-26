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
        [Toggle(_ALPHACLIP)] _AlphaClip("Alpha Clipping", Float) = 0.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.42
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2.0
        [Toggle] _ReceiveShadows("Receive Shadows", Float) = 1.0
        _ShadowTint("Shadow Tint", Color) = (0.36, 0.43, 0.56, 1)
        _LightingWrap("Stylized Wrap", Range(0.0, 0.6)) = 0.25
        _RimStrength("Rim Light Strength", Range(0.0, 0.6)) = 0.14
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHACLIP

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
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord  : TEXCOORD3;
            #endif
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
                float4 _ShadowTint;
                float _BumpScale;
                float _Smoothness;
                float _Metallic;
                float _ReceiveShadows;
                float _LightingWrap;
                float _RimStrength;
                float _Cutoff;
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
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = TransformWorldToShadowCoord(worldPos);
            #endif

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            #if defined(_ALPHACLIP)
                clip(albedo.a - _Cutoff);
            #endif
                half3 normalWS = normalize(input.normalWS);

            #if defined(_NORMALMAP)
                half4 bumpSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(bumpSample, _BumpScale);
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS), true);
            #endif

                // Directional / Main Light with per-pixel cascade shadow sampling (prevents blocky/sheared cascade seams)
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                float4 shadowCoord = float4(0, 0, 0, 0);
            #endif
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                half rawNdotL = dot(normalWS, mainLight.direction);
                half NdotL = saturate((rawNdotL + (half)_LightingWrap) / (1.0h + (half)_LightingWrap));

                // Spherical harmonics ambient light with atmospheric cool Tehran sky floor
                half3 shAmbient = SampleSH(normalWS);
                half3 ambient = max(shAmbient, _ShadowTint.rgb * 0.85h);

                // Shadow attenuation with ReceiveShadows toggle support
                half shadowAtten = lerp(1.0h, (half)mainLight.shadowAttenuation, (half)_ReceiveShadows);
                half lightTerm = NdotL * shadowAtten * (half)mainLight.distanceAttenuation;

                // Blend shaded regions gently toward _ShadowTint instead of flat black mud
                half3 directDiffuse = mainLight.color * lightTerm;
                half3 shadowFill = _ShadowTint.rgb * ((1.0h - lightTerm) * 0.28h);

                // Specular reflection (Blinn-Phong, tinted for metallic surfaces)
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPow = exp2(10.0 * _Smoothness + 1.0);
                float spec = pow(NdotH, specPow) * _Smoothness * (0.35 + 0.65 * _Metallic);
                half3 specTint = lerp(half3(1.0h, 1.0h, 1.0h), albedo.rgb, (half)_Metallic);
                half3 directSpecular = mainLight.color * specTint * (spec * shadowAtten * saturate(rawNdotL));

                // Subtle stylized fresnel rim light so silhouettes separate cleanly in curved perspective
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 3.0h);
                half3 rimLight = mainLight.color * (fresnel * (half)_RimStrength * (0.4h + 0.6h * saturate(normalWS.y)));

                half3 totalLighting = ambient + directDiffuse + shadowFill;

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

                half3 finalRgb = albedo.rgb * totalLighting + directSpecular + rimLight + _EmissionColor.rgb;
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHACLIP
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _ShadowTint;
                float _BumpScale;
                float _Smoothness;
                float _Metallic;
                float _ReceiveShadows;
                float _LightingWrap;
                float _RimStrength;
                float _Cutoff;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            float4 _KomikaCurveOrigin;
            float4 _KomikaCurveParams;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
            #if defined(_ALPHACLIP)
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
            #endif
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHACLIP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _ShadowTint;
                float _BumpScale;
                float _Smoothness;
                float _Metallic;
                float _ReceiveShadows;
                float _LightingWrap;
                float _RimStrength;
                float _Cutoff;
            CBUFFER_END

            float4 _KomikaCurveOrigin;
            float4 _KomikaCurveParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
            #if defined(_ALPHACLIP)
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _ALPHACLIP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _ShadowTint;
                float _BumpScale;
                float _Smoothness;
                float _Metallic;
                float _ReceiveShadows;
                float _LightingWrap;
                float _RimStrength;
                float _Cutoff;
            CBUFFER_END

            float4 _KomikaCurveOrigin;
            float4 _KomikaCurveParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
            #if defined(_ALPHACLIP)
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
            #endif
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
