Shader "ClawMachine/TargetOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.01, 0.01, 0.01, 1)
        _OutlineWidth("Outline Width (Pixels)", Range(0.5, 12)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "TargetOutline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionHCS = TransformWorldToHClip(positionWS);
                float4 normalHCS = TransformWorldToHClip(positionWS + normalWS);

                float2 positionNDC = positionHCS.xy / max(positionHCS.w, 0.0001);
                float2 normalNDC = normalHCS.xy / max(normalHCS.w, 0.0001);
                float2 outlineDirection = normalNDC - positionNDC;
                float directionLength = length(outlineDirection);
                if (directionLength > 0.00001)
                {
                    outlineDirection /= directionLength;
                }

                float2 pixelSize = 2.0 / _ScreenParams.xy;
                positionHCS.xy += outlineDirection * pixelSize * _OutlineWidth * positionHCS.w;
                output.positionHCS = positionHCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
