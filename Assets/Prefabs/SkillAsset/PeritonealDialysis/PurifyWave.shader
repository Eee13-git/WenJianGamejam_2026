Shader "Custom/PurifyWave"
{
    Properties
    {
        _Color ("Color", Color) = (0.7, 1.0, 0.9, 0.8)
        _EdgeColor ("Edge Color", Color) = (1.0, 1.0, 1.0, 0.9)
        _MaxRadius ("Max Radius", Float) = 1.75
        _Opacity ("Opacity", Range(0,1)) = 0.8
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _EdgeColor;
                float _MaxRadius;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(IN.positionOS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float dist = length(centered) * 2.0;

                // 圆形硬遮罩
                float circleMask = 1.0 - smoothstep(_MaxRadius, _MaxRadius + 0.05, dist);

                // 环形光环（净化波）
                float ringDist = abs(dist - _MaxRadius * 0.7);
                float ring = 1.0 - smoothstep(0.0, 0.25, ringDist);

                // 内部净化光
                float inner = (1.0 - smoothstep(0.0, _MaxRadius, dist)) * 0.35;

                float4 col = lerp(_Color, _EdgeColor, ring);
                col.a = (ring + inner) * _Opacity * circleMask;

                if (col.a < 0.01) discard;

                col.rgb = MixFog(col.rgb, IN.fogFactor);
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Forward2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _EdgeColor;
                float _MaxRadius;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float dist = length(centered) * 2.0;

                float circleMask = 1.0 - smoothstep(_MaxRadius, _MaxRadius + 0.05, dist);

                float ringDist = abs(dist - _MaxRadius * 0.7);
                float ring = 1.0 - smoothstep(0.0, 0.25, ringDist);

                float inner = (1.0 - smoothstep(0.0, _MaxRadius, dist)) * 0.35;

                float4 col = lerp(_Color, _EdgeColor, ring);
                col.a = (ring + inner) * _Opacity * circleMask;

                if (col.a < 0.01) discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
