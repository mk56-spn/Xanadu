// Paints the Game of Life state RdLife.gdshader simulates: round cells that cool
// from white-hot to violet as they age, over a fading trail. Godot does not compile
// Slang, so build the SPIR-V RdLifePainter loads, one file per stage:
//
//   slangc Diagnostics/RdLife.slang -target spirv -entry vertexMain -stage vertex -o Diagnostics/RdLife.vert.spv
//   slangc Diagnostics/RdLife.slang -target spirv -entry fragmentMain -stage fragment -o Diagnostics/RdLife.frag.spv

// Matches RdLife.Cells.
static const float Cells = 128.0;

[[vk::binding(0, 0)]] Sampler2D state;

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
    output.uv = corner * 0.5 + 0.5;
    return output;
}

[shader("fragment")]
float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
{
    float4 cell = state.Sample(uv);

    float3 background = float3(0.02, 0.02, 0.05);
    float3 trail = float3(0.10, 0.25, 0.55) * cell.b * cell.b;

    float2 inCell = frac(uv * Cells) - 0.5;
    float disc = smoothstep(0.5, 0.35, length(inCell)) * cell.r;

    float3 young = float3(1.0, 0.95, 0.7);
    float3 old = float3(0.55, 0.2, 0.95);
    float3 alive = lerp(young, old, sqrt(cell.g));

    return float4(lerp(background + trail, alive, disc), 1.0);
}
