Shader "Custom/PoisonGas"
{
    Properties
    {
        _Color ("Inner Color", Color) = (0.25, 0.75, 0.3, 0.7)
        _ColorEdge ("Edge Color", Color) = (0.6, 1.0, 0.5, 0.5)
        _Opacity ("Opacity", Range(0, 1)) = 0.55
        _Turbulence ("Turbulence", Range(0, 1)) = 0.5
        _FlowSpeed ("Flow Speed", Range(0, 3)) = 0.8
        _EdgeSoft ("Edge Soft", Range(0.05, 0.6)) = 0.35
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
                float4 _ColorEdge;
                float _Opacity;
                float _Turbulence;
                float _FlowSpeed;
                float _EdgeSoft;
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

            float noise(float2 p)
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
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * noise(p);
                    p = p * 2.03 + 13.7;
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

                // 裁掉方形 sprite 角落残留（dist 最大 1.414）
                float cornerMask = 1.0 - smoothstep(1.0, 1.05, dist);

                // 气体飘动 + 湍流扰动边缘
                float t = _Time.y * _FlowSpeed;
                float turb = fbm(centered * 3.0 + float2(t * 0.5, t * 0.35));
                float distorted = dist + (turb - 0.5) * _Turbulence * 0.8;

                // 柔边圆（气体边缘比液体更模糊）
                float body = 1.0 - smoothstep(0.55, 0.55 + _EdgeSoft * 1.6, distorted);

                // 内部湍流斑块（浓淡不均，气体翻涌感）
                float inner = fbm(centered * 5.0 - float2(t * 0.8, t * 0.6));
                float patch = smoothstep(0.35, 0.75, inner) * 0.4;

                // 中心深、边缘亮
                float edgeFactor = smoothstep(0.3, 0.95, distorted);
                float4 col = lerp(_Color, _ColorEdge, edgeFactor);
                col.rgb += col.rgb * patch;

                col.a = body * _Opacity * cornerMask;

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
                float4 _ColorEdge;
                float _Opacity;
                float _Turbulence;
                float _FlowSpeed;
                float _EdgeSoft;
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

            float noise(float2 p)
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
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * noise(p);
                    p = p * 2.03 + 13.7;
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

                float cornerMask = 1.0 - smoothstep(1.0, 1.05, dist);

                float t = _Time.y * _FlowSpeed;
                float turb = fbm(centered * 3.0 + float2(t * 0.5, t * 0.35));
                float distorted = dist + (turb - 0.5) * _Turbulence * 0.8;

                float body = 1.0 - smoothstep(0.55, 0.55 + _EdgeSoft * 1.6, distorted);

                float inner = fbm(centered * 5.0 - float2(t * 0.8, t * 0.6));
                float patch = smoothstep(0.35, 0.75, inner) * 0.4;

                float edgeFactor = smoothstep(0.3, 0.95, distorted);
                float4 col = lerp(_Color, _ColorEdge, edgeFactor);
                col.rgb += col.rgb * patch;

                col.a = body * _Opacity * cornerMask;

                if (col.a < 0.01) discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
