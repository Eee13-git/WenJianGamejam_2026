Shader "Custom/KeratinShield"
{
    Properties
    {
        _Color ("Color", Color) = (1.0, 0.82, 0.45, 0.6)
        _EdgeColor ("Edge Color", Color) = (1.0, 0.9, 0.6, 0.9)
        _ShieldRadius ("Shield Radius", Float) = 1.0
        _RingWidth ("Ring Width", Float) = 0.35
        _Opacity ("Opacity", Range(0,1)) = 0.7
        _PatternScale ("Pattern Scale", Float) = 6
        _PulseSpeed ("Pulse Speed", Float) = 3
        _HitFlash ("Hit Flash", Range(0,1)) = 0
        _Break ("Break", Range(0,1)) = 0
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
                float _ShieldRadius;
                float _RingWidth;
                float _Opacity;
                float _PatternScale;
                float _PulseSpeed;
                float _HitFlash;
                float _Break;
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
                float2 centered = IN.uv - 0.5;
                float dist = length(centered) * 2.0;   // 0~1

                // 圆环遮罩：护盾半径处为环带
                float ringDist = abs(dist - _ShieldRadius);
                float ring = 1.0 - smoothstep(0.0, _RingWidth, ringDist);

                // 内部浅色填充（护盾光膜）
                float inner = (1.0 - smoothstep(0.0, _ShieldRadius, dist)) * 0.25;

                // 角质/甲壳图案：极坐标扇形鳞片（流动旋转）
                float angle = atan2(centered.y, centered.x);
                float angNorm = frac(angle / 6.2831853 + _Time.y * 0.05); // 缓慢旋转
                float pattern = hash21(floor(float2(angNorm * _PatternScale, dist * _PatternScale * 1.5)) + 7.7);
                float patternMask = smoothstep(0.45, 0.75, pattern);

                // 边缘辉光：护盾环外侧柔和光晕
                float glow = exp(-abs(dist - _ShieldRadius) * 8.0) * 0.6;

                // 轻微脉冲
                float pulse = 1.0 + 0.08 * sin(_Time.y * _PulseSpeed + dist * 10.0);

                // 颜色：环边缘亮、内部角质色
                float4 col = lerp(_Color, _EdgeColor, ring);
                col.rgb += float3(0.15, 0.1, 0.0) * patternMask * ring * 0.5;
                col.rgb += _EdgeColor.rgb * glow * 0.5;
                col.rgb *= pulse;

                // ═══ 圆形硬遮罩：彻底裁掉方形 sprite 的角落 ═══
                // sprite 是方形，dist 最大 1.414（角落）；护盾半径 1.0 之外的
                // 辉光残影会露出方形轮廓。dist > radius 时直接 alpha 归零。
                float circleMask = 1.0 - smoothstep(_ShieldRadius, _ShieldRadius + 0.05, dist);
                col.a = (ring + inner + glow * 0.5) * _Opacity * circleMask;

                // 命中闪白：受击瞬间整体发白
                col.rgb = lerp(col.rgb, float3(1.0, 1.0, 1.0), _HitFlash);
                col.a = max(col.a, _HitFlash * 0.8 * circleMask);

                // 破碎：裂纹噪声（随时间扩散的径向裂纹）
                float crack = hash21(floor(float2(angNorm * 10.0, dist * 20.0)) + 3.3);
                float crackMask = step(1.0 - _Break * 0.8, crack);
                col.rgb += float3(0.2, 0.15, 0.05) * crackMask * _Break;
                // 破碎时边缘碎裂淡出
                float frag = 1.0 - _Break * 0.7;
                col.a *= lerp(1.0, frag, ring) * circleMask;

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
                float _ShieldRadius;
                float _RingWidth;
                float _Opacity;
                float _PatternScale;
                float _PulseSpeed;
                float _HitFlash;
                float _Break;
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

                float ringDist = abs(dist - _ShieldRadius);
                float ring = 1.0 - smoothstep(0.0, _RingWidth, ringDist);

                float inner = (1.0 - smoothstep(0.0, _ShieldRadius, dist)) * 0.25;

                float angle = atan2(centered.y, centered.x);
                float angNorm = frac(angle / 6.2831853 + _Time.y * 0.05);
                float pattern = hash21(floor(float2(angNorm * _PatternScale, dist * _PatternScale * 1.5)) + 7.7);
                float patternMask = smoothstep(0.45, 0.75, pattern);

                float glow = exp(-abs(dist - _ShieldRadius) * 8.0) * 0.6;

                float pulse = 1.0 + 0.08 * sin(_Time.y * _PulseSpeed + dist * 10.0);

                float4 col = lerp(_Color, _EdgeColor, ring);
                col.rgb += float3(0.15, 0.1, 0.0) * patternMask * ring * 0.5;
                col.rgb += _EdgeColor.rgb * glow * 0.5;
                col.rgb *= pulse;

                // ═══ 圆形硬遮罩：彻底裁掉方形 sprite 的角落 ═══
                float circleMask = 1.0 - smoothstep(_ShieldRadius, _ShieldRadius + 0.05, dist);
                col.a = (ring + inner + glow * 0.5) * _Opacity * circleMask;

                col.rgb = lerp(col.rgb, float3(1.0, 1.0, 1.0), _HitFlash);
                col.a = max(col.a, _HitFlash * 0.8 * circleMask);

                float crack = hash21(floor(float2(angNorm * 10.0, dist * 20.0)) + 3.3);
                float crackMask = step(1.0 - _Break * 0.8, crack);
                col.rgb += float3(0.2, 0.15, 0.05) * crackMask * _Break;
                float frag = 1.0 - _Break * 0.7;
                col.a *= lerp(1.0, frag, ring) * circleMask;

                if (col.a < 0.01) discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
