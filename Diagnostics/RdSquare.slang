// Plasma for the RenderingDevice square. Godot does not compile Slang, so build
// the SPIR-V RdSquareTexture loads, one file per stage:
//
//   slangc Diagnostics/RdSquare.slang -target spirv -entry vertexMain -stage vertex -o Diagnostics/RdSquare.vert.spv
//   slangc Diagnostics/RdSquare.slang -target spirv -entry fragmentMain -stage fragment -o Diagnostics/RdSquare.frag.spv
//
// Do not pass -fvk-use-entrypoint-name: Godot looks for an entry point named "main".

struct Params
{
    float time;
    float3 padding; // float3 aligns to 16, so the block is 28 bytes: PushConstantSize in RdSquareTexture.
};

[[vk::push_constant]] ConstantBuffer<Params> params;

struct VertexOutput
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

[shader("vertex")]
VertexOutput vertexMain(float2 corner : POSITION)
{
    VertexOutput output;
    output.position = float4(corner, 0.0, 1.0);
    output.uv = corner;
    return output;
}

// Slang generics, because plain GLSL has no way to say this.
T wave<T : __BuiltinFloatingPointType>(T value, T speed)
{
    return sin(value * speed);
}

[shader("fragment")]
float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
{
    float t = params.time;
    float swirl = wave(uv.x * 4.0 + t, 1.0f)
        + wave(uv.y * 5.0 - t * 1.3, 1.0f)
        + wave(length(uv) * 9.0 - t * 2.0, 1.0f);
    float shade = swirl / 6.0 + 0.5;

    float3 dark = float3(0.45, 0.08, 0.02);
    float3 bright = float3(1.0, 0.72, 0.15);
    return float4(lerp(dark, bright, shade), 1.0);
}
