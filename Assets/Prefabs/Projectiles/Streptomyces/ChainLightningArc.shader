Shader "Custom/ChainLightningArc"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.7, 0.98, 1.0, 1.0)
        _ArcIntensity ("Arc Intensity", Range(1.0, 8.0)) = 4.0
        _FlickerSpeed ("Flicker Speed", Range(0.0, 20.0)) = 8.0
        _FlickerStrength ("Flicker Strength", Range(0.0, 1.0)) = 0.4
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
                float _FlickerSpeed;
                float _FlickerStrength;
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

                // 几何锯齿由 LineRenderer 顶点实现，uv.x 沿闪电长度 0~1
                float edgeFade = smoothstep(0.0, 0.15, uv.x) * smoothstep(1.0, 0.85, uv.x);

                // 沿线噪声闪烁（明亮跳动）
                float flicker = 1.0 - _FlickerStrength * noise2D(float2(uv.x * 6.0, _Time.y * _FlickerSpeed));

                float alpha = saturate(edgeFade * flicker);

                float3 rgb = _BaseColor.rgb * input.color.rgb * _ArcIntensity;

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw);
                rgb *= tex.rgb;

                float alphaOut = alpha * _BaseColor.a * input.color.a;
                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, alphaOut);
            }
            ENDHLSL
        }

        // URP 2D Renderer 使用 Universal2D light mode
        Pass
        {
            Name "ForwardLit2D"
            Tags { "LightMode" = "Universal2D" }

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
                float _FlickerSpeed;
                float _FlickerStrength;
                float4 _BaseMap_ST;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

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
                float edgeFade = smoothstep(0.0, 0.15, uv.x) * smoothstep(1.0, 0.85, uv.x);
                float flicker = 1.0 - _FlickerStrength * noise2D(float2(uv.x * 6.0, _Time.y * _FlickerSpeed));
                float alpha = saturate(edgeFade * flicker);

                float3 rgb = _BaseColor.rgb * input.color.rgb * _ArcIntensity;
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw);
                rgb *= tex.rgb;

                float alphaOut = alpha * _BaseColor.a * input.color.a;
                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, alphaOut);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
