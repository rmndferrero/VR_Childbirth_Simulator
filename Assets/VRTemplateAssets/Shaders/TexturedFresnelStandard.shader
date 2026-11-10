Shader "SpatialFramework/Textured Fresnel/Standard"
{
    Properties
    {
        _EdgeColor("Edge Color", COLOR) = (1,1,1,1)
        _Color("Color", COLOR) = (.25,.25,.25,.25)
        _EdgeData("Edge min, max, S-strength, S-Blend", VECTOR) = (0, 0.85, 0.5, 1)
        _MainTex("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // First, we do a stencil like technique of writing depth of the model,
        // so we don't have any transparent overdraw in subsequent steps
        Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Tags
            {
                "RenderType" = "Transparent"
                "Queue" = "Transparent"
                "LightMode" = "SRPDefaultUnlit"
                "RenderPipeline" = "UniversalPipeline"
            }
            LOD 100

            Name "Depth Fill"
            Blend Zero One
            Lighting Off
            ZTest LEqual
            ZWrite On
            Offset -1, -1

            ColorMask 0

            CGPROGRAM

                #pragma vertex vert
                #pragma fragment fragEmpty

                #include "UnityCG.cginc"
                #include "TexturedStableFresnelCommon.cginc"

            ENDCG
        }

        // Next, fill in with the base and rim color
        Pass
        {
            Tags
            {
                "RenderType" = "Transparent"
                "Queue" = "Transparent"
                "LightMode" = "UniversalForward"
                "RenderPipeline" = "UniversalPipeline"
            }

            Name "Fresnel Color"
            Blend SrcAlpha OneMinusSrcAlpha
            Lighting Off
            ZTest LEqual
            ZWrite Off
            Offset -1, -1

            CGPROGRAM

                #pragma vertex vert
                #pragma fragment fragRimShader

                #include "UnityCG.cginc"
                #include "TexturedStableFresnelCommon.cginc"

            ENDCG
        }
    }
    FallBack "Diffuse"
}
