Shader "Custom/Item Outline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.8, 0.2, 1)
        _OutlineWidth("Outline Width (Pixels)", Range(0, 20)) = 6
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
            Name "Outline"

            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 视空间沿法线做 3D 外扩：深度随外扩一致变化，
                // 外壳面片不会与物体表面共面闪现。
                // 外扩量按当前深度处的“每像素对应米数”换算，
                // 得到与观察距离无关的恒定像素线宽。
                float3 normalWS =
                    TransformObjectToWorldNormal(IN.normalOS);

                float3 positionVS =
                    TransformWorldToView(
                        TransformObjectToWorld(IN.positionOS.xyz)
                    );

                float3 normalVS =
                    mul((float3x3)UNITY_MATRIX_V, normalWS);

                // Unity 视空间中摄像机前方的点 Z 为正值，
                // 由此换算当前深度处每像素对应的米数。
                float metersPerPixel =
                    2.0 * positionVS.z *
                    rcp(UNITY_MATRIX_P[1][1]) / _ScreenParams.y;

                positionVS.xyz +=
                    normalVS * (metersPerPixel * _OutlineWidth);

                float4 positionCS =
                    TransformWViewToHClip(positionVS);

                OUT.positionHCS = positionCS;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
