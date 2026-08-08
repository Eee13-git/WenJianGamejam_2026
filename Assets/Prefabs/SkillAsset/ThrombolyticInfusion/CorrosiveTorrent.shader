Shader "Custom/CorrosiveTorrent"
{
    Properties
    {
        _Range ("Range", Float) = 8
        _SpreadAngle ("Spread Angle", Range(0, 6.2832)) = 1.0472
        _Color ("Color", Color) = (0.3, 1.0, 0.5, 0.8)
        _ColorDeep ("Deep Color", Color) = (0.1, 0.6, 0.35, 0.9)
        _FlowSpeed ("Flow Speed", Float) = 3
        _NoiseScale ("Noise Scale", Float) = 3
        _Turbulence ("Turbulence", Float) = 0.35
        _Detail ("Detail", Range(0,1)) = 0.8
        _Opacity ("Opacity", Range(0,1)) = 0.75
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
                float _Range;
                float _SpreadAngle;
                float4 _Color;
                float4 _ColorDeep;
                float _FlowSpeed;
                float _NoiseScale;
                float _Turbulence;
                float _Detail;
                float _Opacity;
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

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * noise2D(p);
                    p *= 2.02;
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
                float distNorm = length(centered) * 2.0;
                float worldDist = distNorm * _Range;

                float angle = atan2(centered.y, centered.x);
                float halfAngle = _SpreadAngle * 0.5;
                float t = _Time.y * _FlowSpeed;

                float2 turbPos = centered * _NoiseScale + float2(t * 0.5, t * 0.3);
                float turb = fbm(turbPos);
                float turb2 = fbm(turbPos * 1.6 + 13.1);

                float angEdge = halfAngle * (1.0 + (turb - 0.5) * _Turbulence * 2.5);
                float angMask = 1.0 - smoothstep(angEdge - 0.35, angEdge, abs(angle));

                float tailStart = _Range * 0.4;
                float tailEnd = _Range * (0.85 + (turb2 - 0.5) * 0.3);
                float distMask = 1.0 - smoothstep(tailStart, tailEnd, worldDist);
                distMask *= smoothstep(0.0, _Range * 0.08, worldDist);

                float largeBlob = fbm(centered * 2.2 + float2(-t * 0.7, t * 0.35));
                float midBlob = fbm(centered * 5.0 + float2(-t * 1.2, 0.0));
                float density = 0.55 + 0.45 * largeBlob;
                density *= 0.7 + 0.3 * midBlob * _Detail;

                float surge1 = sin(worldDist * 1.3 - t * 5.0) * 0.5 + 0.5;
                float surge2 = sin(worldDist * 2.7 - t * 8.0 + 1.7) * 0.5 + 0.5;
                float surge = 0.55 + 0.45 * (surge1 * 0.6 + surge2 * 0.4);

                float streak = 0.5 + 0.5 * sin(angle * 5.0 + worldDist * 0.6 - t * 3.0);
                float angStreak = smoothstep(0.15, 0.85, streak);

                float2 bubbleUv = centered * 14.0 - float2(t * 1.0, t * 0.4);
                float bubbleCell = hash21(floor(bubbleUv) + 7.7);
                float bubble = smoothstep(0.72, 0.84, bubbleCell);
                float2 bubbleFrac = frac(bubbleUv) - 0.5;
                float bubbleDist = length(bubbleFrac);
                bubble *= 1.0 - smoothstep(0.16, 0.34, bubbleDist);

                float2 dropUv = centered * 30.0 - float2(t * 1.8, t * 0.7);
                float dropCell = hash21(floor(dropUv) + 3.1);
                float drop = smoothstep(0.86, 0.93, dropCell);
                float2 dropFrac = frac(dropUv) - 0.5;
                drop *= 1.0 - smoothstep(0.07, 0.2, length(dropFrac));

                float liquidBody = angMask * distMask
                    * density
                    * surge
                    * (0.7 + 0.3 * angStreak);

                liquidBody += bubble * 0.55 * distMask * angMask;
                liquidBody += drop * 0.7 * distMask * angMask;

                float edgeFactor = 1.0 - smoothstep(halfAngle * 0.5, halfAngle, abs(angle));
                float4 col = lerp(_ColorDeep, _Color, 0.3 + 0.7 * edgeFactor + 0.25 * turb2);
                col.rgb *= 0.7 + 0.5 * largeBlob;
                col.rgb += float3(0.2, 0.3, 0.06) * surge * 0.3;
                col.rgb += float3(0.25, 0.35, 0.08) * drop * 1.3;

                // 底色浓郁：alpha 完全由边界遮罩决定（主体 0.95 接近不透明，实心液体），
                // 液体流动细节（浓淡/条缕/气泡/液滴）全部通过颜色 RGB 呈现；
                // 边界随 mask 平滑淡出，不形成硬边
                float mask = angMask * distMask;
                col.a = mask * 0.95f * _Opacity;

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
                float _Range;
                float _SpreadAngle;
                float4 _Color;
                float4 _ColorDeep;
                float _FlowSpeed;
                float _NoiseScale;
                float _Turbulence;
                float _Detail;
                float _Opacity;
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
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * noise2D(p);
                    p *= 2.02;
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
                float distNorm = length(centered) * 2.0;
                float worldDist = distNorm * _Range;

                float angle = atan2(centered.y, centered.x);
                float halfAngle = _SpreadAngle * 0.5;
                float t = _Time.y * _FlowSpeed;

                float2 turbPos = centered * _NoiseScale + float2(t * 0.5, t * 0.3);
                float turb = fbm(turbPos);
                float turb2 = fbm(turbPos * 1.6 + 13.1);

                float angEdge = halfAngle * (1.0 + (turb - 0.5) * _Turbulence * 2.5);
                float angMask = 1.0 - smoothstep(angEdge - 0.35, angEdge, abs(angle));

                float tailStart = _Range * 0.4;
                float tailEnd = _Range * (0.85 + (turb2 - 0.5) * 0.3);
                float distMask = 1.0 - smoothstep(tailStart, tailEnd, worldDist);
                distMask *= smoothstep(0.0, _Range * 0.08, worldDist);

                float largeBlob = fbm(centered * 2.2 + float2(-t * 0.7, t * 0.35));
                float midBlob = fbm(centered * 5.0 + float2(-t * 1.2, 0.0));
                float density = 0.55 + 0.45 * largeBlob;
                density *= 0.7 + 0.3 * midBlob * _Detail;

                float surge1 = sin(worldDist * 1.3 - t * 5.0) * 0.5 + 0.5;
                float surge2 = sin(worldDist * 2.7 - t * 8.0 + 1.7) * 0.5 + 0.5;
                float surge = 0.55 + 0.45 * (surge1 * 0.6 + surge2 * 0.4);

                float streak = 0.5 + 0.5 * sin(angle * 5.0 + worldDist * 0.6 - t * 3.0);
                float angStreak = smoothstep(0.15, 0.85, streak);

                float2 bubbleUv = centered * 14.0 - float2(t * 1.0, t * 0.4);
                float bubbleCell = hash21(floor(bubbleUv) + 7.7);
                float bubble = smoothstep(0.72, 0.84, bubbleCell);
                float2 bubbleFrac = frac(bubbleUv) - 0.5;
                float bubbleDist = length(bubbleFrac);
                bubble *= 1.0 - smoothstep(0.16, 0.34, bubbleDist);

                float2 dropUv = centered * 30.0 - float2(t * 1.8, t * 0.7);
                float dropCell = hash21(floor(dropUv) + 3.1);
                float drop = smoothstep(0.86, 0.93, dropCell);
                float2 dropFrac = frac(dropUv) - 0.5;
                drop *= 1.0 - smoothstep(0.07, 0.2, length(dropFrac));

                float liquidBody = angMask * distMask
                    * density
                    * surge
                    * (0.7 + 0.3 * angStreak);

                liquidBody += bubble * 0.55 * distMask * angMask;
                liquidBody += drop * 0.7 * distMask * angMask;

                float edgeFactor = 1.0 - smoothstep(halfAngle * 0.5, halfAngle, abs(angle));
                float4 col = lerp(_ColorDeep, _Color, 0.3 + 0.7 * edgeFactor + 0.25 * turb2);
                col.rgb *= 0.7 + 0.5 * largeBlob;
                col.rgb += float3(0.2, 0.3, 0.06) * surge * 0.3;
                col.rgb += float3(0.25, 0.35, 0.08) * drop * 1.3;

                // 底色浓郁：alpha 完全由边界遮罩决定（主体 0.95 接近不透明，实心液体），
                // 液体流动细节（浓淡/条缕/气泡/液滴）全部通过颜色 RGB 呈现；
                // 边界随 mask 平滑淡出，不形成硬边
                float mask = angMask * distMask;
                col.a = mask * 0.95f * _Opacity;

                if (col.a < 0.01) discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
