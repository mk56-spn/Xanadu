// The entrance's rift, round its middle: everything near it the drawer sets
// numbers for each frame, in item units (made screen pixels with `unit`)
// unless a pixel width is said. Charging or dormant: a soft glow, motes
// spiralling in, a ring imploding on the beat, the seam (two lenses), four pips
// counting the beats in and an arrow the way a dash exit goes. Released
// (rift_release.y = 1): a bloom, two shockwaves, sparks flying out and the seam
// flung wide then snapping shut. The flare up and down the field is rift_flare.slang.
import xanadu;

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

static const colour RiftBlue = colour(0.4, 0.65, 1);
static const colour MoteBlue = colour(0.75, 0.88, 1);
static const colour SeamWhite = colour(0.9, 0.96, 1);
static const colour BloomWhite = colour(0.85, 0.92, 1);
static const int Motes = 18;
static const int Sparks = 12;

/// The rift's shape: an upright lens, pointed top and bottom, widest at its
/// middle (half width `halfWidth`, half height `halfHeight`). Roughly a distance.
struct Seam : IShape
{
    Placement placement = Placement();
    float halfWidth = 0;
    float halfHeight = 0;

    __init(float halfWidth, float halfHeight) { this.halfWidth = halfWidth; this.halfHeight = halfHeight; }

    float local(float2 p)
    {
        if (abs(p.y) >= halfHeight)
            return max(abs(p.y) - halfHeight, abs(p.x));
        float along = (p.y + halfHeight) / (2 * halfHeight);
        return abs(p.x) - halfWidth * pow(max(sin(PI * along), 0), 0.7);
    }
}

float4 shade(ItemInput i)
{
    float bodyHalf = rift_size.x * unit, halfHeight = rift_size.y * unit;
    colour c = colours.transparent;

    if (rift_release.y > 0.5)
    {
        float t = saturate(rift_release.x);
        float fade = 1 - t;
        float quick = max(0, 1 - t * 2.5);

        // A quick bloom of light, gone well before the shockwaves are.
        c.paint(Radial(halfHeight * (1 + 1.2 * t)), BloomWhite.withAlpha(0.55 * quick * quick), BloomWhite.withAlpha(0));

        // Two shockwaves, the second a little behind.
        float wave = ease.outQuad(t);
        c.paint(Circle(halfHeight * (0.6 + 4 * wave)).stroked(2 + 8 * fade), colours.white.withAlpha(0.9 * fade));
        float late = saturate((t - 0.15) / 0.85);
        if (late > 0)
            c.paint(Circle(halfHeight * (0.5 + 2.6 * ease.outQuad(late))).stroked(2 + 4 * (1 - late)), RiftBlue.withAlpha(0.8 * (1 - late)));

        // Sparks: short streaks racing out all round.
        for (int k = 0; k < Sparks; k++)
        {
            float2 way = rotate(float2(1, 0), k * TAU / Sparks + 0.3 * sin(k * 2.1));
            float reach = halfHeight * (0.5 + (2.8 + 0.8 * sin(k * 1.7)) * wave);
            c.paint(Line(way * reach, way * (reach + halfHeight * 0.45 * fade), 2 * (1 + fade)), colours.white.withAlpha(fade));
        }

        // The rift flung wide as the player comes through, then snapping shut.
        float shut = saturate(t / 0.35);
        float open = bodyHalf * 1.3 * (1 - shut * shut);
        if (open > 0.3)
            c.paint(Seam(open, halfHeight * (1 + 0.2 * (1 - shut))), colours.white.withAlpha(0.95 * (1 - shut)));
    }
    else
    {
        // The glow round it.
        c.paint(Radial(rift_glow.xy * unit), RiftBlue.withAlpha(rift_glow.z), RiftBlue.withAlpha(0));

        // Motes spiralling into the rift.
        if (rift_motes.w > 0.5)
        {
            for (int k = 0; k < Motes; k++)
            {
                float trip = saw(rift_motes.x + k / float(Motes));
                float away = rift_motes.y * unit * (1 - trip) * (1 - trip) + halfHeight * 0.08;
                float2 at = rotate(float2(1, 0), k * 2.39996 + trip * 1.6) * float2(0.75, 1) * away;
                float shown = 4 * trip * (1 - trip) * rift_motes.z;
                c.paint(Circle(1.5 + 2 * trip).moved(at), MoteBlue.withAlpha(shown));
            }
        }

        // A ring imploding onto the rift on the beat.
        if (rift_ring.z > 0)
            c.paint(Circle(rift_ring.x * unit).stroked(rift_ring.y), colours.white.withAlpha(rift_ring.z));

        // The seam: a wide faint lens round a bright sliver; a hairline when shut.
        c.paint(Seam(i.pixels(rift_lens.x, 0.5), halfHeight * 1.05), RiftBlue.withAlpha(rift_lens.z));
        c.paint(Seam(i.pixels(rift_lens.y, 0.5), halfHeight), SeamWhite.withAlpha(rift_lens.w));

        // Four pips under it, one lighting per beat of the charge.
        if (rift_pips.y > 0.5)
        {
            float left = rift_pips.x;
            float radius = i.pixels(rift_pips.w, 2.5);
            float2 row = float2(0, halfHeight + 26 * unit);
            for (int k = 0; k < 4; k++)
            {
                float2 place = row + float2((k - 1.5) * rift_pips.z * unit, 0);
                float litFor = (4 - left) - k;
                // A lit pip swells for half a beat as it lights; an unlit one is a ring.
                if (litFor >= 0)
                    c.paint(Circle(radius * (1 + 0.6 * max(0, 1 - litFor * 2))).moved(place), colours.white.withAlpha(0.95));
                else
                    c.paint(Circle(radius).moved(place).stroked(1.5), RiftBlue.withAlpha(0.6));
            }
        }

        // The arrow the way a dash exit launches the player.
        if (rift_exit.w > 0.5)
        {
            float2 way = rift_exit.xy;
            float2 side = float2(-way.y, way.x) * halfHeight * 0.25;
            float2 tip = way * halfHeight * 1.9;
            float2 back = tip - way * halfHeight * 0.3;
            Polyline head = Polyline(3).added(back + side).added(tip).added(back - side);
            let arrow = Line(way * halfHeight * 0.7, tip, 3).or(head);
            c.paint(arrow, colours.white.withAlpha(0.85 * rift_exit.z));
        }
    }

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
