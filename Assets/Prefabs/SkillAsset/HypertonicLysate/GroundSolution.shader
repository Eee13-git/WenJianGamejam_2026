Shader "Custom/GroundSolution"
{
    Properties
    {
        _Radius ("Radius", Float) = 2
        _Color ("Color", Color) = (0.55, 0.25, 0.7, 0.85)
        _ColorEdge ("Edge Color", Color) = (0.75, 0.45, 0.95, 0.5)
        _BubbleSpeed ("Bubble Speed", Float) = 2
        _BubbleDensity ("Bubble Density", Float) = 12
        _BubbleStrength ("Bubble Strength", Range(0,1)) = 0.5
        _Opacity ("Opacity", Range(0,1)) = 0.85
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
                float _Radius;
                float4 _Color;
                float4 _ColorEdge;
                float _BubbleSpeed;
                float _BubbleDensity;
                float _BubbleStrength;
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

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

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
                // 中心化 UV，dist 0~1
                float2 centered = IN.uv - 0.5;
                float dist = length(centered) * 2.0;

                // 圆形遮罩：内部实，边缘柔和
                float circle = 1.0 - smoothstep(0.82, 1.0, dist);

                // 边缘颜色渐变：中心深、边缘亮（溶液厚度感）
                float edgeFactor = smoothstep(0.3, 0.9, dist);
                float4 col = lerp(_Color, _ColorEdge, edgeFactor);

                // 冒泡动画：细胞格噪声气泡上浮
                float t = _Time.y * _BubbleSpeed;
                float2 bubbleUv = centered * _BubbleDensity - float2(0.0, t * 0.6);
                float2 cell = floor(bubbleUv);
                float cellHash = hash21(cell + 7.7);
                float bubbleCell = step(0.6, cellHash);          // 部分格子有气泡
                float2 cellFrac = frac(bubbleUv) - 0.5;
                float bubbleDist = length(cellFrac);
                float bubble = bubbleCell * (1.0 - smoothstep(0.08, 0.2, bubbleDist));

                // 气泡在液面内闪烁（上浮消失）
                bubble *= 0.5 + 0.5 * sin(cellHash * 20.0 + t * 3.0);
                bubble *= _BubbleStrength;

                // 液面轻微波动（径向涟漪）
                float ripple = sin(dist * 18.0 - t * 4.0) * 0.05 + 1.0;

                col.rgb += float3(1.0, 0.8, 1.0) * bubble * 0.6;
                col.rgb *= ripple;
                col.a = circle * _Opacity;

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
                float _Radius;
                float4 _Color;
                float4 _ColorEdge;
                float _BubbleSpeed;
                float _BubbleDensity;
                float _BubbleStrength;
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

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

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

                float circle = 1.0 - smoothstep(0.82, 1.0, dist);

                float edgeFactor = smoothstep(0.3, 0.9, dist);
                float4 col = lerp(_Color, _ColorEdge, edgeFactor);

                float t = _Time.y * _BubbleSpeed;
                float2 bubbleUv = centered * _BubbleDensity - float2(0.0, t * 0.6);
                float2 cell = floor(bubbleUv);
                float cellHash = hash21(cell + 7.7);
                float bubbleCell = step(0.6, cellHash);
                float2 cellFrac = frac(bubbleUv) - 0.5;
                float bubbleDist = length(cellFrac);
                float bubble = bubbleCell * (1.0 - smoothstep(0.08, 0.2, bubbleDist));

                bubble *= 0.5 + 0.5 * sin(cellHash * 20.0 + t * 3.0);
                bubble *= _BubbleStrength;

                float ripple = sin(dist * 18.0 - t * 4.0) * 0.05 + 1.0;

                col.rgb += float3(1.0, 0.8, 1.0) * bubble * 0.6;
                col.rgb *= ripple;
                col.a = circle * _Opacity;

                if (col.a < 0.01) discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
