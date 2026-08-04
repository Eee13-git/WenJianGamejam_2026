Shader "Custom/StreptoElectricArc"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.3, 0.9, 1.0, 1.0)
        _ArcIntensity ("Arc Intensity", Range(0.5, 4.0)) = 2.0
        _ArcWidth ("Arc Width", Range(0.01, 0.3)) = 0.08
        _JitterStrength ("Jitter Strength", Range(0.0, 0.5)) = 0.15
        _JitterFrequency ("Jitter Frequency", Range(1.0, 20.0)) = 8.0
        _GlowFalloff ("Glow Falloff", Range(0.5, 4.0)) = 2.0
        _BaseMap ("Base Map (optional)", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _ArcIntensity;
                float _ArcWidth;
                float _JitterStrength;
                float _JitterFrequency;
                float _GlowFalloff;
                float4 _BaseMap_ST;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // 简单哈希噪声
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // 平滑噪声
            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // 粒子 UV 居中: uv.x = 沿粒子长度方向, uv.y = 垂直方向
                // 电弧沿 x 方向延伸，y 方向形成弧线

                // 生成电弧路径: 沿 x 方向用噪声偏移 y 中心
                float arcNoise = noise2D(float2(uv.x * _JitterFrequency, 0.0));
                float arcNoise2 = noise2D(float2(uv.x * _JitterFrequency * 2.3, 5.0));
                float arcCenter = (arcNoise - 0.5) * 2.0 * _JitterStrength
                                + (arcNoise2 - 0.5) * 2.0 * _JitterStrength * 0.5;

                // 当前像素到电弧中心的距离
                float distFromArc = abs(uv.y - 0.5 - arcCenter);

                // 电弧核心: 中心窄线
                float arcCore = smoothstep(_ArcWidth, 0.0, distFromArc);

                // 电弧辉光: 外围渐变
                float arcGlow = pow(max(0.0, 1.0 - distFromArc * 2.0), _GlowFalloff);

                // 边缘淡出: 粒子首尾渐隐
                float edgeFade = smoothstep(0.0, 0.15, uv.x) * smoothstep(1.0, 0.85, uv.x);

                // 合并
                float arcAlpha = (arcCore * 1.5 + arcGlow * 0.4) * edgeFade;
                arcAlpha = saturate(arcAlpha);

                // 颜色: BaseColor * 顶点色 * 强度
                float3 rgb = _BaseColor.rgb * input.color.rgb * _ArcIntensity;

                // 可选贴图叠加
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw);
                rgb *= tex.rgb;

                float alpha = arcAlpha * _BaseColor.a * input.color.a;

                // Fog
                rgb = MixFog(rgb, input.fogFactor);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
