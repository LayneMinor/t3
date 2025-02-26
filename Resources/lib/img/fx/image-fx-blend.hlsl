cbuffer ParamConstants : register(b0)
{
    float4 ImageAColor;
    float4 ImageBColor;
    float ColorMode;
    float AlphaMode;
    float UseNormalForUpperHalf;
}

cbuffer TimeConstants : register(b1)
{
    float globalTime;
    float time;
    float runTime;
    float beatTime;
}

struct vsOutput
{
    float4 position : SV_POSITION;
    float2 texCoord : TEXCOORD;
};

Texture2D<float4> ImageA : register(t0);
Texture2D<float4> ImageB : register(t1);
sampler texSampler : register(s0);

float IsBetween(float value, float low, float high)
{
    return (value >= low && value <= high) ? 1 : 0;
}

float4 psMain(vsOutput psInput) : SV_TARGET
{
    float4 tA = ImageA.Sample(texSampler, psInput.texCoord) * ImageAColor;
    float4 tB = ImageB.Sample(texSampler, psInput.texCoord) * ImageBColor;
    tA.a = clamp(tA.a, 0, 1);
    tB.a = clamp(tB.a, 0, 1);

    float a = tA.a + tB.a - tA.a * tB.a;

    switch ((int)AlphaMode)
    {

        // case 1:
        //     a = tA.a;
        //     break;

    case 1:
        a = tA.a * tB.a;
        break;

    case 2:
        a = 1;
        break;

    case 3:
        a = tA.a;
        break;

    case 4:
        a = tB.a;
        break;

    case 5:
        a = (tA.r + tA.g + tA.b) / 3;
        break;

    case 6:
        a = (tB.r + tB.g + tB.b) / 3;
        break;

    case 7:
        a = tA.a + tB.a;
        break;

    case 8:
        a = max(tA.a, tB.a);
        break;
    }

    float normalRatio = saturate(tB.a * 2 - 1);

    if (UseNormalForUpperHalf > 0.5)
        tB.a = saturate(tB.a * 2);

    // float3 rgb = (1.0 - tB.a)*tA.rgb + tB.a*tB.rgb;
    float3 rgbNormalBlended = (1.0 - tB.a) * tA.rgb + tB.a * tB.rgb;
    float3 rgb = 1;

    switch ((int)ColorMode)
    {
    // normal
    case 0:
        rgb = rgbNormalBlended;
        break;

    // screen
    case 1:
        // rgb = 1 - saturate(1 - tA.rgb) * saturate(1 - tB.rgb * tB.a);
        rgb = tA.rgb + tB.rgb * tB.a;
        break;

    // multiply
    case 2:
        rgb = lerp(tA.rgb, tA.rgb * tB.rgb, tB.a);
        break;

    // overlay
    case 3:
        rgb = float3(
            tA.r < 0.5 ? (2.0 * tA.r * tB.r) : (1.0 - 2.0 * (1.0 - tA.r) * (1.0 - tB.r)),
            tA.g < 0.5 ? (2.0 * tA.g * tB.g) : (1.0 - 2.0 * (1.0 - tA.g) * (1.0 - tB.g)),
            tA.b < 0.5 ? (2.0 * tA.b * tB.b) : (1.0 - 2.0 * (1.0 - tA.b) * (1.0 - tB.b)));

        rgb = lerp(tA.rgb, rgb, tB.a);
        break;

    // difference
    case 4:
        rgb = abs(tA.rgb - tB.rgb) * tB.a + tB.rgb * (1.0 - tB.a);
        break;

    // use a
    case 5:
        rgb = tA.rgb;
        break;

    // use b
    case 6:
        rgb = tB.rgb;
        break;

    case 8:
        rgb = max(tA.rgb, tB.rgb);
        break;
    }

    if (UseNormalForUpperHalf > 0.5)
        rgb = lerp(rgb, rgbNormalBlended, normalRatio);

    // rgb = lerp(tA.rgb, rgb, tB.a);

    return float4(rgb, a);
}
