// FND-01・FND-07
// ツールキットのシェーダーで共通に使う関数。
#ifndef ZS_COMMON_INCLUDED
#define ZS_COMMON_INCLUDED

// UV の1成分がローカル座標に対してどれだけ変化するかから、その軸方向の矩形の長さを求める。
// スクリーン空間ではなくローカル空間で求めるので、斜めから見ても形が歪まない。
float ZSRectSide(float2 duv, float3 dpdx, float3 dpdy)
{
    float e = dot(dpdx, dpdx);
    float f = dot(dpdx, dpdy);
    float g = dot(dpdy, dpdy);
    float det = max(e * g - f * f, 1e-20);
    float gradSq = (g * duv.x * duv.x - 2.0 * f * duv.x * duv.y + e * duv.y * duv.y) / det;
    return 1.0 / sqrt(max(gradSq, 1e-20));
}

// uGUI の矩形の大きさ（Canvas 単位）を求める。UV が矩形の端から端までで 1 だけ変化する前提。
// 要素ごとの設定やスクリプトなしで、矩形の大きさに合わせた形を描くために使う。
float2 ZSRectSize(float2 uv, float3 localPos)
{
    float3 dpdx = ddx(localPos);
    float3 dpdy = ddy(localPos);
    return float2(
        ZSRectSide(float2(ddx(uv.x), ddy(uv.x)), dpdx, dpdy),
        ZSRectSide(float2(ddx(uv.y), ddy(uv.y)), dpdx, dpdy));
}

#endif
