Shader "Custom/AnalgesicField"
{
    Properties
    {
        _Color ("Color", Color) = (0.6, 0.85, 1.0, 0.35)
        _EdgeColor ("Edge Color", Color) = (0.8, 0.95, 1.0, 0.6)
        _FieldRadius ("Field Radius", Float) = 1.1
        _Opacity ("Opacity", Range(0,1)) = 0.6
        _PulseSpeed ("Pulse Speed", Float) = 2.5
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
                float _FieldRadius;
                float _Opacity;
                float _PulseSpeed;
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

                // 圆形硬遮罩（裁掉方形 sprite 角落）
                float circleMask = 1.0 - smoothstep(_FieldRadius, _FieldRadius + 0.05, dist);

                // 内部柔和光膜（镇痛护体）
                float inner = (1.0 - smoothstep(0.0, _FieldRadius, dist)) * 0.5;

                // 边缘柔光
                float glow = exp(-abs(dist - _FieldRadius) * 6.0) * 0.5;

                // 缓慢脉冲（镇痛呼吸感）
                float pulse = 1.0 + 0.06 * sin(_Time.y * _PulseSpeed);

                float4 col = lerp(_Color, _EdgeColor, smoothstep(0.5, _FieldRadius, dist));
                col.rgb *= pulse;

                col.a = (inner + glow) * _Opacity * circleMask;

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
                float _FieldRadius;
                float _Opacity;
                float _PulseSpeed;
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

                float circleMask = 1.0 - smoothstep(_FieldRadius, _FieldRadius + 0.05, dist);

                float inner = (1.0 - smoothstep(0.0, _FieldRadius, dist)) * 0.5;
                float glow = exp(-abs(dist - _FieldRadius) * 6.0) * 0.5;
                float pulse = 1.0 + 0.06 * sin(_Time.y * _PulseSpeed);

                float4 col = lerp(_Color, _EdgeColor, smoothstep(0.5, _FieldRadius, dist));
                col.rgb *= pulse;

                col.a = (inner + glow) * _Opacity * circleMask;

                if (col.a < 0.01) discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
