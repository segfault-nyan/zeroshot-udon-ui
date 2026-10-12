// FND-01
// uGUI 用の角丸矩形シェーダー。Sprite を設定していない Image / RawImage に使う（UV が矩形全体で 0-1 になる前提）。
// 矩形サイズは UV とローカル座標の微分から求めるため、要素ごとの設定やスクリプトは不要（ZSCommon.cginc）。
// 角丸半径と枠線幅は Canvas 単位で指定する。半径が短辺の半分を超える場合はピル形状になる。
Shader "ZeroShotUdonUI/RoundedRect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Radius ("Corner Radius", Float) = 16
        _BorderWidth ("Border Width", Float) = 0
        _BorderColor ("Border Color", Color) = (1,1,1,1)

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

            float4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 size = ZSRectSize(uv, IN.localPos);

                float2 p = (uv - 0.5) * size;
                float radius = min(_Radius, min(size.x, size.y) * 0.5);
                float2 q = abs(p) - (size * 0.5 - radius);
                float dist = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
                float aa = max(fwidth(dist), 1e-6);

                float4 fill = IN.color * _Color;
                // 枠線にも頂点カラーを掛ける。枠線のみの要素でも、ホバーや押下の明るさの変化が見えるようにするため。
                float4 border = _BorderColor * IN.color;
                float borderMask = _BorderWidth > 0.0 ? saturate(0.5 + (dist + _BorderWidth) / aa) : 0.0;

                float4 color = lerp(fill, border, borderMask);
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
