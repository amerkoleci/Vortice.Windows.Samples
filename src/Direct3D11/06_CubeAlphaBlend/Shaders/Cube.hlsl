struct VSInput {
    float3 Position : POSITION;
    float4 Color : COLOR;
};

struct PSInput {
    float4 Position : SV_POSITION;
    float4 Color: COLOR;
};

cbuffer params : register(b0) {
    float4x4 worldViewProjection;
};

PSInput VSMain(in VSInput input) {
    PSInput result;
    result.Position = mul(worldViewProjection, float4(input.Position, 1.0f));
    result.Color = input.Color;
    return result;
}

float4 PSMain(in PSInput input) : SV_TARGET {
    return input.Color;
}
