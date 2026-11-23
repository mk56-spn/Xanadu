// xanadu.filter: post-processing. A picture is something to read colours from
// (the screen drawn so far, the item's own texture, any texture), and filters
// read it several times to give one colour: blurred, sharpened, pixelated,
// split into colour fringes…
//
//     let behind = pictures.screen();
//     colour c = behind.blurred(i.screenUv, 12);                  // frosted glass
//     colour crt = behind.aberrated(filter.curved(i.screenUv, 0.1), 3);
//     c.brighten(filter.vignette(i.screenUv, 0.4));
//     c.rgb += filter.grain(i.pixel, 0.05);
//     float2 push = filter.shockwave(i.pixel, centre, 300 * pop.progress, 40, 20);
//
// Points on a picture are its uv (0..1 across); sizes are in its pixels. The
// filter. functions without a picture work on uv or a pixel and hand back a
// number or a moved uv, so they combine with anything.
module filter;
import xanadu.math;
import xanadu.stage;
import xanadu.colour;

/// Something to read colours from: the screen so far, the item's texture or any texture.
public interface IPicture
{
    /// Its colour at `uv` (0..1 across it), straight; `lod` reads a blurrier mip (0 the sharpest).
    public float4 at(float2 uv, float lod);

    /// Its size in pixels.
    public property float2 size { get; }
}

/// What has been drawn on the target so far (sampleScreen): pictures.screen(). Its mips are
/// Gaussian-filtered by the host, so a blur reads a blurrier one rather than many pixels.
public struct ScreenPicture : IPicture
{
    public float4 at(float2 uv, float lod) { return sampleScreen(uv, lod); }
    public property float2 size { get { return frame.screen; } }
};

/// The item's own texture (sampleItem): pictures.item().
public struct ItemPicture : IPicture
{
    public float4 at(float2 uv, float lod)
    {
        float4 texel = itemTexture.SampleLevel(uv, lod);
        if (frame.itemPremultiplied > 0.5 && texel.a > 0.0)
            texel.rgb /= texel.a;
        return texel;
    }

    public property float2 size
    {
        get
        {
            float w, h;
            itemTexture.GetDimensions(w, h);
            return float2(w, h);
        }
    }
};

/// Any texture an effect binds: pictures.of(myTexture).
public struct TexturePicture : IPicture
{
    public Sampler2D texture;

    public __init(Sampler2D texture) { this.texture = texture; }

    public float4 at(float2 uv, float lod) { return texture.SampleLevel(uv, lod); }

    public property float2 size
    {
        get
        {
            float w, h;
            texture.GetDimensions(w, h);
            return float2(w, h);
        }
    }
};

/// Where pictures come from.
public namespace pictures
{
    /// What has been drawn on the target so far: read it at i.screenUv.
    public ScreenPicture screen() { return ScreenPicture(); }

    /// The item's own texture: read it at i.uv.
    public ItemPicture item() { return ItemPicture(); }

    /// Any texture the effect binds.
    public TexturePicture of(Sampler2D texture) { return TexturePicture(texture); }
}

// ---- What every picture can do ----------------------------------------------------

public extension<P : IPicture> P
{
    /// Its colour at `uv`, the sharpest mip.
    public float4 sampled(float2 uv) { return at(uv, 0.0); }

    /// Blurred over about `radius` pixels: a blurrier mip, smoothed by 8 reads round a ring so
    /// the mip's blocks don't show. Cheap at any radius.
    /// @cost 9
    public float4 blurred(float2 uv, float radius)
    {
        float2 texel = 1.0 / max(size, float2(1.0));
        float lod = max(log2(max(radius, 1.0)) - 1.0, 0.0);
        float4 sum = at(uv, lod) * 2.0;
        for (int k = 0; k < 8; k++)
        {
            float a = float(k) * TAU / 8.0 + 0.3927;
            sum += at(uv + float2(cos(a), sin(a)) * radius * 0.6 * texel, lod);
        }
        return sum / 10.0;
    }

    /// Smeared along `direction` (in pixels, both ways): motion blur, `taps` reads along it.
    /// @cost 12
    public float4 smeared(float2 uv, float2 direction, int taps = 12)
    {
        float2 step = direction / max(size, float2(1.0));
        float4 sum = float4(0.0);
        int n = clamp(taps, 2, 32);
        for (int k = 0; k < n; k++)
            sum += at(uv + step * (float(k) / float(n - 1) - 0.5), 0.0);
        return sum / float(n);
    }

    /// Smeared out from `centre` (uv) by `strength` (0.1 is a tenth of the way to the centre): a
    /// zoom blur, light rushing outwards.
    /// @cost 12
    public float4 zoomBlurred(float2 uv, float2 centre, float strength, int taps = 12)
    {
        float4 sum = float4(0.0);
        int n = clamp(taps, 2, 32);
        for (int k = 0; k < n; k++)
        {
            float t = float(k) / float(n - 1);
            sum += at(lerp(uv, centre, t * strength), 0.0);
        }
        return sum / float(n);
    }

    /// Turned round `centre` (uv) and smeared over `angle` radians: a spin blur.
    /// @cost 12
    public float4 spinBlurred(float2 uv, float2 centre, float angle, int taps = 12)
    {
        float2 aspect = float2(size.x / max(size.y, 1.0), 1.0);
        float4 sum = float4(0.0);
        int n = clamp(taps, 2, 32);
        for (int k = 0; k < n; k++)
        {
            float a = angle * (float(k) / float(n - 1) - 0.5);
            sum += at(centre + rotate((uv - centre) * aspect, a) / aspect, 0.0);
        }
        return sum / float(n);
    }

    /// Edges made crisper by `amount` (0 none, 1 strong): the picture minus a blur of it (unsharp mask).
    /// @cost 5
    public float4 sharpened(float2 uv, float amount = 1.0)
    {
        float2 t = 1.0 / max(size, float2(1.0));
        float4 c = at(uv, 0.0);
        float4 around = at(uv + float2(t.x, 0), 0.0) + at(uv - float2(t.x, 0), 0.0) + at(uv + float2(0, t.y), 0.0) + at(uv - float2(0, t.y), 0.0);
        return float4(c.rgb + (c.rgb - around.rgb * 0.25) * amount * 2.0, c.a);
    }

    /// How strongly the brightness changes here, 0 flat to about 1 on a hard edge (Sobel): an outline of everything.
    /// @cost 8
    public float edges(float2 uv, float width = 1.0)
    {
        float2 t = width / max(size, float2(1.0));
        float tl = at(uv + float2(-t.x, -t.y), 0.0).luminance(), tc = at(uv + float2(0, -t.y), 0.0).luminance(), tr = at(uv + float2(t.x, -t.y), 0.0).luminance();
        float ml = at(uv + float2(-t.x, 0), 0.0).luminance(), mr = at(uv + float2(t.x, 0), 0.0).luminance();
        float bl = at(uv + float2(-t.x, t.y), 0.0).luminance(), bc = at(uv + float2(0, t.y), 0.0).luminance(), br = at(uv + float2(t.x, t.y), 0.0).luminance();
        float gx = (tr + 2.0 * mr + br) - (tl + 2.0 * ml + bl);
        float gy = (bl + 2.0 * bc + br) - (tl + 2.0 * tc + tr);
        return saturate(length(float2(gx, gy)) * 0.5);
    }

    /// Lit from the top left as if pressed into metal: grey, with edges light and dark by `strength`.
    /// @cost 2
    public float4 embossed(float2 uv, float strength = 1.0)
    {
        float2 t = 1.0 / max(size, float2(1.0));
        float d = at(uv - t, 0.0).luminance() - at(uv + t, 0.0).luminance();
        return float4(float3(0.5 + d * strength * 2.0), at(uv, 0.0).a);
    }

    /// Cut into square blocks `block` pixels across, each the colour at its middle.
    public float4 pixelated(float2 uv, float block)
    {
        float2 cells = size / max(block, 1.0);
        return at((floor(uv * cells) + 0.5) / cells, 0.0);
    }

    /// Its red, green and blue pulled apart `amount` pixels, outwards from the middle (a cheap
    /// lens's colour fringes, chromatic aberration). Stronger towards the edges.
    /// @cost 3
    public float4 aberrated(float2 uv, float amount)
    {
        float2 outward = (uv - 0.5) * 2.0;
        float2 shift = outward * amount / max(size, float2(1.0));
        float4 g = at(uv, 0.0);
        return float4(at(uv + shift, 0.0).r, g.g, at(uv - shift, 0.0).b, g.a);
    }

    /// Its red, green and blue pulled apart `offset` pixels along a direction (red one way, blue the other).
    /// @cost 3
    public float4 split(float2 uv, float2 offset)
    {
        float2 shift = offset / max(size, float2(1.0));
        float4 g = at(uv, 0.0);
        return float4(at(uv + shift, 0.0).r, g.g, at(uv - shift, 0.0).b, g.a);
    }

    /// Smoothed into flat patches of colour with crisp edges between them, as if painted
    /// (Kuwahara's filter, over `radius` pixels, read 4×4 per quarter).
    /// @cost 64
    public float4 painterly(float2 uv, float radius)
    {
        float2 t = radius / 3.0 / max(size, float2(1.0));
        float3 bestMean = float3(0.0);
        float bestSpread = 1e9;
        for (int q = 0; q < 4; q++)
        {
            float2 dir = float2(q % 2 == 0 ? -1.0 : 1.0, q < 2 ? -1.0 : 1.0);
            float3 sum = float3(0.0), sumSq = float3(0.0);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    float3 c = at(uv + dir * float2(x, y) * t, 0.0).rgb;
                    sum += c;
                    sumSq += c * c;
                }
            float3 mean = sum / 16.0;
            float3 spread = abs(sumSq / 16.0 - mean * mean);
            float total = spread.r + spread.g + spread.b;
            if (total < bestSpread) { bestSpread = total; bestMean = mean; }
        }
        return float4(bestMean, at(uv, 0.0).a);
    }

    /// Only what's brighter than `threshold` (0..1 brightness), the rest black, blurred over
    /// `radius` pixels: add it back on (blend.add) for bloom.
    /// @cost 9
    public float4 bloomed(float2 uv, float threshold, float radius)
    {
        float4 c = blurred(uv, radius);
        float over = max(c.luminance() - threshold, 0.0) / max(1.0 - threshold, 1e-3);
        return float4(c.rgb * over, c.a);
    }
}

// ---- On a point or a uv ---------------------------------------------------------

/// Filters on a uv or a pixel, without a picture: they give a number to multiply or add, or a
/// moved uv to read any picture at.
public namespace filter
{
    /// 1 in the middle darkening towards the corners: multiply a colour by it. `strength` 0 none,
    /// 1 black corners; `softness` how gradual (0 a hard ring, 1 from the very middle).
    public float vignette(float2 uv, float strength = 0.5, float softness = 0.5)
    {
        float d = length(uv - 0.5) * 1.41421356;
        return 1.0 - strength * smoothstep(1.0 - softness, 1.0 + softness * 0.2, d);
    }

    /// Film grain: a fresh random -amount..amount per pixel every frame; add it to a colour's rgb.
    public float grain(float2 pixel, float amount = 0.05)
    {
        return (random(float3(floor(pixel), floor(time * 60.0))) * 2.0 - 1.0) * amount;
    }

    /// Dark lines across every `spacing` pixels, as an old screen's: 1 on a line's middle, 1 -
    /// `depth` between; multiply a colour by it.
    public float scanlines(float2 pixel, float spacing = 3.0, float depth = 0.3)
    {
        return 1.0 - depth * (0.5 - 0.5 * cos(TAU * pixel.y / max(spacing, 1.0)));
    }

    /// A uv bent outwards like an old curved screen (barrel distortion): read a picture there.
    /// `amount` about 0.05..0.3; outside 0..1 the screen has ended (filter.inside).
    public float2 curved(float2 uv, float amount)
    {
        float2 c = uv * 2.0 - 1.0;
        c *= 1.0 + amount * dot(c, c) * float2(c.y * c.y, c.x * c.x) * 0.5 + amount * 0.25 * dot(c, c);
        return c * 0.5 + 0.5;
    }

    /// A uv through a lens: `amount` above 0 bulges it outwards (fisheye), below 0 pinches it in.
    public float2 lens(float2 uv, float amount)
    {
        float2 c = uv - 0.5;
        float r2 = dot(c, c);
        return 0.5 + c * (1.0 + amount * r2 + amount * r2 * r2);
    }

    /// 1 when `uv` is on the picture (0..1 both ways), 0 off it, its edge smoothed over `soft` of the uv.
    public float inside(float2 uv, float soft = 0.002)
    {
        float2 d = min(uv, 1.0 - uv);
        return smoothstep(-soft, soft, min(d.x, d.y));
    }

    /// How far to push a pixel for a ring of distortion `radius` pixels out from `centre`,
    /// `width` thick, pushing outwards by up to `strength` pixels: a shockwave. Grow the radius
    /// with a clip; add the push to the pixel (or push / size to a uv) and read there.
    public float2 shockwave(float2 pixel, float2 centre, float radius, float width, float strength)
    {
        float2 to = pixel - centre;
        float d = length(to);
        float x = (d - radius) / max(width, 1e-3);
        float ring = exp(-x * x * 4.0) * x;
        return d > 1e-3 ? -to / d * ring * strength : float2(0.0);
    }

    /// How far to push a pixel seen through a slab of glass with a rounded rim (liquid glass):
    /// flat on top, the rim `bevel` pixels wide dropping to the edge like a quarter circle, so the
    /// view bends most right at the edge (by `strength` pixels) and not at all on the flat. `d` and
    /// `outward` are the shape's distance and outward direction there (distance, outward). Add the
    /// push to the pixel and read there: a positive strength pulls what lies past the edge in over
    /// the rim, as a thick lens does; a negative one pushes the inside out.
    public float2 glassRim(float d, float2 outward, float bevel, float strength)
    {
        float t = 1.0 - saturate(-d / max(bevel, 1e-5)); // 1 on the edge, 0 where the flat starts
        float drop = 1.0 - sqrt(saturate(1.0 - t * t));  // how far the rim has fallen from the top
        return outward * strength * drop;
    }

    /// How far to push a pixel for wavering heat over a fire or a road: noise-free, cheap sines,
    /// `strength` pixels at most, rising as `time` goes.
    public float2 heatHaze(float2 pixel, float strength = 2.0, float scale = 40.0)
    {
        float2 p = pixel / max(scale, 1e-3);
        return float2(sin(p.y * 2.1 + time * 5.3) + sin(p.y * 3.7 - time * 3.1) * 0.5,
                      cos(p.x * 1.7 + time * 4.3) * 0.3) * strength * 0.6;
    }

    /// A uv as if seen through rippling water round `centre` (uv): rings `spacing` of the uv apart,
    /// moving out at `speed`, pushing by up to `strength` of the uv.
    public float2 ripple(float2 uv, float2 centre, float spacing = 0.05, float speed = 0.1, float strength = 0.005)
    {
        float2 to = uv - centre;
        float d = length(to);
        float wave = sin((d - time * speed) * TAU / max(spacing, 1e-4));
        return uv + (d > 1e-5 ? to / d : float2(0.0)) * wave * strength;
    }

    /// The 8×8 ordered-dither threshold (Bayer's matrix) for a pixel, in 0..1: compare a value
    /// against it for a regular cross-hatched dither, as old consoles did.
    public float bayer(float2 pixel)
    {
        // Bayer's index by interleaving the bits of x ^ y and y, reversed.
        uint2 p = uint2(floor(pixel)) & 7u;
        uint x = p.x ^ p.y;
        uint y = p.y;
        uint v = ((x & 1u) << 5) | ((y & 1u) << 4) | ((x & 2u) << 2) | ((y & 2u) << 1) | ((x & 4u) >> 1) | ((y & 4u) >> 2);
        return (float(v) + 0.5) / 64.0;
    }

    /// Jimenez's interleaved gradient noise for a pixel, 0..1: a fine dither that hides banding
    /// and averages away over a few frames (pass a frame count as `frame` to move it).
    public float ign(float2 pixel, float frame = 0.0)
    {
        pixel += 5.588238 * frame;
        return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
    }

    /// A uv torn by digital glitches: horizontal bands jump sideways, `amount` 0..1 how many and
    /// how far, new ones `rate` times a second. Read the picture there (and split its colours
    /// for the full look): behind.split(filter.glitch(uv, 0.5), float2(6, 0)).
    public float2 glitch(float2 uv, float amount = 0.5, float rate = 15.0)
    {
        float tick = floor(time * rate);
        float band = floor(uv.y * (8.0 + 24.0 * random(float2(tick, 1.0))));
        float r = random(float2(band, tick));
        float shift = r < amount * 0.4 ? (random(float2(band, tick + 7.0)) - 0.5) * 0.2 * amount : 0.0;
        // Now and then a blocky stutter: the band snaps to a coarse grid.
        float2 moved = uv + float2(shift, 0.0);
        if (r < amount * 0.1)
            moved.x = floor(moved.x * 24.0) / 24.0;
        return moved;
    }

    // ---- Print looks ----------------------------------------------------------

    /// Halftone: how much ink a printed dot screen lays at `pixel` for an `ink` of 0 (paper) to 1
    /// (solid), dots on a grid `size` pixels apart turned by `angle` (45° by default, as presses
    /// print black). Paint ink colour by it: c.paint(filter.halftone(i.pixel, 1 - c.luminance()), colours.black).
    public float halftone(float2 pixel, float ink, float size = 8.0, float angle = 0.785398)
    {
        float2 p = rotate(pixel, -angle);
        float2 local = p - size * round(p / size);
        // The dot whose area is `ink` of the cell: r = size·√(ink/π), grown past the cell's corners at the top end.
        float r = size * sqrt(saturate(ink) / PI) * (1.0 + 0.2 * saturate(ink));
        float d = length(local) - r;
        float w = max(fwidth(d), 1e-4) * 0.75;
        return 1.0 - smoothstep(-w, w, d);
    }

    /// Pen hatching: how much ink lines lay at `pixel` for an `ink` of 0 (blank) to 1 (dark): one
    /// set of strokes `spacing` apart for light shading, a second across them past a third, a third
    /// past two thirds. Wobbles a little, like a hand.
    public float hatched(float2 pixel, float ink, float spacing = 7.0, float width = 1.2)
    {
        float lines = 0.0;
        float3 angles = float3(0.785398, -0.785398, 0.0);
        for (int k = 0; k < 3; k++)
        {
            float start = float(k) / 3.0;
            if (ink <= start)
                break;
            float2 p = rotate(pixel, angles[k]);
            float wobble = sin(p.x * 0.05 + float(k) * 2.0) * 1.2;
            float off = abs(p.y + wobble - spacing * round((p.y + wobble) / spacing));
            float strength = saturate((ink - start) * 3.0);
            lines = max(lines, (1.0 - smoothstep(width * 0.5 * strength, width * 0.5 * strength + 0.8, off)) * saturate(strength * 2.0));
        }
        return lines;
    }
}

public extension float4
{
    /// Cuts each channel to `levels` steps, dithered by Bayer's pattern at `pixel` so the steps
    /// cross-hatch into each other instead of banding: retro palettes and 1-bit looks.
    [mutating] public void dither(float2 pixel, float levels = 2.0) { this = dithered(pixel, levels); }

    /// A copy cut to `levels` steps per channel, dithered by Bayer's pattern at `pixel`.
    public float4 dithered(float2 pixel, float levels = 2.0)
    {
        float n = max(levels - 1.0, 1.0);
        float t = filter.bayer(pixel) - 0.5;
        return float4(floor(saturate(this.rgb) * n + 0.5 + t) / n, this.a);
    }

    /// The faintest dither to hide banding in a smooth gradient (a fraction of one 8-bit step),
    /// by interleaved gradient noise at `pixel`.
    [mutating] public void deband(float2 pixel) { this.rgb += (filter.ign(pixel) - 0.5) / 255.0; }

    /// A copy with the faintest dither added to hide banding.
    public float4 debanded(float2 pixel) { float4 r = this; r.deband(pixel); return r; }
}
