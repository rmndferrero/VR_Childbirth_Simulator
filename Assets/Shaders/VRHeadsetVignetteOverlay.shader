Shader "VRSim/VRHeadsetVignetteOverlay"
{
    Properties
    {
        _Color ("Vignette Color", Color) = (0.88, 0.04, 0.04, 1.0)
        _Intensity ("Intensity", Range(0.0, 1.0)) = 0.0
        _InnerRadius ("Inner Radius (NDC)", Range(0.1, 1.2)) = 0.44
        _OuterRadius ("Outer Radius (NDC)", Range(0.2, 1.5)) = 0.86
        _Softness ("Edge Falloff Power", Range(0.5, 4.0)) = 1.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay+10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "VRHeadsetVignetteOverlay"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _InnerRadius;
                half _OuterRadius;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 ndc : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Unity Quad vertices span [-0.5, 0.5] in local XY; scale by 2 to cover [-1, 1] NDC per stereo eye.
                float2 ndc = input.positionOS.xy * 2.0;
                output.ndc = ndc;
                output.positionCS = float4(ndc.x, ndc.y, UNITY_NEAR_CLIP_VALUE, 1.0);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_Intensity <= 0.0001h)
                {
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                float dist = length(input.ndc);
                float inner = (float)_InnerRadius;
                float outer = max(inner + 0.05, (float)_OuterRadius);
                float mask = smoothstep(inner, outer, dist);
                mask = pow(saturate(mask), max(0.01, (float)_Softness));
                half alpha = (half)saturate(mask * (float)_Intensity * (float)_Color.a);
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
