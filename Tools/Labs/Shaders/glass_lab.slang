// Xanadu Labs' glass lab: liquid-glass panels stacked over a busy backdrop.
// Each panel reads only a copy of what lies under its own footprint (its
// region: the panel plus room for the rim's bend and the frost), never the
// whole screen. The copy carries mips the host blits down, so frost is a
// blurrier mip smoothed by a ring of reads.
//
// SDL_GPU's SPIR-V layout: vertex uniforms set 1; fragment samplers set 2,
// uniforms set 3.

import xanadu.math;
import xanadu.shape;
import xanadu.colour;
import xanadu.filter;
import xanadu.pattern;
import xanadu.paint;

struct Glass
{
    float4 panel;   // centre x, y, width, height (target px)
    float4 region;  // where the backdrop copy was taken from: x, y, width, height (target px)
    float4 look;    // corner radius, rim bevel, rim bend (px), frost (px)
    float4 more;    // colour split (0..1 of the bend), tint amount, shadow, held (0/1)
    float4 tint;    // rgb, content (0/1)
    float4 target;  // width, height, time, shadow margin (px)
};

struct Scene
{
    float4 target;  // width, height, time, unused
};

[[vk::binding(0, 1)]] ConstantBuffer<Glass> glassVertex;
[[vk::binding(0, 3)]] ConstantBuffer<Glass> glass;
[[vk::binding(0, 3)]] ConstantBuffer<Scene> scene;
[[vk::binding(0, 2)]] Sampler2D backdrop;

struct Varyings
{
    float4 position : SV_Position;
};

static const float2 Corners[6] = { float2(0, 0), float2(1, 0), float2(1, 1), float2(0, 0), float2(1, 1), float2(0, 1) };

// ---- the backdrop: busy enough that bending and frost show ------------------

[shader("vertex")]
Varyings full(uint vertex : SV_VertexID)
{
    float2 uv = float2((vertex << 1) & 2, vertex & 2);
    Varyings o;
    o.position = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0.0, 1.0);
    return o;
}

[shader("fragment")]
float4 backdropShade(Varyings v) : SV_Target
{
    float2 p = v.position.xy;
    float2 size = scene.target.xy;
    float time = scene.target.z;

    // A slow hue sweep, then big soft blobs drifting over it.
    colour c = colours.hsv(frac(p.x / size.x * 0.35 + p.y / size.y * 0.15 + time * 0.02), 0.55, 0.35);
    for (int i = 0; i < 6; i++)
    {
        float k = float(i);
        float2 centre = size * float2(0.5 + 0.38 * sin(time * (0.13 + 0.03 * k) + k * 1.7),
            0.5 + 0.36 * cos(time * (0.11 + 0.025 * k) + k * 2.3));
        Circle blob = Circle(size.y * (0.12 + 0.04 * k));
        blob.move(centre);
        c.paint(blob.feathered(p, 60.0), colours.hsv(frac(k / 6.0 + time * 0.03), 0.85, 0.95).withAlpha(0.8));
    }

    // Sharp things the glass has to bend: thin diagonal stripes, a grid, rows of "text".
    c.paint(Stripes(56, 0.12).moved(float2(time * 20.0, 0)).filled(p) * 0.35, colours.white);
    c.paint(Grid(1.5, 64).filled(p) * 0.25, colours.black);
    float2 cell = float2(160.0, 36.0);
    float2 id = floor(p / cell);
    float2 at = p - id * cell;
    float width = 40.0 + 100.0 * random(id);
    Box line = Box(float2(width, 8.0), 4.0);
    line.move(float2(12.0 + width * 0.5, 18.0));
    c.paint(line.filled(at) * step(0.45, random(id + 7.0)) * 0.55, colours.white);
    return c;
}

// ---- a glass panel ------------------------------------------------------------

[shader("vertex")]
Varyings panelVertex(uint vertex : SV_VertexID)
{
    // The panel's quad, grown by the shadow's margin.
    float2 size = glassVertex.panel.zw + glassVertex.target.w * 2.0;
    float2 pixel = glassVertex.panel.xy - size * 0.5 + Corners[vertex] * size;
    float2 ndc = pixel / glassVertex.target.xy * 2.0 - 1.0;
    Varyings o;
    o.position = float4(ndc.x, -ndc.y, 0.5, 1.0);
    return o;
}

/// The backdrop copy at a target pixel, frosted over `frost` px; reads past the copy's edge stop at it.
colour behind(float2 pixel, float frost)
{
    float2 low = glass.region.xy + 1.0, high = glass.region.xy + glass.region.zw - 1.0;
    float2 uv = (clamp(pixel, low, high) - glass.region.xy) / glass.region.zw;
    return pictures.of(backdrop).blurred(uv, frost);
}

[shader("fragment")]
float4 panelShade(Varyings v) : SV_Target
{
    float2 p = v.position.xy;
    float2 centre = glass.panel.xy, size = glass.panel.zw;
    float radius = glass.look.x, bevel = glass.look.y, bend = glass.look.z, frost = glass.look.w;
    float split = glass.more.x, held = glass.more.w;

    Box slab = Box(size, radius);
    slab.move(centre);
    float d = slab.distance(p);

    // The drop shadow under it: only where the panel isn't.
    Box shade = Box(size, radius);
    shade.move(centre + float2(0.0, 10.0));
    float shadow = glass.more.z * (1.0 - smoothstep(-16.0, glass.target.w, shade.distance(p)));
    float cover = sdf.filled(d);
    if (cover <= 0.0)
        return float4(0.0, 0.0, 0.0, shadow);

    // Through the glass: the rim bends the view, red and blue a little more and less than green.
    float2 outward = slab.outward(p);
    float2 push = filter.glassRim(d, outward, bevel, bend);
    colour c;
    if (split > 0.0)
    {
        c.r = behind(p + push * (1.0 + split), frost).r;
        c.g = behind(p + push, frost).g;
        c.b = behind(p + push * (1.0 - split), frost).b;
        c.a = 1.0;
    }
    else
        c = behind(p + push, frost);

    c.tint(colour(glass.tint.rgb), glass.more.y);
    c.brighten(1.06);

    // Light from the top left catching the rim, a weaker kick on the far side, and a fine edge line.
    float rim = pow(1.0 - saturate(-d / max(bevel, 1.0)), 3.0);
    float2 light = normalize(float2(-1.0, -1.3));
    float facing = dot(outward, light);
    float shine = rim * (pow(saturate(facing), 2.0) * 0.9 + pow(saturate(-facing), 2.0) * 0.35);
    c.paint(saturate(shine), colours.white);
    c.paint(slab.outlined(p, 1.2) * (0.35 + 0.4 * held), colours.white);
    c.paint(rim * 0.12 * saturate(outward.y), colours.black);

    // What sits on the panel: a title, a few lines of text, an icon. Lower panels' content is what
    // the upper ones have to show bent and frosted.
    if (glass.tint.a > 0.5)
    {
        float2 corner = centre - size * 0.5;
        Box title = Box(float2(min(size.x * 0.45, 220.0), 14.0), 7.0);
        title.move(corner + float2(24.0 + title.size.x * 0.5, 32.0));
        c.paint(title.filled(p) * 0.9, colours.white);
        for (int i = 0; i < 4; i++)
        {
            float y = 64.0 + float(i) * 20.0;
            if (y > size.y - 28.0)
                break;
            float w = (size.x - 48.0) * (0.55 + 0.35 * frac(float(i) * 0.618 + glass.panel.z * 0.01));
            Box text = Box(float2(w, 8.0), 4.0);
            text.move(corner + float2(24.0 + w * 0.5, y));
            c.paint(text.filled(p) * 0.55, colours.white);
        }
        Circle icon = Circle(16.0);
        icon.move(corner + float2(size.x - 40.0, 34.0));
        c.paint(icon.filled(p) * 0.9, colour(glass.tint.rgb).saturatedBy(1.6).brightened(1.3));
    }

    // Premultiplied out: the glass where it covers, its shadow round the antialiased edge.
    return float4(c.rgb * cover, cover + shadow * (1.0 - cover));
}

// ---- where the copies were taken from (SHOW REGIONS) ---------------------------

[shader("fragment")]
float4 regionShade(Varyings v) : SV_Target
{
    float2 p = v.position.xy;
    Box box = Box(glass.region.zw, 0.0);
    box.move(glass.region.xy + glass.region.zw * 0.5);
    float dash = step(0.5, frac((p.x + p.y) / 16.0));
    float a = box.outlined(p, 2.0) * dash;
    if (a <= 0.0)
        discard;
    return float4(colours.yellow.rgb * a, a);
}

[shader("vertex")]
Varyings regionVertex(uint vertex : SV_VertexID)
{
    float2 pixel = glassVertex.region.xy + Corners[vertex] * glassVertex.region.zw;
    float2 ndc = pixel / glassVertex.target.xy * 2.0 - 1.0;
    Varyings o;
    o.position = float4(ndc.x, -ndc.y, 0.5, 1.0);
    return o;
}
