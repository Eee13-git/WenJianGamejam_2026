Shader "Custom/DiffusionWave"
{
    Properties
    {
        _CurrentRadius ("Current Radius", Float) = 0
        _MaxRadius ("Max Radius", Float) = 10
        _Progress ("Progress", Range(0,1)) = 0
        _Fade ("Fade", Range(0,1)) = 1
        _RingWidth ("Ring Width", Float) = 0.8
        _Distortion ("Distortion", Float) = 0.15
        _Direction ("Direction", Vector) = (1,0,0,0)
        _SpreadAngle ("Spread Angle", Range(0,6.2832)) = 6.2832
        _Color ("Color", Color) = (0.2, 0.8, 1.0, 1.0)
        _ColorInner ("Inner Color", Color) = (0.6, 0.9, 1.0, 0.6)
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

                float t = _Time.y * 3.0;
                // 单次噪声做边缘扭曲
                float n = noise2D(centered * 12.0 + t);
                float distortedRadius = _CurrentRadius + (n - 0.5) * _Distortion * _MaxRadius * 0.4;

                // ── 单环：只显示扩散环本身，无内圈填充 ──
                float ringHalf = _RingWidth;
                float distToRing = abs(worldDist - distortedRadius);

                // 环内亮、环外淡出（一道清晰的光环）
                float ring = 1.0 - smoothstep(0.0, ringHalf, distToRing);
                // 外缘柔和：环带后半段渐弱，形成"拖尾"在环外
                float trail = 1.0 - smoothstep(ringHalf, ringHalf * 2.5, distToRing - ringHalf);

                float pulse = 0.85 + 0.15 * sin(t * 4.0 + dist * 30.0);

                // 环：亮青色，加法混合下明显发光；外侧带淡拖尾
                float4 col = _Color * ring * pulse * 1.5;
                col += _ColorInner * trail * 0.5;

                // 外缘淡出（接近最大半径）
                float radiusFade = 1.0 - smoothstep(_MaxRadius * 0.9, _MaxRadius, worldDist);
                col.a *= (ring + trail * 0.5) * _Fade * radiusFade * sectorMask;

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

                float t = _Time.y * 3.0;
                float n = noise2D(centered * 12.0 + t);
                float distortedRadius = _CurrentRadius + (n - 0.5) * _Distortion * _MaxRadius * 0.4;

                float ringHalf = _RingWidth;
                float distToRing = abs(worldDist - distortedRadius);

                float ring = 1.0 - smoothstep(0.0, ringHalf, distToRing);
                float trail = 1.0 - smoothstep(ringHalf, ringHalf * 2.5, distToRing - ringHalf);

                float pulse = 0.85 + 0.15 * sin(t * 4.0 + dist * 30.0);

                float4 col = _Color * ring * pulse * 1.5;
                col += _ColorInner * trail * 0.5;

                float radiusFade = 1.0 - smoothstep(_MaxRadius * 0.9, _MaxRadius, worldDist);
                col.a *= (ring + trail * 0.5) * _Fade * radiusFade * sectorMask;

                if (col.a < 0.003)
                    discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
