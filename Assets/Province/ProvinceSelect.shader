Shader "Reef/ProvinceSelect"
{
    Properties
    {
        _BaseMap ("ID Map", 2D) = "white" {}
        // 用 Vector 而非 Color，避免 Unity 在 Linear 项目下对颜色属性做自动 sRGB->Linear 转换，
        // 保证 _SelectedId 与线性纹理采样得到的原始字节值处于同一基准，可直接比较。
        _SelectedId ("Selected ID", Vector) = (0,0,0,0)
        _HasSelection ("Has Selection", Float) = 0
        _FillTint ("Fill Tint", Color) = (1, 0.92, 0.35, 0.35)
        _OutlineColor ("Outline", Color) = (1, 1, 1, 1)
        _OutlinePixels ("Outline Width", Float) = 2.5
        _SelectionBlend ("Selection Blend", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _BaseMap_ST;
            float4 _BaseMap_TexelSize;
            float4 _SelectedId;
            float _HasSelection;
            float4 _FillTint;
            float4 _OutlineColor;
            float _OutlinePixels;
            float _SelectionBlend;

            struct Attr
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct V2F
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            V2F vert(Attr v)
            {
                V2F o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            // sRGB -> Linear，仅用于屏幕显示补偿（_BaseMap 为线性纹理，存储的是原始 sRGB 字节值）
            float3 SRGBToLinear(float3 c)
            {
                float3 lo = c / 12.92;
                float3 hi = pow(max((c + 0.055) / 1.055, 0.0), 2.4);
                return lerp(lo, hi, step(float3(0.04045, 0.04045, 0.04045), c));
            }

            bool SameId(float3 a, float3 b)
            {
                // 容差约 10/255，覆盖纹理压缩带来的少量误差，又远小于任意两省份间的色距
                return distance(a, b) < 0.04;
            }

            float4 frag(V2F i) : SV_Target
            {
                // 线性纹理：采样得到原始字节值/255，无任何解码，与 C# 端传的 _SelectedId 同源
                float3 selfId = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float3 displayColor = SRGBToLinear(selfId);

                if (_HasSelection < 0.5)
                    return float4(displayColor, 1);

                if (!SameId(selfId, _SelectedId.rgb))
                    return float4(displayColor, 1);

                // 8 方向邻域（含对角线）检测边缘，描边更连续
                float2 px = _BaseMap_TexelSize.xy * _OutlinePixels;
                float2 offsets[8] =
                {
                    float2( px.x,  0   ),
                    float2(-px.x,  0   ),
                    float2( 0,    px.y),
                    float2( 0,   -px.y),
                    float2( px.x,  px.y),
                    float2( px.x, -px.y),
                    float2(-px.x,  px.y),
                    float2(-px.x, -px.y)
                };

                bool edge = false;
                [unroll]
                for (int k = 0; k < 8; k++)
                {
                    float3 neighbor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv + offsets[k]).rgb;
                    edge = edge || !SameId(neighbor, _SelectedId.rgb);
                }

                if (edge)
                    return float4(lerp(displayColor, _OutlineColor.rgb, _SelectionBlend), 1);

                float blend = _FillTint.a * _SelectionBlend;
                return float4(lerp(displayColor, _FillTint.rgb, blend), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}