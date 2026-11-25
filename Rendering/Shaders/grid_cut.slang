// An effect's [Grid], cut on the GPU: every box primitive a gridded effect
// draws is a job, cut into columns × rows cells, each cell two triangles of
// canvas vertices written straight into the frame's grid vertex buffer, which
// the effect's draw then reads. What Primitive.TessellateGrid did on the CPU,
// one thread a cell. A ComputeOperation (HostCore/Rendering/Compute): every
// job runs in one dispatch, the job from SV_GroupID.y.
//
// SDL_GPU's compute layout: read-only storage set 0, read-write set 1, uniforms set 2.

struct GridJob
{
    float4 corners01;   // top left, top right: local
    float4 corners23;   // bottom right, bottom left
    float4 uvs01;
    float4 uvs23;
    float4 colour0;     // per corner, the tint already in
    float4 colour1;
    float4 colour2;
    float4 colour3;
    float4 basis;       // the transform's x and y axes
    float4 origin;      // its origin, unused
    uint4 grid;         // columns, rows, first vertex out, first point
};

struct Cut
{
    uint pad0, pad1, pad2, pad3;
};

// A canvas vertex is 17 words (CanvasVertex): too odd a size for a structured
// buffer's alignment, so it is written word by word.
static const uint Words = 17;

[[vk::binding(0, 0)]] StructuredBuffer<GridJob> jobs;
[[vk::binding(0, 1)]] RWStructuredBuffer<uint> vertices;
[[vk::binding(0, 2)]] ConstantBuffer<Cut> cut;

float2 place(GridJob job, float2 p) { return job.origin.xy + job.basis.xy * p.x + job.basis.zw * p.y; }
float2 turn(GridJob job, float2 v) { return job.basis.xy * v.x + job.basis.zw * v.y; }

float2 blend(float2 tl, float2 tr, float2 br, float2 bl, float2 f)
{
    return lerp(lerp(tl, tr, f.x), lerp(bl, br, f.x), f.y);
}

float4 blendColour(GridJob job, float2 f)
{
    return lerp(lerp(job.colour0, job.colour1, f.x), lerp(job.colour3, job.colour2, f.x), f.y);
}

/// A triangle's quad at rest (RestFrame.Of, then Transformed): its uv-(0, 0) corner and its sides.
void restFrame(GridJob job, float2 pa, float2 pb, float2 pc, float2 ua, float2 ub, float2 uc,
    out float2 corner, out float2 xAxis, out float2 yAxis)
{
    float2 d1 = pb - pa, d2 = pc - pa, e1 = ub - ua, e2 = uc - ua;
    float det = e1.x * e2.y - e2.x * e1.y;
    if (abs(det) < 1e-12)
    {
        corner = place(job, (pa + pb + pc) / 3.0);
        xAxis = float2(0, 0);
        yAxis = float2(0, 0);
        return;
    }

    float2 x = (d1 * e2.y - d2 * e1.y) / det;
    float2 y = (d2 * e1.x - d1 * e2.x) / det;
    corner = place(job, pa - x * ua.x - y * ua.y);
    xAxis = turn(job, x);
    yAxis = turn(job, y);
}

void write(uint at, float2 position, float2 uv, float4 colour, float2 local, float2 corner, float2 xAxis, float2 yAxis, uint index)
{
    uint w = at * Words;
    vertices[w + 0] = asuint(position.x);
    vertices[w + 1] = asuint(position.y);
    vertices[w + 2] = asuint(uv.x);
    vertices[w + 3] = asuint(uv.y);
    vertices[w + 4] = asuint(colour.r);
    vertices[w + 5] = asuint(colour.g);
    vertices[w + 6] = asuint(colour.b);
    vertices[w + 7] = asuint(colour.a);
    vertices[w + 8] = asuint(local.x);
    vertices[w + 9] = asuint(local.y);
    vertices[w + 10] = asuint(corner.x);
    vertices[w + 11] = asuint(corner.y);
    vertices[w + 12] = asuint(xAxis.x);
    vertices[w + 13] = asuint(xAxis.y);
    vertices[w + 14] = asuint(yAxis.x);
    vertices[w + 15] = asuint(yAxis.y);
    vertices[w + 16] = index;
}

void triangle(GridJob job, uint at, float2 pa, float2 pb, float2 pc, float2 ua, float2 ub, float2 uc,
    float4 ca, float4 cb, float4 cc, uint ia, uint ib, uint ic)
{
    float2 corner, xAxis, yAxis;
    restFrame(job, pa, pb, pc, ua, ub, uc, corner, xAxis, yAxis);
    write(at, place(job, pa), ua, ca, pa, corner, xAxis, yAxis, ia);
    write(at + 1, place(job, pb), ub, cb, pb, corner, xAxis, yAxis, ib);
    write(at + 2, place(job, pc), uc, cc, pc, corner, xAxis, yAxis, ic);
}

[shader("compute")]
[numthreads(64, 1, 1)]
void cutGrids(uint3 group : SV_GroupID, uint3 thread : SV_GroupThreadID)
{
    GridJob job = jobs[group.y];
    uint columns = job.grid.x, rows = job.grid.y;
    uint cell = group.x * 64 + thread.x;
    if (cell >= columns * rows)
        return;

    uint x = cell % columns, y = cell / columns;
    float2 size = float2(columns, rows);
    float2 f00 = float2(x, y) / size, f10 = float2(x + 1, y) / size, f11 = float2(x + 1, y + 1) / size, f01 = float2(x, y + 1) / size;

    float2 tl = job.corners01.xy, tr = job.corners01.zw, br = job.corners23.xy, bl = job.corners23.zw;
    float2 utl = job.uvs01.xy, utr = job.uvs01.zw, ubr = job.uvs23.xy, ubl = job.uvs23.zw;
    float2 p00 = blend(tl, tr, br, bl, f00), p10 = blend(tl, tr, br, bl, f10), p11 = blend(tl, tr, br, bl, f11), p01 = blend(tl, tr, br, bl, f01);
    float2 u00 = blend(utl, utr, ubr, ubl, f00), u10 = blend(utl, utr, ubr, ubl, f10), u11 = blend(utl, utr, ubr, ubl, f11), u01 = blend(utl, utr, ubr, ubl, f01);
    float4 c00 = blendColour(job, f00), c10 = blendColour(job, f10), c11 = blendColour(job, f11), c01 = blendColour(job, f01);

    // Neighbouring cells share their corners' points, as the CPU numbered them.
    uint stride = columns + 1;
    uint a = job.grid.w + y * stride + x, b = a + 1, d = a + stride, e = d + 1;
    uint at = job.grid.z + cell * 6;
    triangle(job, at, p00, p10, p11, u00, u10, u11, c00, c10, c11, a, b, e);
    triangle(job, at + 3, p00, p11, p01, u00, u11, u01, c00, c11, c01, a, e, d);
}
