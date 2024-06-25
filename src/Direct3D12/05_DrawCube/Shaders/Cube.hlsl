struct VSInput {
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float4 Texcoord : TEXCOORD0;
};

struct PSInput {
    float4 Position : SV_POSITION;
    float3 Normal : NORMAL;
    float2 Texcoord : TEXCOORD0;
};

cbuffer params : register(b0) {
    float4x4 worldViewProjection;
};

PSInput VSMain(in VSInput input) {
    PSInput result;
    result.Position = mul(worldViewProjection, float4(input.Position, 1.0f));
    result.Normal = input.Normal;
    result.Texcoord = input.Texcoord;
    return result;
}

float4 PSMain(in PSInput input) : SV_TARGET {
    return float4(input.Normal, 1.0f);
}
