Shader "Custom/Stool Highlight"
{
    // 踏板引导高亮：无光照半透明纯色罩，透明度由材质颜色 Alpha
    // 驱动（PrologueController 按闪烁频率每帧改写 Alpha）。
    // 罩体微放大浮在踏板表面之外，整块踏板（含朝向玩家的棱面）
    // 一起泛绿闪烁，不依赖描边外壳的轮廓可见性。
    Properties
    {
        _HighlightColor("Highlight Color", Color) = (0, 1, 0.4, 0.55)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Highlight"

            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _HighlightColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS =
                    TransformObjectToHClip(IN.positionOS.xyz);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _HighlightColor;
            }
            ENDHLSL
        }
    }
}
