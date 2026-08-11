Shader "Custom/PhotonBeamCore"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.02, 0.05, 1.0)      // 暗血红（主色/外缘）
        _CoreColor ("Core Color", Color) = (0.95, 0.12, 0.1, 1.0)  // 鲜血红（核心/亮部）
        _Mode ("Mode", Float) = 0        // 0=预警环, 1=光束, 2=闪光
        _Flash ("Flash", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 0.9
        _PulseSpeed ("Pulse Speed", Float) = 4
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
                float4 _Color;
                float4 _CoreColor;
                float _Mode;
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

            // ── 简化 FBM 噪声（生物质感血管/肉质扰动）──
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
                    p = p * 2.1 + float2(7.3, 3.1);
                    a *= 0.55;
                }
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float dist = length(centered) * 2.0;
                float t = _Time.y * _PulseSpeed;

                // 圆形硬遮罩（裁掉方形角落）
                float circleMask = 1.0 - smoothstep(0.95, 1.0, dist);

                float4 col = 0;
                float shape = 0;

                if (_Mode < 0.5)
                {
                    // ── Mode 0：血色预警环（血管状有机边缘 + 心跳脉动）──
                    // 心跳脉动（双拍：快收缩 + 慢舒张，生物感）
                    float beat = pow(max(0.0, sin(t * 2.0)), 8.0) * 0.5
                               + pow(max(0.0, sin(t * 2.0 + 0.8)), 4.0) * 0.5;
                    float pulse = 1.0 + 0.08 * beat;

                    // 血管状 FBM 扰动（沿环方向撕裂边缘）
                    float ang = atan2(centered.y, centered.x);
                    float vein = Fbm(float2(ang * 2.0, t * 0.6));
                    float ringR = 0.72 + 0.045 * (vein - 0.5);
                    float ringW = 0.035 + 0.02 * Fbm(float2(ang * 6.0, t * 0.8));

                    // 主环（FBM 扰动半径+宽度，有机感）
                    float ring = 1.0 - smoothstep(0.0, ringW * 2.0, abs(dist - ringR));

                    // 环上细密血管纹理（高频脉动）
                    float veinLine = 0.5 + 0.5 * sin(ang * 12.0 + t * 1.8 + vein * 6.0);
                    float veins = ring * (0.35 + 0.65 * veinLine) * 0.4;

                    // 中心脉搏点（血肉核心）
                    float innerDot = 1.0 - smoothstep(0.0, 0.1, dist);
                    float heart = 0.6 + 0.4 * beat;

                    shape = (ring * 1.3 + innerDot * 0.45 * heart + veins) * pulse;
                    // 深红主体（厚重血液感），鲜红仅作细高光
                    col = _Color * shape * 1.7;
                    col += _CoreColor * (ring * 0.22 + innerDot * 0.35 * heart);
                    col *= 0.72;   // 整体压暗成暗血色
                }
                else if (_Mode < 1.5)
                {
                    // ── Mode 1：血色光柱（从地面向上喷射，生物血柱）──
                    float xDist = abs(centered.x);
                    float y = centered.y + 0.5;   // 0=底部（地面源头）, 1=顶部

                    // 血管状 FBM 扰动宽度（轻微有机感）
                    float vein = Fbm(float2(y * 3.0, t * 0.4));
                    float halfW = 0.14 + 0.03 * (vein - 0.5);

                    // 简洁光柱主体（窄、边缘柔）
                    float beam = 1.0 - smoothstep(halfW * 0.55, halfW * 1.15, xDist);

                    // 中心亮核（极细高亮，底部最强）
                    float core = exp(-dist * 7.0) * 0.9;

                    // 底部辉光（地面血液源头，最亮）
                    float bottomGlow = exp(-y * 16.0) * beam * 1.3;

                    // 向上渐暗（地面→天空，血柱能量消散）
                    float groundFade = 1.0 - y * 0.82;  // 底部=1, 顶部=0.18

                    // 向上涌动的血色条纹（从地面往上的液体感）
                    float flow = 0.5 + 0.5 * sin(y * 18.0 + t * 2.4 + vein * 5.0);
                    float flowMask = beam * (0.55 + 0.45 * flow);

                    // 顶部柔边缘（渐消失于空中，不留硬截断）
                    float topFade = 1.0 - smoothstep(0.82, 1.0, y);

                    shape = (beam * 0.65 + core * 0.75) * groundFade + bottomGlow * 0.6;
                    shape *= topFade;
                    // 暗红血柱主体 + 流动条纹
                    col = _Color * (beam * 0.9 + flowMask * 0.45 + bottomGlow * 0.55);
                    // 鲜红核心 + 底部地面辉光
                    col += _CoreColor * (core * 0.75 + bottomGlow * 0.4);
                    // 底部更鲜红（血液从地面涌出）
                    float fadeY = 1.0 - y;
                    col = lerp(col, _CoreColor * shape, 0.5 * fadeY * fadeY);
                }
                else
                {
                    // ── Mode 2：血色落地闪光（径向爆闪 + 冲击环）──
                    float glow = exp(-dist * 4.5);
                    // 冲击环（随 Flash 向外扩）
                    float shockR = 0.22 + 0.58 * _Flash;
                    float shock = 1.0 - smoothstep(0.0, 0.08, abs(dist - shockR));
                    shock *= 0.8 * (1.0 - _Flash * 0.5);

                    // 放射状血管纹（生物质感的爆闪）
                    float ang = atan2(centered.y, centered.x);
                    float spokes = 0.5 + 0.5 * sin(ang * 8.0 + t * 3.0);
                    float spokeMask = exp(-dist * 2.0) * (0.5 + 0.5 * spokes) * 0.5;

                    shape = glow + shock + spokeMask;
                    // 暗红爆闪主体 + 鲜红冲击环
                    col = _Color * (glow * 1.3 + shock * 0.5);
                    col += _CoreColor * (shock * 0.9 + spokeMask);
                    col *= 0.8;   // 压暗成血色爆闪
                }

                // 闪白（预警闪烁/命中脉冲）
                col.rgb = lerp(col.rgb, float3(1, 1, 1), _Flash);

                col.a = shape * _Opacity * circleMask;
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
                float4 _CoreColor;
                float _Mode;
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
                    p = p * 2.1 + float2(7.3, 3.1);
                    a *= 0.55;
                }
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float dist = length(centered) * 2.0;
                float t = _Time.y * _PulseSpeed;

                float circleMask = 1.0 - smoothstep(0.95, 1.0, dist);

                float4 col = 0;
                float shape = 0;

                if (_Mode < 0.5)
                {
                    float beat = pow(max(0.0, sin(t * 2.0)), 8.0) * 0.5
                               + pow(max(0.0, sin(t * 2.0 + 0.8)), 4.0) * 0.5;
                    float pulse = 1.0 + 0.08 * beat;

                    float ang = atan2(centered.y, centered.x);
                    float vein = Fbm(float2(ang * 2.0, t * 0.6));
                    float ringR = 0.72 + 0.045 * (vein - 0.5);
                    float ringW = 0.035 + 0.02 * Fbm(float2(ang * 6.0, t * 0.8));

                    float ring = 1.0 - smoothstep(0.0, ringW * 2.0, abs(dist - ringR));

                    float veinLine = 0.5 + 0.5 * sin(ang * 12.0 + t * 1.8 + vein * 6.0);
                    float veins = ring * (0.35 + 0.65 * veinLine) * 0.4;

                    float innerDot = 1.0 - smoothstep(0.0, 0.1, dist);
                    float heart = 0.6 + 0.4 * beat;

                    shape = (ring * 1.3 + innerDot * 0.45 * heart + veins) * pulse;
                    // 深红主体（厚重血液感），鲜红仅作细高光
                    col = _Color * shape * 1.7;
                    col += _CoreColor * (ring * 0.22 + innerDot * 0.35 * heart);
                    col *= 0.72;   // 整体压暗成暗血色
                }
                else if (_Mode < 1.5)
                {
                    float xDist = abs(centered.x);
                    float y = centered.y + 0.5;

                    float vein = Fbm(float2(y * 3.0, t * 0.4));
                    float halfW = 0.14 + 0.03 * (vein - 0.5);

                    float beam = 1.0 - smoothstep(halfW * 0.55, halfW * 1.15, xDist);
                    float core = exp(-dist * 7.0) * 0.9;

                    float bottomGlow = exp(-y * 16.0) * beam * 1.3;
                    float groundFade = 1.0 - y * 0.82;
                    float flow = 0.5 + 0.5 * sin(y * 18.0 + t * 2.4 + vein * 5.0);
                    float flowMask = beam * (0.55 + 0.45 * flow);
                    float topFade = 1.0 - smoothstep(0.82, 1.0, y);

                    shape = (beam * 0.65 + core * 0.75) * groundFade + bottomGlow * 0.6;
                    shape *= topFade;
                    col = _Color * (beam * 0.9 + flowMask * 0.45 + bottomGlow * 0.55);
                    col += _CoreColor * (core * 0.75 + bottomGlow * 0.4);
                    float fadeY = 1.0 - y;
                    col = lerp(col, _CoreColor * shape, 0.5 * fadeY * fadeY);
                }
                else
                {
                    float glow = exp(-dist * 4.5);
                    float shockR = 0.22 + 0.58 * _Flash;
                    float shock = 1.0 - smoothstep(0.0, 0.08, abs(dist - shockR));
                    shock *= 0.8 * (1.0 - _Flash * 0.5);

                    float ang = atan2(centered.y, centered.x);
                    float spokes = 0.5 + 0.5 * sin(ang * 8.0 + t * 3.0);
                    float spokeMask = exp(-dist * 2.0) * (0.5 + 0.5 * spokes) * 0.5;

                    shape = glow + shock + spokeMask;
                    col = _Color * (glow * 1.3 + shock * 0.5);
                    col += _CoreColor * (shock * 0.9 + spokeMask);
                    col *= 0.8;
                }

                col.rgb = lerp(col.rgb, float3(1, 1, 1), _Flash);

                col.a = shape * _Opacity * circleMask;
                if (col.a < 0.01) discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
