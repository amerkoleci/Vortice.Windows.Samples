struct VSInput {
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 Texcoord : TEXCOORD0;
};

struct PSInput {
    float4 Position : SV_POSITION;
    float3 Normal : NORMAL;
    float2 Texcoord : TEXCOORD0;
};

cbuffer params : register(b0) {
    float4x4 worldViewProjection;
};
Texture2D<float4> Texture: register(t0); 
SamplerState TextureSampler: register(s0);

PSInput VSMain(in VSInput input) {
    PSInput result;
    result.Position = mul(worldViewProjection, float4(input.Position, 1.0f));
    result.Normal = input.Normal;
    result.Texcoord = input.Texcoord * 5.0f;
    return result;
}

float4 PSMain(in PSInput input) : SV_TARGET
{
    return Texture.Sample(TextureSampler, input.Texcoord);
}
