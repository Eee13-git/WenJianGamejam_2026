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

                float t = _Time.y * 3.0;
                // 单次噪声（原为两次，优化为一次）
                float n = noise2D(centered * 12.0 + t);
                float distortedRadius = _CurrentRadius + (n - 0.5) * _Distortion * _MaxRadius * 0.4;

                float ringHalf = _RingWidth;
                float distToRing = abs(worldDist - distortedRadius);
                float ring = 1.0 - smoothstep(0.0, ringHalf, distToRing);

                float innerFill = (1.0 - smoothstep(0.0, _CurrentRadius * 0.9, worldDist)) * 0.15;
                float pulse = 0.8 + 0.2 * sin(t * 4.0 + dist * 30.0);

                float4 col = _Color * ring * pulse;
                col += _ColorInner * innerFill;

                float radiusFade = 1.0 - smoothstep(_MaxRadius * 0.85, _MaxRadius, worldDist);
                col.a *= (ring + innerFill) * _Fade * radiusFade;

                if (worldDist > distortedRadius + ringHalf && innerFill < 0.01)
                    col.a = 0.0;

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

                float t = _Time.y * 3.0;
                float n = noise2D(centered * 12.0 + t);
                float distortedRadius = _CurrentRadius + (n - 0.5) * _Distortion * _MaxRadius * 0.4;

                float ringHalf = _RingWidth;
                float distToRing = abs(worldDist - distortedRadius);
                float ring = 1.0 - smoothstep(0.0, ringHalf, distToRing);

                float innerFill = (1.0 - smoothstep(0.0, _CurrentRadius * 0.9, worldDist)) * 0.15;
                float pulse = 0.8 + 0.2 * sin(t * 4.0 + dist * 30.0);

                float4 col = _Color * ring * pulse;
                col += _ColorInner * innerFill;

                float radiusFade = 1.0 - smoothstep(_MaxRadius * 0.85, _MaxRadius, worldDist);
                col.a *= (ring + innerFill) * _Fade * radiusFade;

                if (worldDist > distortedRadius + ringHalf && innerFill < 0.01)
                    col.a = 0.0;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
