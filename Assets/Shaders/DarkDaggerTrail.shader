Shader "Custom/DarkDaggerTrail"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _CoreColor ("核心色", Color) = (0.12, 0.02, 0.2, 0.85)
        _EdgeColor ("边缘亮色", Color) = (0.7, 0.2, 1.0, 0.5)
        _GlowStrength ("辉光强度", Range(0, 2)) = 1.2
        _EdgeWidth ("边缘宽度", Range(0, 0.5)) = 0.25
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _CoreColor;
            float4 _EdgeColor;
            float _GlowStrength;
            float _EdgeWidth;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(float3(IN.positionOS.xy, 0.0));
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 原贴图 alpha（残影形状）
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float shape = tex.a;

                // 距离场：中心 0 → 边缘 1
                float2 c = IN.uv - 0.5;
                float dist = length(c) * 2.0;

                // 核心（中心深紫黑）
                float core = smoothstep(_EdgeWidth, 0.0, dist) * _CoreColor.a;
                // 边缘亮紫
                float edge = smoothstep(_EdgeWidth, _EdgeWidth * 1.8, dist) * _EdgeColor.a;
                edge *= smoothstep(1.0, _EdgeWidth * 1.8, dist);

                float3 col = _CoreColor.rgb * core + _EdgeColor.rgb * edge * _GlowStrength;
                float a = (core + edge) * shape;

                return half4(col, a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _CoreColor;
            float4 _EdgeColor;
            float _GlowStrength;
            float _EdgeWidth;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(float3(IN.positionOS.xy, 0.0));
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float shape = tex.a;
                float2 c = IN.uv - 0.5;
                float dist = length(c) * 2.0;
                float core = smoothstep(_EdgeWidth, 0.0, dist) * _CoreColor.a;
                float edge = smoothstep(_EdgeWidth, _EdgeWidth * 1.8, dist) * _EdgeColor.a;
                edge *= smoothstep(1.0, _EdgeWidth * 1.8, dist);
                float3 col = _CoreColor.rgb * core + _EdgeColor.rgb * edge * _GlowStrength;
                float a = (core + edge) * shape;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
