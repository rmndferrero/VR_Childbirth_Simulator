Shader "Custom/URP_Hospital_Floor_Grid"
{
    Properties
    {
        [Header(Floor Colors)]
        _BaseColor ("Base Floor Color", Color) = (0.86, 0.89, 0.92, 1.0)
        _TileColor ("Tile Inner Color", Color) = (0.91, 0.93, 0.95, 1.0)
        _GridColor ("Grid Lines Color", Color) = (0.60, 0.68, 0.76, 1.0)
        
        [Header(Grid Dimensions)]
        _GridSpacing ("Tile Size (Meters)", Float) = 1.0
        _LineWidth ("Line Width (Meters)", Range(0.005, 0.1)) = 0.025
        _BevelWidth ("Bevel Width (Meters)", Range(0.01, 0.2)) = 0.06
        
        [Header(Surface Properties)]
        _Smoothness ("Surface Smoothness", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry" 
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
        ZWrite On
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TileColor;
                float4 _GridColor;
                float _GridSpacing;
                float _LineWidth;
                float _BevelWidth;
                float _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float spacing = max(_GridSpacing, 0.01);
                float2 coord = input.positionWS.xz / spacing;

                // Anti-aliased grid line calculation using fwidth
                float2 gridPos = abs(frac(coord - 0.5) - 0.5) * spacing;
                float2 dGrid = fwidth(input.positionWS.xz);
                float2 lineDist = gridPos - (_LineWidth * 0.5);
                float2 lineAA = saturate(lineDist / max(dGrid, 0.0001));
                float lineMask = 1.0 - min(lineAA.x, lineAA.y);

                // Bevel border highlight around tile edges
                float2 bevelDist = gridPos - _BevelWidth;
                float2 bevelAA = saturate(bevelDist / max(dGrid, 0.0001));
                float bevelMask = 1.0 - min(bevelAA.x, bevelAA.y);

                // Blend floor surface color
                half4 surfaceCol = lerp(_TileColor, _BaseColor, bevelMask);
                surfaceCol = lerp(surfaceCol, _GridColor, lineMask);

                // Simple diffuse lighting calculation
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(input.normalWS, mainLight.direction));
                half3 ambient = SampleSH(input.normalWS);
                half3 diffuse = mainLight.color * NdotL * 0.45;
                half3 finalRGB = surfaceCol.rgb * (ambient + diffuse);

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
