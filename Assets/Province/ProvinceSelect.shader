Shader "Reef/ProvinceSelect"
{
    Properties
    {
        // _BaseMap 只提供「显示色」：渗透模式由程序经 MPB 换成 penMap（保持现状）
        _BaseMap ("Base Map (Display)", 2D) = "white" {}
        // _IdMap 是「身份图」：ID 解码与 SameId 判据只看它。
        // 只在 MapMat.mat 里静态赋值，运行时不改、不走 MPB —— 这样 _IdMap_TexelSize 才有值
        //（MPB 设纹理不填 TexelSize），也顺带绕开渗透期 _BaseMap 被换掉导致描边失效的问题。
        _IdMap ("ID Map (Identity)", 2D) = "white" {}
        // 用 Vector 而非 Color，避免 Unity 在 Linear 项目下对颜色属性做自动 sRGB->Linear 转换，
        // 保证 _SelectedId 与线性纹理采样得到的原始字节值处于同一基准，可直接比较。
        _SelectedId ("Selected ID", Vector) = (0,0,0,0)
        _HasSelection ("Has Selection", Float) = 0
        _FillTint ("Fill Tint", Color) = (1, 0.92, 0.35, 0.35)
        _OutlineColor ("Outline", Color) = (1, 1, 1, 1)
        _OutlinePixels ("Outline Width", Float) = 2.5
        _SelectionBlend ("Selection Blend", Range(0,1)) = 0
        // 悬停：_HoverId 同 _SelectedId 必须用 Vector —— Color 属性会被 Unity 做空间转换，
        // 0-1 编码的 ID 会被破坏，悬停判据随之失效。
        _HoverId ("Hover ID", Vector) = (0,0,0,0)
        _HoverStrength ("Hover Strength", Range(0,1)) = 0
        // 纸感参数：shader 默认一律 0（与改动前逐像素等价）；目标观感值写在 MapMat.mat，
        // 这样「改一行 .mat」就能整体回退，且 .mat 漏写新 property 时不会发生视觉突变。
        _PaperWash ("Paper Wash", Range(0,1)) = 0
        _PaperTint ("Paper Tint", Range(0,1)) = 0
        _EdgeInk ("Coast Ink", Color) = (0,0,0,0)
        _Vignette ("Vignette", Range(0,1)) = 0
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

            // 纹理与采样器必须在 CBUFFER 之外（URP 约定，见 LitInput.hlsl:118-125）
            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_IdMap);      SAMPLER(sampler_IdMap);

            // 所有材质属性都放进 UnityPerMaterial，Properties 与 CBUFFER 一一对应；
            // 少写一个就会出现「Inspector 能改、shader 读不到」的静默失效。
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _IdMap_TexelSize;   // 静态纹理，Unity 会填；描边宽度取自它
            float4 _SelectedId;
            float _HasSelection;
            float4 _FillTint;
            float4 _OutlineColor;
            float _OutlinePixels;
            float _SelectionBlend;
            float4 _HoverId;
            float _HoverStrength;
            float _PaperWash;
            float _PaperTint;
            float4 _EdgeInk;
            float _Vignette;
            CBUFFER_END

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

            // 纸质洗淡 + 暖色压色：在 sRGB 空间计算，返回 Linear 颜色。
            // ❗ 只作用于出口显示色，绝不参与 ID 比较（否则拾取与描边会一起坏）。
            float3 PaperLook(float3 sRGB)
            {
                float lum = dot(sRGB, float3(0.2126, 0.7152, 0.0722));
                float3 c = lerp(sRGB, float3(lum, lum, lum), _PaperWash);
                // 纸色乘子 (1.0, 0.961, 0.866)：由 UITheme.PaperBg #E7DEC8 归一得到
                //（shader 读不到 C# 常量，故硬编码在此）
                c *= lerp(float3(1.0, 1.0, 1.0), float3(1.0, 0.961, 0.866), _PaperTint);
                return SRGBToLinear(c);
            }

            bool SameId(float3 a, float3 b)
            {
                // 容差约 10/255，覆盖纹理压缩带来的少量误差，又远小于任意两省份间的色距
                return distance(a, b) < 0.04;
            }

            // 海岸墨线：基于身份图的 4 向边界检测（省-省、省-海 都算边界）。
            // uv 由调用方 saturate 过：wrap=Repeat 时贴图边缘会采到对边，可能长出假墨线。
            bool IsCoast(float2 uv)
            {
                float3 selfId = SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv).rgb;
                float2 px = _IdMap_TexelSize.xy * (_OutlinePixels * 0.5);
                float3 nx0 = SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2( px.x, 0)).rgb;
                float3 nx1 = SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2(-px.x, 0)).rgb;
                float3 ny0 = SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2(0,  px.y)).rgb;
                float3 ny1 = SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2(0, -px.y)).rgb;
                return !SameId(nx0, selfId) || !SameId(nx1, selfId)
                    || !SameId(ny0, selfId) || !SameId(ny1, selfId);
            }

            // 选中描边：8 方向邻域（含对角线）检测边缘，描边更连续。身份判据只看 _IdMap。
            // ❗ 不使用 static const 偏移表：偏移量由 uniform（_IdMap_TexelSize × _OutlinePixels）算出，
            //    不是编译期常量——static const 的初始化式必须是常量（FXC/DXC 下不可移植），
            //    且 static 语义会把描边宽度钉在首次求值的结果上。展开为 8 次显式采样，逐像素语义不变。
            bool SelectedEdge(float2 uv)
            {
                float2 px = _IdMap_TexelSize.xy * _OutlinePixels;
                bool edge = false;
                edge = edge || !SameId(SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2( px.x,  0    )).rgb, _SelectedId.rgb);
                edge = edge || !SameId(SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2(-px.x,  0    )).rgb, _SelectedId.rgb);
                edge = edge || !SameId(SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2( 0,     px.y )).rgb, _SelectedId.rgb);
                edge = edge || !SameId(SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2( 0,    -px.y )).rgb, _SelectedId.rgb);
                edge = edge || !SameId(SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2( px.x,  px.y )).rgb, _SelectedId.rgb);
                edge = edge || !SameId(SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2( px.x, -px.y )).rgb, _SelectedId.rgb);
                edge = edge || !SameId(SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2(-px.x,  px.y )).rgb, _SelectedId.rgb);
                edge = edge || !SameId(SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, uv + float2(-px.x, -px.y )).rgb, _SelectedId.rgb);
                return edge;
            }

            float4 frag(V2F i) : SV_Target
            {
                // wrap=Repeat + 双线性之外，saturate 还保证描边/墨线的邻域采样不会绕到贴图对边
                float2 idUv = saturate(i.uv);

                // 身份路径：只采样 _IdMap（.mat 静态赋值，渗透期不会被替换）。
                // 线性纹理：采样得到原始字节值/255，无任何解码，与 C# 端传的 _SelectedId 同源
                float3 selfId = SAMPLE_TEXTURE2D(_IdMap, sampler_IdMap, idUv).rgb;

                // 显示路径：只采样 _BaseMap（渗透期可能被 MPB 换成 penMap）
                float3 displayColor = PaperLook(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb);

                // 全图海岸墨线（身份判据，只染出口色；_EdgeInk.a = 0 时整段跳过）
                if (_EdgeInk.a > 0.0 && IsCoast(idUv))
                    displayColor = lerp(displayColor, _EdgeInk.rgb, _EdgeInk.a);

                // 悬停：复用 _FillTint 作高亮色，强度减半，避免压过选中态
                if (_HoverStrength > 0.0 && SameId(selfId, _HoverId.rgb))
                    displayColor = lerp(displayColor, _FillTint.rgb, _FillTint.a * _HoverStrength * 0.5);

                // 选中：先判身份，再决定描边或填充
                if (_HasSelection >= 0.5 && SameId(selfId, _SelectedId.rgb))
                {
                    if (SelectedEdge(idUv))
                        displayColor = lerp(displayColor, _OutlineColor.rgb, _SelectionBlend);
                    else
                        displayColor = lerp(displayColor, _FillTint.rgb, _FillTint.a * _SelectionBlend);
                }

                // 暗角：UV 空间（跟着地图走，不跟屏幕），只作用于出口色
                float2 vd = (i.uv - 0.5) * 2.0;
                displayColor *= (1.0 - _Vignette * saturate(dot(vd, vd) * 0.5));

                return float4(displayColor, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
