struct VSInput {
    float2 Position : POSITION;
    float3 Color : COLOR;
};

struct PSInput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

PSInput VSMain(in VSInput input) {
    PSInput result;
    result.Position = float4(input.Position, 0.5f, 1.0f);
    result.Color = float4(input.Color, 1.0f);
    return result;
}

float4 PSMain(in PSInput input) : SV_TARGET{
    return input.Color;
}
