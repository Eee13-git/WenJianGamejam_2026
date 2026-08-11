Shader "Custom/IntestinalPeristalsisWave"
{
    Properties
    {
        _CurrentRadius ("Current Radius", Float) = 0
        _MaxRadius ("Max Radius", Float) = 8
        _Progress ("Progress", Range(0,1)) = 0
        _Fade ("Fade", Range(0,1)) = 1
        _RingWidth ("Ring Width", Float) = 1.6
        _Distortion ("Distortion", Float) = 0.3
        _Direction ("Direction", Vector) = (1,0,0,0)
        _SpreadAngle ("Spread Angle", Range(0,6.2832)) = 6.2832
        _Color ("Color", Color) = (0.98, 0.62, 0.35, 1.0)
        _ColorInner ("Inner Color", Color) = (1.0, 0.85, 0.65, 0.5)
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
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CurrentRadius;
                float _MaxRadius;
                float _Progress;
                float _Fade;
                float _RingWidth;
                float _Distortion;
                float4 _Direction;
                float _SpreadAngle;
                float4 _Color;
                float4 _ColorInner;
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

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // FBM 3 octave — 气浪湍流
            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.55;
                for (int i = 0; i < 3; i++)
                {
                    v += a * noise2D(p);
                    p *= 2.1;
                    a *= 0.5;
                }
                return v;
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
                float dist = length(centered) * 2.0;
                float worldDist = dist * _MaxRadius;

                // ── 扇区裁剪（spreadAngle<360 时只显示扇形区域；360=全圆跳过）──
                float sectorMask = 1.0;
                if (_SpreadAngle < 6.27)
                {
                    float dirAngle = atan2(_Direction.y, _Direction.x);
                    float pixelAngle = atan2(centered.y, centered.x);
                    float angleDiff = abs(frac((pixelAngle - dirAngle) / 6.2831853 + 0.5) - 0.5) * 6.2831853;
                    sectorMask = smoothstep(_SpreadAngle * 0.5, _SpreadAngle * 0.5 - 0.2, angleDiff);
                }

                float t = _Time.y * 2.5;

                // ── 气浪湍流场：随时间翻涌的 FBM ──
                float2 turbUv = centered * 4.0 + float2(t * 0.4, t * 0.25);
                float turb = fbm(turbUv);
                float turb2 = fbm(turbUv * 1.7 + 7.3);

                // 气浪前缘半径：被湍流扰动的不规则边缘
                float distortedRadius = _CurrentRadius
                    + (turb - 0.5) * _Distortion * _MaxRadius * 0.55;

                // ── 气浪主体：宽软多环叠加（不是单一硬环）──
                // 主前缘：宽过渡，像压缩空气的亮缘
                float edge = 1.0 - smoothstep(0.0, _RingWidth, abs(worldDist - distortedRadius));
                // 外侧第二道余波（空气被推开的二次涟漪）
                float echo = 1.0 - smoothstep(_RingWidth, _RingWidth * 1.6, abs(worldDist - distortedRadius * 1.06));
                echo *= 0.45;

                // 前缘湍流撕裂：噪声让边缘破碎，像真实气浪
                float edgeRip = smoothstep(0.42, 0.62, fbm(centered * 9.0 + float2(-t * 1.2, t * 0.6)));
                edge *= 0.55 + 0.45 * edgeRip;

                // ── 内部气浪翻涌：扩散过的区域里残留的气流（旋转湍流）──
                float insideFill = 1.0 - smoothstep(0.0, distortedRadius * 0.75, worldDist);
                // 环形卷曲气流：旋转噪声产生"肠道蠕动"般的漩涡感
                float angle = atan2(centered.y, centered.x);
                float swirlUv = worldDist * 0.5 - t * 1.2 + angle * 0.8;
                float swirl = 0.5 + 0.5 * sin(swirlUv);
                float churn = fbm(centered * 6.0 + float2(t * 1.0, -t * 0.5));
                insideFill *= 0.3 + 0.7 * (swirl * 0.55 + churn * 0.45);

                // ── 脉冲（气浪呼吸感）──
                float pulse = 0.9 + 0.1 * sin(t * 3.0 + dist * 20.0);

                // 颜色合成：亮橙前缘 + 柔和余波 + 内部暖气流
                float4 col = _Color * edge * pulse * 2.0;
                col += _ColorInner * echo * 0.6;
                col += _ColorInner * insideFill * 0.5;

                // 外缘淡出（气浪消散）
                float radiusFade = 1.0 - smoothstep(_MaxRadius * 0.8, _MaxRadius, worldDist);
                col.a *= (edge * 1.4 + echo * 0.8 + insideFill) * _Fade * radiusFade * sectorMask;

                if (col.a < 0.003)
                    discard;

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
                float _CurrentRadius;
                float _MaxRadius;
                float _Progress;
                float _Fade;
                float _RingWidth;
                float _Distortion;
                float4 _Direction;
                float _SpreadAngle;
                float4 _Color;
                float4 _ColorInner;
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

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.55;
                for (int i = 0; i < 3; i++)
                {
                    v += a * noise2D(p);
                    p *= 2.1;
                    a *= 0.5;
                }
                return v;
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
                float worldDist = dist * _MaxRadius;

                float sectorMask = 1.0;
                if (_SpreadAngle < 6.27)
                {
                    float dirAngle = atan2(_Direction.y, _Direction.x);
                    float pixelAngle = atan2(centered.y, centered.x);
                    float angleDiff = abs(frac((pixelAngle - dirAngle) / 6.2831853 + 0.5) - 0.5) * 6.2831853;
                    sectorMask = smoothstep(_SpreadAngle * 0.5, _SpreadAngle * 0.5 - 0.2, angleDiff);
                }

                float t = _Time.y * 2.5;

                float2 turbUv = centered * 4.0 + float2(t * 0.4, t * 0.25);
                float turb = fbm(turbUv);
                float turb2 = fbm(turbUv * 1.7 + 7.3);

                float distortedRadius = _CurrentRadius
                    + (turb - 0.5) * _Distortion * _MaxRadius * 0.55;

                float edge = 1.0 - smoothstep(0.0, _RingWidth, abs(worldDist - distortedRadius));
                float echo = 1.0 - smoothstep(_RingWidth, _RingWidth * 1.6, abs(worldDist - distortedRadius * 1.06));
                echo *= 0.45;

                float edgeRip = smoothstep(0.42, 0.62, fbm(centered * 9.0 + float2(-t * 1.2, t * 0.6)));
                edge *= 0.55 + 0.45 * edgeRip;

                float insideFill = 1.0 - smoothstep(0.0, distortedRadius * 0.75, worldDist);
                float angle = atan2(centered.y, centered.x);
                float swirlUv = worldDist * 0.5 - t * 1.2 + angle * 0.8;
                float swirl = 0.5 + 0.5 * sin(swirlUv);
                float churn = fbm(centered * 6.0 + float2(t * 1.0, -t * 0.5));
                insideFill *= 0.3 + 0.7 * (swirl * 0.55 + churn * 0.45);

                float pulse = 0.9 + 0.1 * sin(t * 3.0 + dist * 20.0);

                float4 col = _Color * edge * pulse * 2.0;
                col += _ColorInner * echo * 0.6;
                col += _ColorInner * insideFill * 0.5;

                float radiusFade = 1.0 - smoothstep(_MaxRadius * 0.8, _MaxRadius, worldDist);
                col.a *= (edge * 1.4 + echo * 0.8 + insideFill) * _Fade * radiusFade * sectorMask;

                if (col.a < 0.003)
                    discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
