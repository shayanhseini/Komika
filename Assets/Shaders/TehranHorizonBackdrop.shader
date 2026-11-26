Shader "Komika/Tehran Horizon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _HazeColor("Horizon Haze Color", Color) = (0.79, 0.85, 0.92, 1)
        _HazeBaseY("Haze Base World Y", Float) = -10.0
        _HazeTopY("Haze Clear Top World Y", Float) = 62.0
        _HazeStrength("Max Horizon Haze Blend", Range(0.0, 1.0)) = 0.72
        _MinAtmosphere("Upper Peak Atmosphere Blend", Range(0.0, 0.5)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "HorizonUnlitLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _HazeColor;
                float _HazeBaseY;
                float _HazeTopY;
                float _HazeStrength;
                float _MinAtmosphere;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = normalize(input.normalWS);

                Light mainLight = GetMainLight();
                half ndl = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half3 shaded = col.rgb * (half3(0.45h, 0.50h, 0.60h) + mainLight.color * (ndl * 0.68h));

                // Lower foothills dissolve smoothly into Tehran urban haze while high snow peaks & tower spire pop cleanly!
                float hNorm = saturate((input.positionWS.y - _HazeBaseY) / max(1.0, _HazeTopY - _HazeBaseY));
                float hazeFactor = lerp(_HazeStrength, _MinAtmosphere, pow(hNorm, 0.85));
                half3 finalRgb = lerp(shaded, _HazeColor.rgb, (half)hazeFactor);

                return half4(finalRgb, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
