// Xanadu Labs' CJK grid, GPU path. The grid lives on the GPU as two buffers
// that stay put between frames:
//
//   glyphs: one entry per character, where its image sits in the text atlas
//           and where it sits in a cell; written once, the first time the
//           character is used.
//   cells:  one uint per cell, the character it shows: a reference into
//           glyphs. A cell's place comes from its index and the column count,
//           and its colour from its character, so nothing else is stored.
//
// Changing a cell is a patch (cell, character) that applyPatches writes in a
// compute pass. The grid is then one instanced draw per atlas page in use:
// each draw has that page bound, and a cell whose glyph is on another page
// makes no triangle.
//
// SDL_GPU's SPIR-V layout: compute reads storage buffers from set 0, writes them
// in set 1, uniforms in set 2; vertex storage set 0, uniforms set 1; fragment
// samplers set 2.

import xanadu.colour;

struct Glyph
{
    float4 uv;      // the atlas rect: min u, v, max u, v
    float4 box;     // offset from the cell's top left x, y, and size w, h (px)
    float4 page;    // atlas page, unused ×3
};

struct Grid
{
    float4 layout;  // columns, cell size (px), cell count, the atlas page this draw has bound
    float4 stage;   // width, height (px), grid origin x, y (px)
};

struct Patches
{
    float4 count;   // patches this frame, unused ×3
};

[[vk::binding(0, 0)]] StructuredBuffer<uint2> patches;
[[vk::binding(0, 1)]] RWStructuredBuffer<uint> cellsOut;
[[vk::binding(0, 2)]] ConstantBuffer<Patches> patchFrame;

[[vk::binding(0, 0)]] StructuredBuffer<uint> cells;
[[vk::binding(1, 0)]] StructuredBuffer<Glyph> glyphs;
[[vk::binding(0, 1)]] ConstantBuffer<Grid> grid;

[[vk::binding(0, 2)]] Sampler2D atlasPage;

/// Writes each patch's character into its cell.
[shader("compute")]
[numthreads(64, 1, 1)]
void applyPatches(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= uint(patchFrame.count.x))
        return;
    uint2 patch = patches[id.x];
    cellsOut[patch.x] = patch.y;
}

struct Varyings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
    nointerpolation float4 tint : TEXCOORD1;
};

static const float2 Corners[6] =
{
    float2(0, 0), float2(1, 0), float2(1, 1),
    float2(0, 0), float2(1, 1), float2(0, 1)
};

/// The same slow walk round the hue wheel the UI cells take, from the character's number.
colour tintOf(uint character)
{
    return colours.hsv(frac(float(character) * 0.0007), 0.45, 1.0);
}

[shader("vertex")]
Varyings cellVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    uint character = cells[instance];
    Glyph glyph = glyphs[character];
    uint columns = uint(grid.layout.x);
    float cell = grid.layout.y;
    float2 corner = Corners[vertex];

    float2 cellAt = grid.stage.zw + float2(instance % columns, instance / columns) * cell;
    float2 pixel = cellAt + glyph.box.xy + corner * glyph.box.zw;
    float2 ndc = pixel / grid.stage.xy * 2 - 1;

    Varyings o;
    // A glyph on another page is drawn by that page's draw: here every corner lands off screen.
    o.position = abs(glyph.page.x - grid.layout.w) < 0.5 ? float4(ndc.x, -ndc.y, 0, 1) : float4(2, 2, 2, 1);
    o.uv = lerp(glyph.uv.xy, glyph.uv.zw, corner);
    o.tint = tintOf(character);
    return o;
}

[shader("fragment")]
float4 cellShade(Varyings v) : SV_Target
{
    float cover = atlasPage.SampleLevel(v.uv, 0).a;
    float alpha = v.tint.a * cover;
    return float4(v.tint.rgb * alpha, alpha);
}
