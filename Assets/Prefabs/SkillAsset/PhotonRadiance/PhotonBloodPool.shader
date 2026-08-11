Shader "Custom/PhotonBloodPool"
{
    Properties
    {
        _Color ("Color", Color) = (0.45, 0.015, 0.04, 1.0)     // 暗红（主体血迹）
        _CoreColor ("Core Color", Color) = (0.85, 0.06, 0.08, 1.0)  // 鲜红（中心/涟漪）
        _EdgeColor ("Edge Color", Color) = (0.25, 0.008, 0.02, 1.0) // 深红（边缘干涸）
        _Flash ("Flash", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 0.85
        _PulseSpeed ("Pulse Speed", Float) = 1
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
                float4 _CoreColor;
                float4 _EdgeColor;
                float _Flash;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x),
                    lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x),
                    u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * ValueNoise(p);
                    p = p * 2.1 + float2(5.7, 2.9);
                    a *= 0.55;
                }
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float dist = length(centered) * 2.0;
                float t = _Time.y * _PulseSpeed;

                // ── 有机血迹主体 ──
                // 主边缘：FBM 扰动半径（不规则蔓延的血液）
                float mainNoise = Fbm(centered * 3.5 + float2(0.0, t * 0.12));
                float edgeR = 0.8 + 0.16 * (mainNoise - 0.5);

                // 血迹主体（内部实、边缘渐隐）
                float body = 1.0 - smoothstep(edgeR - 0.12, edgeR, dist);

                // 外沿晕染（血迹蔓延扩散，更淡更不规则）
                float spreadNoise = Fbm(centered * 5.0 - float2(t * 0.08, 0.0));
                float spreadR = edgeR + 0.22 + 0.12 * (spreadNoise - 0.5);
                float spread = (1.0 - smoothstep(edgeR, spreadR, dist)) * 0.4;

                // 细微血管纹理（内部肉质纤维感）
                float vein = Fbm(centered * 8.0 + float2(0.0, t * 0.05));
                float veinMask = body * (0.75 + 0.25 * sin(vein * 20.0)) * 0.5;

                // ── 涟漪（缓慢扩散的血液波纹）──
                float ripplePos = frac(t * 0.1);
                float ripple = 1.0 - smoothstep(0.0, 0.06, abs(dist - ripplePos * edgeR * 0.9));
                ripple *= exp(-dist * 2.5) * 0.3;

                // 中心鲜红（新鲜血液聚集）
                float center = exp(-dist * 3.0);

                // 呼吸脉动（血池起伏，生物感）
                float breath = 1.0 + 0.03 * sin(t * 1.2);

                // ── 合成 ──
                float3 col = _EdgeColor.rgb * (spread * 0.8);
                col += _Color.rgb * (body * 0.85 + veinMask);
                col += _CoreColor.rgb * (center * 0.5 + ripple);
                col *= breath;

                float alpha = (body * 0.9 + spread * 0.5 + center * 0.4 + ripple * 0.5) * _Opacity;

                // 闪白
                col = lerp(col, float3(1, 1, 1), _Flash);

                if (alpha < 0.01) discard;

                return float4(col, alpha);
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
                float4 _CoreColor;
                float4 _EdgeColor;
                float _Flash;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x),
                    lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x),
                    u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * ValueNoise(p);
                    p = p * 2.1 + float2(5.7, 2.9);
                    a *= 0.55;
                }
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float dist = length(centered) * 2.0;
                float t = _Time.y * _PulseSpeed;

                float mainNoise = Fbm(centered * 3.5 + float2(0.0, t * 0.12));
                float edgeR = 0.8 + 0.16 * (mainNoise - 0.5);

                float body = 1.0 - smoothstep(edgeR - 0.12, edgeR, dist);

                float spreadNoise = Fbm(centered * 5.0 - float2(t * 0.08, 0.0));
                float spreadR = edgeR + 0.22 + 0.12 * (spreadNoise - 0.5);
                float spread = (1.0 - smoothstep(edgeR, spreadR, dist)) * 0.4;

                float vein = Fbm(centered * 8.0 + float2(0.0, t * 0.05));
                float veinMask = body * (0.75 + 0.25 * sin(vein * 20.0)) * 0.5;

                float ripplePos = frac(t * 0.1);
                float ripple = 1.0 - smoothstep(0.0, 0.06, abs(dist - ripplePos * edgeR * 0.9));
                ripple *= exp(-dist * 2.5) * 0.3;

                float center = exp(-dist * 3.0);

                float breath = 1.0 + 0.03 * sin(t * 1.2);

                float3 col = _EdgeColor.rgb * (spread * 0.8);
                col += _Color.rgb * (body * 0.85 + veinMask);
                col += _CoreColor.rgb * (center * 0.5 + ripple);
                col *= breath;

                float alpha = (body * 0.9 + spread * 0.5 + center * 0.4 + ripple * 0.5) * _Opacity;

                col = lerp(col, float3(1, 1, 1), _Flash);

                if (alpha < 0.01) discard;

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
