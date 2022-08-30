cbuffer SceneConstantBuffer : register(b0)
{
    float4 offset;
    float4 padding[15];
};

struct PSInput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

PSInput VSMain(in float3 position : POSITION, in float4 color : COLOR) {
    PSInput result;
    result.Position = float4(position, 1.0f) + offset;
    result.Color = color;
    return result;
}

float4 PSMain(in PSInput input) : SV_TARGET{
    return input.Color;
}
