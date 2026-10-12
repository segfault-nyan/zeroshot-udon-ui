// FND-07
// uGUI 用のアイコンシェーダー。画像を使わず、線で構成したアイコンを描く。テクスチャを設定していない RawImage に使う。
// アイコンの種類は、RawImage の UV Rect の X で指定する（種類の番号 × 2）。種類ごとにマテリアルを分けずに済む。
// 番号と形の対応は、UIBuilder の IconType と合わせること。
// 矩形の短辺に合わせた正方形の中に描くので、要素の大きさを変えても形は崩れない。色は RawImage の Color に従う。
Shader "ZeroShotUdonUI/Icon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Thickness ("Line Thickness", Range(0.02, 0.2)) = 0.09

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "ZSCommon.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _Color;
            float _Thickness;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.localPos = v.vertex.xyz;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            // 点 p から、a と b を結ぶ線分までの距離。
            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // 上向きの矢印の先。ほかの向きは、座標を入れ替えてこれを使う。
            float ChevronDistance(float2 p)
            {
                return min(
                    SegmentDistance(p, float2(-0.26, -0.12), float2(0.0, 0.14)),
                    SegmentDistance(p, float2(0.0, 0.14), float2(0.26, -0.12)));
            }

            // アイコンを構成する線の中心までの距離を返す。
            // p は、矩形の短辺が -0.5 から 0.5 になる座標で、上が +Y。
            float IconDistance(float2 p, float icon)
            {
                // 0: 閉じる
                if (icon < 0.5)
                {
                    return min(
                        SegmentDistance(p, float2(-0.28, -0.28), float2(0.28, 0.28)),
                        SegmentDistance(p, float2(-0.28, 0.28), float2(0.28, -0.28)));
                }

                // 1: チェック
                if (icon < 1.5)
                {
                    return min(
                        SegmentDistance(p, float2(-0.30, 0.0), float2(-0.08, -0.22)),
                        SegmentDistance(p, float2(-0.08, -0.22), float2(0.32, 0.24)));
                }

                // 2: プラス
                if (icon < 2.5)
                {
                    return min(
                        SegmentDistance(p, float2(-0.30, 0.0), float2(0.30, 0.0)),
                        SegmentDistance(p, float2(0.0, -0.30), float2(0.0, 0.30)));
                }

                // 3: マイナス
                if (icon < 3.5)
                    return SegmentDistance(p, float2(-0.30, 0.0), float2(0.30, 0.0));

                // 4〜7: 矢印の先（上、下、左、右）
                if (icon < 4.5)
                    return ChevronDistance(p);
                if (icon < 5.5)
                    return ChevronDistance(float2(p.x, -p.y));
                if (icon < 6.5)
                    return ChevronDistance(float2(p.y, -p.x));
                if (icon < 7.5)
                    return ChevronDistance(float2(p.y, p.x));

                // 8: メニュー（三本線）。上下の線は対称なので、Y の絶対値を取って1本分で済ませる。
                if (icon < 8.5)
                {
                    return min(
                        SegmentDistance(p, float2(-0.30, 0.0), float2(0.30, 0.0)),
                        SegmentDistance(float2(p.x, abs(p.y)), float2(-0.30, 0.22), float2(0.30, 0.22)));
                }

                // 9: 情報（丸に i）。i の点は、線より少しだけ大きくする。
                float ring = abs(length(p) - 0.36);
                float stem = SegmentDistance(p, float2(0.0, 0.04), float2(0.0, -0.20));
                float dotMark = length(p - float2(0.0, 0.19)) - 0.012;
                return min(ring, min(stem, dotMark));
            }

            float4 frag(v2f IN) : SV_Target
            {
                // UV の X は「種類の番号 × 2」から始まり、矩形の端から端までで 1 だけ増える。
                float icon = floor(IN.texcoord.x * 0.5 + 0.25);
                float2 uv = float2(IN.texcoord.x - icon * 2.0, IN.texcoord.y);

                // 矩形の短辺に合わせた正方形の中に、中央寄せで描く。
                float2 size = ZSRectSize(uv, IN.localPos);
                float2 p = (uv - 0.5) * size / min(size.x, size.y);

                float dist = IconDistance(p, icon) - _Thickness * 0.5;
                float aa = max(fwidth(dist), 1e-6);

                float4 color = IN.color * _Color;
                color.a *= saturate(0.5 - dist / aa);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.localPos.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
