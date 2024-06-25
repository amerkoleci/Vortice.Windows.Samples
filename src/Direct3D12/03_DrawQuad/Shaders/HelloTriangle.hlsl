struct VSInput {
    float3 Position : POSITION;
    float4 Color : COLOR;
};

struct PSInput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

PSInput VSMain(in VSInput input) {
    PSInput result;
    result.Position = float4(input.Position, 1.0f);
    result.Color = input.Color;
    return result;
}

float4 PSMain(in PSInput input) : SV_TARGET{
    return input.Color;
}
