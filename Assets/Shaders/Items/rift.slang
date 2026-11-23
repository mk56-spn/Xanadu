// The entrance's rift, round its middle: everything near it the drawer sets
// numbers for each frame, in item units (playfield units, made screen pixels
// with xif_unit) unless a pixel width is said. Charging or dormant: a soft glow,
// motes spiralling in, a ring imploding on the beat, the seam (two lenses), four
// pips counting the beats in and an arrow the way a dash exit goes. Released
// (rift_release.y = 1): a bloom, two shockwaves, sparks flying out and the seam
// flung wide then snapping shut. The flare up and down the field is rift_flare.slang.
import xanadu;

[Default(1)] uniform float xif_unit;
// Half the body's width, the seam's half height.
[Default(30, 120)] uniform float2 rift_size;
// The glow's radii and alpha.
[Default(75, 156, 0.18, 0)] uniform float4 rift_glow;
// The seam: the outer lens's half width, the inner's, their alphas.
[Default(5, 2.4, 0.35, 0.85)] uniform float4 rift_lens;
// The imploding ring: radius, width (pixels), alpha.
[Default(0, 2, 0, 0)] uniform float4 rift_ring;
// The motes: their phase (song ms over one trip), how far out they start, their alpha scale, shown (0 or 1).
[Default(0, 312, 0.35, 0)] uniform float4 rift_motes;
// The pips: beats left to the release, shown (0 or 1), the pip spacing and radius.
[Default(4, 0, 22, 6)] uniform float4 rift_pips;
// The dash exit's arrow: its way (x, y), its alpha, shown (0 or 1).
[Default(0, 0, 0, 0)] uniform float4 rift_exit;
// The release: how far through it (0..1), released (0 or 1).
[Default(0, 0, 0, 0)] uniform float4 rift_release;

static const float3 RiftBlue = float3(0.4, 0.65, 1.0);
static const int Motes = 18;
static const int Sparks = 12;

// A vertical lens, pointed top and bottom: the rift's shape. A distance (item units, roughly).
float lens(float2 p, float halfWidth, float halfHeight)
{
    if (abs(p.y) >= halfHeight)
        return max(abs(p.y) - halfHeight, abs(p.x));
    float u = (p.y + halfHeight) / (2.0 * halfHeight);
    return abs(p.x) - halfWidth * pow(max(sin(PI * u), 0.0), 0.7);
}

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float px = 1.0 / unit;
    float2 p = i.canvas.centred / unit;
    float r = length(p);
    float bodyHalf = rift_size.x, halfHeight = rift_size.y;
    colour c = colours.transparent;

    if (rift_release.y > 0.5)
    {
        float t = saturate(rift_release.x);
        float fade = 1.0 - t;
        float fast = max(0.0, 1.0 - t * 2.5);

        // A quick bloom of light, gone well before the shockwaves are.
        float bloom = saturate(1.0 - r / (halfHeight * (1.0 + 1.2 * t)));
        c.paint(bloom, float4(0.85, 0.92, 1.0, 0.55 * fast * fast));

        // Two shockwaves, the second a little behind.
        float wave = 1.0 - fade * fade;
        c.paint(sdf.stroked(r - halfHeight * (0.6 + 4.0 * wave), (2.0 + 8.0 * fade) * px, px), float4(1.0, 1.0, 1.0, 0.9 * fade));
        float late = saturate((t - 0.15) / 0.85);
        if (late > 0.0)
        {
            float lateWave = 1.0 - (1.0 - late) * (1.0 - late);
            c.paint(sdf.stroked(r - halfHeight * (0.5 + 2.6 * lateWave), (2.0 + 4.0 * (1.0 - late)) * px, px), float4(RiftBlue, 0.8 * (1.0 - late)));
        }

        // Sparks: short streaks racing out all round.
        float sparks = 1e9;
        for (int k = 0; k < Sparks; k++)
        {
            float angle = k * TAU / Sparks + 0.3 * sin(k * 2.1);
            float2 way = float2(cos(angle), sin(angle));
            float reach = halfHeight * (0.5 + (2.8 + 0.8 * sin(k * 1.7)) * wave);
            float length = halfHeight * 0.45 * fade;
            sparks = min(sparks, Line(way * reach, way * (reach + length)).local(p));
        }
        c.paint(sdf.filled(sparks - (1.0 + fade) * px, px), float4(1.0, 1.0, 1.0, fade));

        // The rift flung wide as the player comes through, then snapping shut.
        float shut = saturate(t / 0.35);
        float open = bodyHalf * 1.3 * (1.0 - shut * shut);
        if (open > 0.3 * px)
            c.paint(sdf.filled(lens(p, open, halfHeight * (1.0 + 0.2 * (1.0 - shut))), px), float4(1.0, 1.0, 1.0, 0.95 * (1.0 - shut)));
    }
    else
    {
        // The glow round it.
        float glow = saturate(1.0 - length(p / rift_glow.xy));
        c.paint(glow, float4(RiftBlue, rift_glow.z));

        // Motes spiralling into the rift.
        if (rift_motes.w > 0.5)
        {
            for (int k = 0; k < Motes; k++)
            {
                float f = frac(rift_motes.x + k / float(Motes));
                float away = rift_motes.y * (1.0 - f) * (1.0 - f) + halfHeight * 0.08;
                float angle = k * 2.39996 + f * 1.6;
                float2 at = float2(cos(angle) * away * 0.75, sin(angle) * away);
                float alpha = 4.0 * f * (1.0 - f) * rift_motes.z;
                c.paint(sdf.filled(length(p - at) - (1.5 + 2.0 * f) * px, px), float4(0.75, 0.88, 1.0, alpha));
            }
        }

        // A ring imploding onto the rift on the beat.
        if (rift_ring.z > 0.0)
            c.paint(sdf.stroked(r - rift_ring.x, rift_ring.y * px, px), float4(1.0, 1.0, 1.0, rift_ring.z));

        // The seam: a wide faint lens round a bright sliver; a hairline when shut.
        float outer = max(rift_lens.x, 0.5 * px), inner = max(rift_lens.y, 0.5 * px);
        c.paint(sdf.filled(lens(p, outer, halfHeight * 1.05), px), float4(RiftBlue, rift_lens.z));
        c.paint(sdf.filled(lens(p, inner, halfHeight), px), float4(0.9, 0.96, 1.0, rift_lens.w));

        // Four pips under it, one lighting per beat of the charge.
        if (rift_pips.y > 0.5)
        {
            float left = rift_pips.x;
            float2 row = float2(0.0, halfHeight + 26.0);
            float radius = max(2.5 * px, rift_pips.w);
            for (int k = 0; k < 4; k++)
            {
                float2 pip = row + float2((k - 1.5) * rift_pips.z, 0.0);
                float litFor = (4.0 - left) - k;
                float d = length(p - pip);
                if (litFor >= 0.0)
                {
                    float flash = max(0.0, 1.0 - litFor * 2.0);
                    c.paint(sdf.filled(d - radius * (1.0 + 0.6 * flash), px), float4(1.0, 1.0, 1.0, 0.95));
                }
                else
                    c.paint(sdf.stroked(d - radius, 1.5 * px, px), float4(RiftBlue, 0.6));
            }
        }

        // The arrow the way a dash exit launches the player.
        if (rift_exit.w > 0.5)
        {
            float2 way = rift_exit.xy;
            float2 side = float2(-way.y, way.x) * halfHeight * 0.25;
            float2 tip = way * halfHeight * 1.9;
            float2 back = tip - way * halfHeight * 0.3;
            float arrow = min(Line(way * halfHeight * 0.7, tip).local(p),
                          min(Line(tip, back + side).local(p), Line(tip, back - side).local(p)));
            c.paint(sdf.filled(arrow - 1.5 * px, px), float4(1.0, 1.0, 1.0, 0.85 * rift_exit.z));
        }
    }

    // The vertex colour carries self modulate and fades.
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}
