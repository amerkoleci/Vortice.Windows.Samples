struct VSInput {
    float3 Position : POSITION;
    float2 Texcoord : TEXCOORD0;
};

struct PSInput {
    float4 Position : SV_POSITION;
    float2 Texcoord : TEXCOORD0;
};

Texture2D<float4> Texture: register(t0); 
SamplerState TextureSampler: register(s0);

PSInput VSMain(in VSInput input) {
    PSInput result;
    result.Position = float4(input.Position, 1);
    result.Texcoord = input.Texcoord;
    return result;
}

float4 PSMain(in PSInput input) : SV_TARGET
{
    return Texture.Sample(TextureSampler, input.Texcoord);
}
