// FND-01・FND-02
// uGUI 用の角丸矩形シェーダー。Sprite を設定していない Image / RawImage に使う（UV が矩形全体で 0-1 になる前提）。
// 矩形サイズは UV とローカル座標の微分から求めるため、要素ごとの設定やスクリプトは不要（ZSCommon.cginc）。
// 角丸半径と枠線幅は Canvas 単位で指定する。半径が短辺の半分を超える場合はピル形状になる。
//
// 影：_ShadowMargin が 0 より大きいと、矩形の内側に、その幅だけ縮めた形を描き、外側の帯に落ち影を描く。
// 影を付ける要素は、描く範囲を _ShadowMargin だけ外に広げて置く（見た目の大きさが変わらないように）。
// 影用の板を足さずに1枚で描くので、半透明の重ね描きが増えない。
Shader "ZeroShotUdonUI/RoundedRect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Radius ("Corner Radius", Float) = 16
        _BorderWidth ("Border Width", Float) = 0
        _BorderColor ("Border Color", Color) = (1,1,1,1)

        _ShadowMargin ("Shadow Margin", Float) = 0
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0)
        _ShadowBlur ("Shadow Blur", Float) = 24
        _ShadowOffset ("Shadow Offset", Vector) = (0,-6,0,0)

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
            float4 _BorderColor;
            float _Radius;
            float _BorderWidth;
            float _ShadowMargin;
            float4 _ShadowColor;
            float _ShadowBlur;
            float4 _ShadowOffset;
            float4 _ClipRect;

            // 中心が原点、大きさが size、角丸半径が radius の形までの距離。内側は負になる。
            float RoundedBoxDistance(float2 p, float2 size, float radius)
            {
                float2 q = abs(p) - (size * 0.5 - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

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

            float4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 rectSize = ZSRectSize(uv, IN.localPos);
                float2 p = (uv - 0.5) * rectSize;

                // 影を付ける場合は、描く範囲から影の帯の分を除いた大きさが、見た目の形になる。
                float2 size = max(rectSize - 2.0 * _ShadowMargin, 0.0);
                float radius = min(_Radius, min(size.x, size.y) * 0.5);
                float dist = RoundedBoxDistance(p, size, radius);
                float aa = max(fwidth(dist), 1e-6);

                float4 fill = IN.color * _Color;

                // 枠線にも頂点カラーを掛ける。枠線のみの要素でも、ホバーや押下の明るさの変化が見えるようにするため。
                float4 border = _BorderColor * IN.color;
                float borderMask = _BorderWidth > 0.0 ? saturate(0.5 + (dist + _BorderWidth) / aa) : 0.0;

                float4 color = lerp(fill, border, borderMask);
                color.a *= saturate(0.5 - dist / aa);

                // 落ち影。形を少しずらした位置からの距離で、外側に向かって薄くする。形の下に重ねて合成する。
                if (_ShadowMargin > 0.0 && _ShadowColor.a > 0.0)
                {
                    float shadowDist = RoundedBoxDistance(p - _ShadowOffset.xy, size, radius);
                    float blur = max(_ShadowBlur, 1e-3);
                    float shadowAlpha = _ShadowColor.a * IN.color.a * (1.0 - smoothstep(-blur * 0.25, blur, shadowDist));

                    float outAlpha = color.a + shadowAlpha * (1.0 - color.a);
                    color.rgb = (color.rgb * color.a + _ShadowColor.rgb * shadowAlpha * (1.0 - color.a)) / max(outAlpha, 1e-5);
                    color.a = outAlpha;
                }

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
