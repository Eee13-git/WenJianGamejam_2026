Shader "Custom/WaveOverlay"
{
    Properties
    {
        _Progress ("Progress", Range(0,1)) = 0
        _WaveCenter ("Wave Center", Vector) = (0,0,0,0)
        _Distortion ("Distortion", Float) = 0.5
        _Tint ("Tint Color", Color) = (0.15, 0.35, 0.6, 0.2)
        _NoiseScale ("Noise Scale", Float) = 15
        _NoiseSpeed ("Noise Speed", Float) = 2
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float4 _WaveCenter;
                float _Distortion;
                float4 _Tint;
                float _NoiseScale;
                float _NoiseSpeed;
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
                float2 worldPos : TEXCOORD1;
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

            // FBM 2 octaves（原为4，优化为2）
            float fbm(float2 p)
            {
                return noise2D(p) * 0.65 + noise2D(p * 2.0) * 0.35;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldPos = worldPos.xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float t = _Time.y * _NoiseSpeed;

                // 单次 FBM（原为两次，优化为一次）
                float n = fbm(uv * _NoiseScale + t);
                float distortion = (n - 0.5) * _Distortion;

                float distToCenter = length(IN.worldPos - _WaveCenter.xy);
                float radialWave = sin(distToCenter * 2.0 - t * 4.0) * 0.5 + 0.5;
                radialWave *= _Progress;

                float4 col = _Tint;
                col.rgb += float3(n * 0.08, n * 0.12, n * 0.15) * _Progress;
                col.rgb += float3(0.1, 0.2, 0.3) * radialWave * 0.3;

                float vignette = 1.0 - smoothstep(0.3, 0.7, length(uv - 0.5));
                col.a = _Tint.a * _Progress * (0.6 + vignette * 0.4);

                col.r += distortion * 0.05 * _Progress;
                col.b -= distortion * 0.05 * _Progress;

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
                float _Progress;
                float4 _WaveCenter;
                float _Distortion;
                float4 _Tint;
                float _NoiseScale;
                float _NoiseSpeed;
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
                float2 worldPos : TEXCOORD1;
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
                return noise2D(p) * 0.65 + noise2D(p * 2.0) * 0.35;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldPos = worldPos.xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float t = _Time.y * _NoiseSpeed;

                float n = fbm(uv * _NoiseScale + t);
                float distortion = (n - 0.5) * _Distortion;

                float distToCenter = length(IN.worldPos - _WaveCenter.xy);
                float radialWave = sin(distToCenter * 2.0 - t * 4.0) * 0.5 + 0.5;
                radialWave *= _Progress;

                float4 col = _Tint;
                col.rgb += float3(n * 0.08, n * 0.12, n * 0.15) * _Progress;
                col.rgb += float3(0.1, 0.2, 0.3) * radialWave * 0.3;

                float vignette = 1.0 - smoothstep(0.3, 0.7, length(uv - 0.5));
                col.a = _Tint.a * _Progress * (0.6 + vignette * 0.4);

                col.r += distortion * 0.05 * _Progress;
                col.b -= distortion * 0.05 * _Progress;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
