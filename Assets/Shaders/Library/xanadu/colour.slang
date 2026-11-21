// xanadu.colour: the colour type, its helpers, and the X11/web colours by
// name, all under `colours.` so none of them crowd the global names (values
// from OpenTK's Color4).
//
//     colour c = colours.gold;     a colour is a float4 (straight alpha), .rgb and all
//     colour(1.0, 0.3, 1.0)        rgb alone (three numbers or a float3) is opaque
//     colours.hsv(h, s, v)         hue, saturation, value (each 0..1) to a colour
//     colours.hue(h)               the hue at full saturation and value
//     top.over(bottom)             straight-alpha "over"
//     colours.black.withAlpha(0.5) the same colour, another alpha
//     c.brighten(1.2); c.greyed()  rgb changes, the alpha kept: invert, grey,
//                                  brighten, saturateBy, contrast, tint, shiftHue
//                                  (and inverted, greyed, … for a changed copy)
module colour;
import xanadu.math;

/// A colour: just a float4 (r, g, b, a, straight alpha) under a name that says what it holds.
public typealias colour = float4;

public extension float4
{
    /// An opaque colour from red, green and blue: colour(1.0, 0.3, 1.0) has alpha 1.
    public __init(float r, float g, float b) { this = float4(r, g, b, 1.0); }

    /// An opaque colour from an rgb: colour(c.rgb) has alpha 1.
    public __init(float3 rgb) { this = float4(rgb, 1.0); }

    /// The same colour with alpha `a`: colours.black.withAlpha(0.5).
    public colour withAlpha(float a) { return colour(this.rgb, a); }

    /// This colour over another with straight (not premultiplied) alpha.
    public colour over(colour bottom)
    {
        colour top = this;
        float a = top.a + bottom.a * (1.0 - top.a);
        if (a == 0.0)
            return colour(0.0);
        return colour((top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / a, a);
    }

    // ---- Changing the rgb, keeping the alpha -------------------------------------
    // Verbs change the colour (c.invert()); past participles give a changed copy (c.inverted()).

    /// The same alpha with another rgb: c.withRgb(c.rgb * 0.5).
    public colour withRgb(float3 rgb) { return colour(rgb, this.a); }

    /// How bright it looks, 0..1: the rgb weighted as the eye weighs it (Rec. 709).
    public float luminance() { return dot(this.rgb, float3(0.2126, 0.7152, 0.0722)); }

    /// Turns it into its opposite: 1 - rgb.
    [mutating] public void invert() { this = colour(1.0 - this.rgb, this.a); }

    /// A copy turned into its opposite: 1 - rgb.
    public colour inverted() { return colour(1.0 - this.rgb, this.a); }

    /// Takes its colour away, leaving how bright it looks.
    [mutating] public void grey() { this = colour(float3(luminance()), this.a); }

    /// A copy with its colour taken away, leaving how bright it looks.
    public colour greyed() { return colour(float3(luminance()), this.a); }

    /// Multiplies the rgb by `factor`: above 1 brighter, below darker, 0 black.
    [mutating] public void brighten(float factor) { this = colour(this.rgb * factor, this.a); }

    /// A copy with the rgb multiplied by `factor`: above 1 brighter, below darker.
    public colour brightened(float factor) { return colour(this.rgb * factor, this.a); }

    /// Scales how colourful it is: 0 grey, 1 as it is, above 1 more vivid.
    [mutating] public void saturateBy(float factor) { this = saturatedBy(factor); }

    /// A copy scaled in how colourful it is: 0 grey, 1 as it is, above 1 more vivid.
    public colour saturatedBy(float factor) { return colour(lerp(float3(luminance()), this.rgb, factor), this.a); }

    /// Scales how far the rgb is from mid grey: 0 flat grey, 1 as it is, above 1 punchier.
    [mutating] public void contrast(float factor) { this = contrasted(factor); }

    /// A copy with the rgb's distance from mid grey scaled: 0 flat grey, 1 as it is, above 1 punchier.
    public colour contrasted(float factor) { return colour((this.rgb - 0.5) * factor + 0.5, this.a); }

    /// Moves the rgb towards `to`'s by `amount` (0 none, 1 all the way); its own alpha kept.
    [mutating] public void tint(colour to, float amount) { this = tinted(to, amount); }

    /// A copy with the rgb moved towards `to`'s by `amount` (0 none, 1 all the way).
    public colour tinted(colour to, float amount) { return colour(lerp(this.rgb, to.rgb, amount), this.a); }

    /// Encodes it for the screen: rgb to the power 1 / `gamma` (lighter mid-tones), as shaders that
    /// work in linear light finish with pow(rgb, 1.0 / 2.2).
    [mutating] public void gammaCorrect(float gamma = 2.2) { this = gammaCorrected(gamma); }

    /// A copy encoded for the screen: rgb to the power 1 / `gamma`.
    public colour gammaCorrected(float gamma = 2.2) { return colour(pow(max(this.rgb, 0.0), 1.0 / gamma), this.a); }

    // ---- Colour spaces ---------------------------------------------------------------
    // Colours are stored as the screen shows them (sRGB-encoded). Light adds up in linear
    // values, and the eye judges "halfway" in Oklab, so mixing, glows and HDR go through those.

    /// Turns it from sRGB-encoded into linear light (the exact sRGB curve), alpha kept: add light up in these.
    [mutating] public void linearise() { this = linearised(); }

    /// A copy in linear light (the exact sRGB curve, not pow 2.2), alpha kept.
    public colour linearised() { return colour(colours.toLinear(this.rgb), this.a); }

    /// Turns it from linear light back into sRGB for the screen, alpha kept.
    [mutating] public void encode() { this = encoded(); }

    /// A copy from linear light encoded for the screen (the exact sRGB curve), alpha kept.
    public colour encoded() { return colour(colours.toSrgb(this.rgb), this.a); }

    /// Its hue, saturation and value, each 0..1 (colours.hsv turns them back).
    public float3 hsv()
    {
        float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
        float4 p = this.g < this.b ? float4(this.b, this.g, k.w, k.z) : float4(this.g, this.b, k.x, k.y);
        float4 q = this.r < p.x ? float4(p.x, p.y, p.w, this.r) : float4(this.r, p.y, p.z, p.x);
        float d = q.x - min(q.w, q.y);
        return float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-10)), d / (q.x + 1e-10), q.x);
    }

    /// Its hue, saturation and lightness, each 0..1 (colours.hsl turns them back).
    public float3 hsl()
    {
        float3 v = hsv();
        float l = v.z * (1.0 - v.y * 0.5);
        float s = (l <= 0.0 || l >= 1.0) ? 0.0 : (v.z - l) / min(l, 1.0 - l);
        return float3(v.x, s, l);
    }

    /// It in Oklab (Björn Ottosson's): lightness 0..1, then a (green to red) and b (blue to
    /// yellow), each about -0.4..0.4. Even steps in it look even.
    public float3 oklab() { return colours.linearToOklab(colours.toLinear(this.rgb)); }

    /// It in Oklch: lightness 0..1, chroma (how colourful, 0 grey, about 0.4 at most) and hue in turns 0..1.
    public float3 oklch()
    {
        float3 lab = oklab();
        return float3(lab.x, length(lab.yz), frac(atan2(lab.z, lab.y) / TAU + 1.0));
    }

    /// Moves it towards `to` by `amount` as the eye sees it (through Oklab): no muddy grey middle
    /// between complementary colours, as lerp gives. Alphas mix plainly.
    [mutating] public void mix(colour to, float amount) { this = mixed(to, amount); }

    /// A copy moved towards `to` by `amount` through Oklab: the perceptual blend.
    public colour mixed(colour to, float amount)
    {
        float3 lab = lerp(oklab(), to.oklab(), amount);
        return colour(colours.oklab(lab.x, lab.y, lab.z).rgb, lerp(this.a, to.a, amount));
    }

    /// Cuts each channel to `levels` steps (2 is on/off): a poster's flat bands.
    [mutating] public void posterize(float levels) { this = posterized(levels); }

    /// A copy with each channel cut to `levels` steps.
    public colour posterized(float levels)
    {
        float n = max(levels - 1.0, 1.0);
        return colour(floor(saturate(this.rgb) * n + 0.5) / n, this.a);
    }

    /// Changes its exposure by `stops` (each doubles the light, in linear light): +1 twice as bright.
    [mutating] public void expose(float stops) { this = exposed(stops); }

    /// A copy `stops` brighter (or darker if negative), doubling the light per stop in linear light.
    public colour exposed(float stops) { return colour(colours.toSrgb(colours.toLinear(this.rgb) * exp2(stops)), this.a); }

    /// Warms it (positive, towards orange) or cools it (negative, towards blue) by `amount` (about -1..1), as white balance does.
    [mutating] public void warm(float amount) { this = warmed(amount); }

    /// A copy warmed (positive) or cooled (negative) by `amount`.
    public colour warmed(float amount)
    {
        float3 lab = oklab();
        lab.yz += float2(0.02, 0.06) * amount;
        return colour(colours.oklab(lab.x, lab.y, lab.z).rgb, this.a);
    }

    // ---- Grading ---------------------------------------------------------------------

    /// Stretches `black`..`white` (0..1 brightness) to the full range, mid-tones bent by `gamma`
    /// (above 1 lighter): Photoshop's Levels.
    [mutating] public void level(float black, float white, float gamma = 1.0) { this = levelled(black, white, gamma); }

    /// A copy with `black`..`white` stretched to the full range, mid-tones bent by `gamma`.
    public colour levelled(float black, float white, float gamma = 1.0)
    {
        float3 x = saturate((this.rgb - black) / max(white - black, 1e-5));
        return colour(pow(x, float3(1.0 / max(gamma, 1e-3))), this.a);
    }

    /// Colour grading by lift (raises the shadows), gamma (bends the mid-tones) and gain (scales
    /// the highlights), each per channel, 0 / 1 / 1 leaving it alone: a colourist's three wheels.
    [mutating] public void grade(float3 lift, float3 gamma, float3 gain) { this = graded(lift, gamma, gain); }

    /// A copy graded by lift, gamma and gain (each per channel; 0, 1, 1 leave it alone).
    public colour graded(float3 lift, float3 gamma, float3 gain)
    {
        float3 x = this.rgb * gain + lift * (1.0 - this.rgb);
        return colour(pow(max(x, 0.0), 1.0 / max(gamma, float3(1e-3))), this.a);
    }

    /// Maps how bright it is onto two colours: black to `shadows`, white to `highlights` (a duotone
    /// print, a tinted photo).
    [mutating] public void duotone(colour shadows, colour highlights) { this = duotoned(shadows, highlights); }

    /// A copy mapped by brightness from `shadows` to `highlights`.
    public colour duotoned(colour shadows, colour highlights)
    {
        return colour(lerp(shadows.rgb, highlights.rgb, luminance()), this.a);
    }

    /// Turns it the warm brown of an old photo by `amount` (0 none, 1 all the way).
    [mutating] public void sepiaTone(float amount = 1.0) { this = sepiaToned(amount); }

    /// A copy toned the brown of an old photo by `amount`.
    public colour sepiaToned(float amount = 1.0)
    {
        float3 x = this.rgb;
        float3 sepia = float3(dot(x, float3(0.393, 0.769, 0.189)), dot(x, float3(0.349, 0.686, 0.168)), dot(x, float3(0.272, 0.534, 0.131)));
        return colour(lerp(x, saturate(sepia), amount), this.a);
    }

    /// Black or white by whether it is brighter than `level`, the step smoothed over `soft`.
    [mutating] public void threshold(float level = 0.5, float soft = 0.0) { this = thresholded(level, soft); }

    /// A copy black or white by whether it is brighter than `level`.
    public colour thresholded(float level = 0.5, float soft = 0.0)
    {
        float l = luminance();
        float v = soft > 0.0 ? smoothstep(level - soft, level + soft, l) : step(level, l);
        return colour(float3(v), this.a);
    }

    /// Makes the dull colours more colourful by `amount` and leaves vivid ones nearly alone, so skin
    /// and skies don't blow out the way saturateBy does (vibrance).
    [mutating] public void enliven(float amount) { this = enlivened(amount); }

    /// A copy whose dull colours are made more colourful by `amount` (vibrance).
    public colour enlivened(float amount)
    {
        float hi = max(this.r, max(this.g, this.b));
        float lo = min(this.r, min(this.g, this.b));
        float already = hi - lo;
        float l = luminance();
        return colour(lerp(float3(l), this.rgb, 1.0 + amount * (1.0 - already)), this.a);
    }

    /// Turns the hue round by `turns` (1 is all the way round, back where it started), keeping how bright it is.
    [mutating] public void shiftHue(float turns) { this = hueShifted(turns); }

    /// A copy with the hue turned round by `turns` (1 is all the way round).
    public colour hueShifted(float turns)
    {
        // Rotating the rgb round the grey axis (1, 1, 1).
        float angle = turns * 6.28318530718;
        float3 k = float3(0.57735026919);
        float c = cos(angle), s = sin(angle);
        float3 rgb = this.rgb * c + cross(k, this.rgb) * s + k * dot(k, this.rgb) * (1.0 - c);
        return colour(rgb, this.a);
    }
}

public namespace colours
{
    /// Hue, saturation and value (each 0..1) to an opaque colour.
    public colour hsv(float hue, float saturation, float value)
    {
        float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
        float3 p = abs(frac(float3(hue) + k.xyz) * 6.0 - k.www);
        return colour(value * lerp(k.xxx, saturate(p - k.xxx), saturation), 1.0);
    }

    /// Inigo Quilez's cosine palette: a + b·cos(TAU·(c·t + d)), each per channel. `a` is the
    /// middle, `b` how far it swings, `c` how many times round per unit of t, `d` each channel's
    /// head start (in turns). colours.palette(t, 0.5, 0.5, 1, float3(0, 0.33, 0.67)) is a rainbow.
    public colour palette(float t, float3 a, float3 b, float3 c, float3 d)
    {
        return colour(a + b * cos(TAU * (c * t + d)), 1.0);
    }

    /// The commonest cosine palette: a smooth rainbow round t (a whole turn every 1), the
    /// channels a third of a turn apart, `middle` ± `swing`. cos(float3(0, 2, 4) * PI / 3 + x) * s + m
    /// is colours.rainbow(x / TAU, m, s).
    public colour rainbow(float t, float middle = 0.5, float swing = 0.5)
    {
        return palette(t, float3(middle), float3(swing), float3(1.0), float3(0.0, 1.0 / 3.0, 2.0 / 3.0));
    }

    /// The hue at full saturation and value, cycling red, green, blue as it goes 0..1.
    public colour hue(float hue)
    {
        return colour(saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0), 1.0);
    }

    /// sRGB-encoded rgb to linear light: the exact curve (a straight bit near black, then pow 2.4).
    public float3 toLinear(float3 rgb)
    {
        float3 c = max(rgb, 0.0);
        return lerp(c / 12.92, pow((c + 0.055) / 1.055, float3(2.4)), step(0.04045, c));
    }

    /// Linear light to sRGB-encoded rgb, the exact curve.
    public float3 toSrgb(float3 linear)
    {
        float3 c = max(linear, 0.0);
        return lerp(c * 12.92, 1.055 * pow(c, float3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
    }

    /// Linear rgb to Oklab (lightness, a, b).
    public float3 linearToOklab(float3 c)
    {
        float l = 0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b;
        float m = 0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b;
        float s = 0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b;
        float3 q = sign(float3(l, m, s)) * pow(abs(float3(l, m, s)), float3(1.0 / 3.0));
        return float3(0.2104542553 * q.x + 0.7936177850 * q.y - 0.0040720468 * q.z,
                      1.9779984951 * q.x - 2.4285922050 * q.y + 0.4505937099 * q.z,
                      0.0259040371 * q.x + 0.7827717662 * q.y - 0.8086757660 * q.z);
    }

    /// Oklab (lightness, a, b) to linear rgb, not held inside 0..1.
    public float3 oklabToLinear(float3 lab)
    {
        float l = lab.x + 0.3963377774 * lab.y + 0.2158037573 * lab.z;
        float m = lab.x - 0.1055613458 * lab.y - 0.0638541728 * lab.z;
        float s = lab.x - 0.0894841775 * lab.y - 1.2914855480 * lab.z;
        l = l * l * l; m = m * m * m; s = s * s * s;
        return float3(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                     -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
                     -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
    }

    /// A colour from Oklab: lightness 0..1, a (green to red) and b (blue to yellow), each about -0.4..0.4.
    public colour oklab(float lightness, float a, float b) { return colour(saturate(toSrgb(oklabToLinear(float3(lightness, a, b)))), 1.0); }

    /// A colour from Oklch: lightness 0..1, chroma (0 grey, about 0.37 at most) and hue in turns.
    /// Even lightness across hues, which hsv doesn't give: colours.oklch(0.7, 0.15, t) is a rainbow
    /// whose every colour looks as bright.
    public colour oklch(float lightness, float chroma, float hue)
    {
        float a = hue * TAU;
        return oklab(lightness, chroma * cos(a), chroma * sin(a));
    }

    /// Hue, saturation and lightness (each 0..1) to an opaque colour: lightness 0.5 is the full colour.
    public colour hsl(float hue, float saturation, float lightness)
    {
        float3 rgb = saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
        float c = (1.0 - abs(2.0 * lightness - 1.0)) * saturation;
        return colour(lightness + c * (rgb - 0.5), 1.0);
    }

    /// The colour of a glowing hot body at `kelvin` (about 1000 to 40000): 1900 a candle, 2700 a
    /// bulb, 6500 daylight, 10000 blue sky. Its brightest channel is 1 (Krystek's Planckian fit).
    public colour blackbody(float kelvin)
    {
        float t = clamp(kelvin, 1000.0, 40000.0);
        float u = (0.860117757 + 1.54118254e-4 * t + 1.28641212e-7 * t * t) / (1.0 + 8.42420235e-4 * t + 7.08145163e-7 * t * t);
        float v = (0.317398726 + 4.22806245e-5 * t + 4.20481691e-8 * t * t) / (1.0 - 2.89741816e-5 * t + 1.61456053e-7 * t * t);
        float x = 3.0 * u / (2.0 * u - 8.0 * v + 4.0);
        float y = 2.0 * v / (2.0 * u - 8.0 * v + 4.0);
        float3 xyz = float3(x / y, 1.0, (1.0 - x - y) / y);
        float3 rgb = float3(3.2404542 * xyz.x - 1.5371385 * xyz.y - 0.4985314 * xyz.z,
                           -0.9692660 * xyz.x + 1.8760108 * xyz.y + 0.0415560 * xyz.z,
                            0.0556434 * xyz.x - 0.2040259 * xyz.y + 1.0572252 * xyz.z);
        rgb = max(rgb, 0.0);
        return colour(toSrgb(rgb / max(max(rgb.r, rgb.g), max(rgb.b, 1e-6))), 1.0);
    }

    /// A 6th-degree polynomial in t per channel (Matt Zucker's fits of matplotlib's maps).
    /// @hidden
    public colour fitted(float t, float3 c0, float3 c1, float3 c2, float3 c3, float3 c4, float3 c5, float3 c6)
    {
        t = saturate(t);
        return colour(saturate(c0 + t * (c1 + t * (c2 + t * (c3 + t * (c4 + t * (c5 + t * c6)))))), 1.0);
    }

    /// matplotlib's viridis along t (0..1): dark purple, teal, yellow. Even in lightness and kind to
    /// colour-blind eyes: the safe map for showing a number.
    public colour viridis(float t)
    {
        return fitted(t, float3(0.2777273272234177, 0.005407344544966578, 0.3340998053353061),
            float3(0.1050930431085774, 1.404613529898575, 1.384590162594685),
            float3(-0.3308618287255563, 0.214847559468213, 0.09509516302823659),
            float3(-4.634230498983486, -5.799100973351585, -19.33244095627987),
            float3(6.228269936347081, 14.17993336680509, 56.69055260068105),
            float3(4.776384997670288, -13.74514537774601, -65.35303263337234),
            float3(-5.435455855934631, 4.645852612178535, 26.3124352495832));
    }

    /// matplotlib's inferno along t (0..1): black, purple, orange, pale yellow. Fire and heat.
    public colour inferno(float t)
    {
        return fitted(t, float3(0.0002189403691192265, 0.001651004631001012, -0.01948089843709184),
            float3(0.1065134194856116, 0.5639564367884091, 3.932712388889277),
            float3(11.60249308247187, -3.972853965665698, -15.9423941062914),
            float3(-41.70399613139459, 17.43639888205313, 44.35414519872813),
            float3(77.162935699427, -33.40235894210092, -81.80730925738993),
            float3(-71.31942824499214, 32.62606426397723, 73.20951985803202),
            float3(25.13112622477341, -12.24266895238567, -23.07032500287172));
    }

    /// matplotlib's magma along t (0..1): black, purple, pink, pale cream.
    public colour magma(float t)
    {
        return fitted(t, float3(-0.002136485053939582, -0.000749655052795221, -0.005386127855323933),
            float3(0.2516605407371642, 0.6775232436837668, 2.494026599312351),
            float3(8.353717279216625, -3.577719514958484, 0.3144679030132573),
            float3(-27.66873308576866, 14.26473078096533, -13.64921318813922),
            float3(52.17613981234068, -27.94360607168351, 12.94416944238394),
            float3(-50.76852536473588, 29.04658282127291, 4.23415299384598),
            float3(18.65570506591883, -11.48977351997711, -5.601961508734096));
    }

    /// matplotlib's plasma along t (0..1): deep blue, magenta, orange, yellow.
    public colour plasma(float t)
    {
        return fitted(t, float3(0.05873234392399702, 0.02333670892565664, 0.5433401826748754),
            float3(2.176514634195958, 0.2383834171260182, 0.7539604599784036),
            float3(-2.689460476458034, -7.455851135738909, 3.110799939717086),
            float3(6.130348345893603, 42.3461881477227, -28.51885465332158),
            float3(-11.10743619062271, -82.66631109428045, 60.13984767418263),
            float3(10.02306557647065, 71.41361770095349, -54.07218655560067),
            float3(-3.658713842777788, -22.93153465461149, 18.19190778539828));
    }

    /// Google's Turbo along t (0..1): a smooth jet-like rainbow, dark blue to dark red (Mikhailov's fit).
    public colour turbo(float t)
    {
        t = saturate(t);
        float4 v4 = float4(1.0, t, t * t, t * t * t);
        float2 v2 = v4.zw * v4.z;
        return colour(saturate(float3(
            dot(v4, float4(0.13572138, 4.61539260, -42.66032258, 132.13108234)) + dot(v2, float2(-152.94239396, 59.28637943)),
            dot(v4, float4(0.09140261, 2.19418839, 4.84296658, -14.18503333)) + dot(v2, float2(4.27729857, 2.82956604)),
            dot(v4, float4(0.10667330, 12.64194608, -60.58204836, 110.36276771)) + dot(v2, float2(-89.90310912, 27.34824973)))), 1.0);
    }

    public static const colour transparent = float4(0.0, 0.0, 0.0, 0.0);
    public static const colour black = float4(0.0, 0.0, 0.0, 1.0);
    public static const colour white = float4(1.0, 1.0, 1.0, 1.0);
    public static const colour red = float4(1.0, 0.0, 0.0, 1.0);
    public static const colour green = float4(0.0, 1.0, 0.0, 1.0);
    public static const colour blue = float4(0.0, 0.0, 1.0, 1.0);
    public static const colour yellow = float4(1.0, 1.0, 0.0, 1.0);
    public static const colour cyan = float4(0.0, 1.0, 1.0, 1.0); // Also known as Aqua
    public static const colour magenta = float4(1.0, 0.0, 1.0, 1.0); // Also known as Fuchsia
    public static const colour gray = float4(0.5, 0.5, 0.5, 1.0);
    public static const colour aliceBlue = float4(0.94, 0.97, 1.0, 1.0);
    public static const colour antiqueWhite = float4(0.98, 0.92, 0.84, 1.0);
    public static const colour aqua = float4(0.0, 1.0, 1.0, 1.0);
    public static const colour aquamarine = float4(0.5, 1.0, 0.83, 1.0);
    public static const colour azure = float4(0.94, 1.0, 1.0, 1.0);
    public static const colour beige = float4(0.96, 0.96, 0.86, 1.0);
    public static const colour bisque = float4(1.0, 0.89, 0.77, 1.0);
    public static const colour blanchedAlmond = float4(1.0, 0.92, 0.8, 1.0);
    public static const colour blueViolet = float4(0.54, 0.17, 0.89, 1.0);
    public static const colour brown = float4(0.65, 0.16, 0.16, 1.0);
    public static const colour burlyWood = float4(0.87, 0.72, 0.53, 1.0);
    public static const colour cadetBlue = float4(0.37, 0.62, 0.63, 1.0);
    public static const colour chartreuse = float4(0.5, 1.0, 0.0, 1.0);
    public static const colour chocolate = float4(0.82, 0.41, 0.12, 1.0);
    public static const colour coral = float4(1.0, 0.5, 0.31, 1.0);
    public static const colour cornflowerBlue = float4(0.39, 0.58, 0.93, 1.0);
    public static const colour cornsilk = float4(1.0, 0.97, 0.86, 1.0);
    public static const colour crimson = float4(0.86, 0.08, 0.24, 1.0);
    public static const colour darkBlue = float4(0.0, 0.0, 0.55, 1.0);
    public static const colour darkCyan = float4(0.0, 0.55, 0.55, 1.0);
    public static const colour darkGoldenrod = float4(0.72, 0.53, 0.04, 1.0);
    public static const colour darkGray = float4(0.66, 0.66, 0.66, 1.0);
    public static const colour darkGreen = float4(0.0, 0.39, 0.0, 1.0);
    public static const colour darkKhaki = float4(0.74, 0.72, 0.42, 1.0);
    public static const colour darkMagenta = float4(0.55, 0.0, 0.55, 1.0);
    public static const colour darkOliveGreen = float4(0.33, 0.42, 0.18, 1.0);
    public static const colour darkOrange = float4(1.0, 0.55, 0.0, 1.0);
    public static const colour darkOrchid = float4(0.6, 0.2, 0.8, 1.0);
    public static const colour darkRed = float4(0.55, 0.0, 0.0, 1.0);
    public static const colour darkSalmon = float4(0.91, 0.59, 0.48, 1.0);
    public static const colour darkSeaGreen = float4(0.56, 0.74, 0.56, 1.0);
    public static const colour darkSlateBlue = float4(0.28, 0.24, 0.55, 1.0);
    public static const colour darkSlateGray = float4(0.18, 0.31, 0.31, 1.0);
    public static const colour darkTurquoise = float4(0.0, 0.81, 0.82, 1.0);
    public static const colour darkViolet = float4(0.58, 0.0, 0.83, 1.0);
    public static const colour deepPink = float4(1.0, 0.08, 0.58, 1.0);
    public static const colour deepSkyBlue = float4(0.0, 0.75, 1.0, 1.0);
    public static const colour dimGray = float4(0.41, 0.41, 0.41, 1.0);
    public static const colour dodgerBlue = float4(0.12, 0.56, 1.0, 1.0);
    public static const colour firebrick = float4(0.7, 0.13, 0.13, 1.0);
    public static const colour floralWhite = float4(1.0, 0.98, 0.94, 1.0);
    public static const colour forestGreen = float4(0.13, 0.55, 0.13, 1.0);
    public static const colour fuchsia = float4(1.0, 0.0, 1.0, 1.0);
    public static const colour gainsboro = float4(0.86, 0.86, 0.86, 1.0);
    public static const colour ghostWhite = float4(0.97, 0.97, 1.0, 1.0);
    public static const colour gold = float4(1.0, 0.84, 0.0, 1.0);
    public static const colour goldenrod = float4(0.85, 0.65, 0.13, 1.0);
    public static const colour greenYellow = float4(0.68, 1.0, 0.18, 1.0);
    public static const colour honeydew = float4(0.94, 1.0, 0.94, 1.0);
    public static const colour hotPink = float4(1.0, 0.41, 0.71, 1.0);
    public static const colour indianRed = float4(0.8, 0.36, 0.36, 1.0);
    public static const colour indigo = float4(0.29, 0.0, 0.51, 1.0);
    public static const colour ivory = float4(1.0, 1.0, 0.94, 1.0);
    public static const colour khaki = float4(0.94, 0.9, 0.55, 1.0);
    public static const colour lavender = float4(0.9, 0.9, 0.98, 1.0);
    public static const colour lavenderBlush = float4(1.0, 0.94, 0.96, 1.0);
    public static const colour lawnGreen = float4(0.49, 0.99, 0.0, 1.0);
    public static const colour lemonChiffon = float4(1.0, 0.98, 0.8, 1.0);
    public static const colour lightBlue = float4(0.68, 0.85, 0.9, 1.0);
    public static const colour lightCoral = float4(0.94, 0.5, 0.5, 1.0);
    public static const colour lightCyan = float4(0.88, 1.0, 1.0, 1.0);
    public static const colour lightGoldenrodYellow = float4(0.98, 0.98, 0.82, 1.0);
    public static const colour lightGray = float4(0.83, 0.83, 0.83, 1.0);
    public static const colour lightGreen = float4(0.56, 0.93, 0.56, 1.0);
    public static const colour lightPink = float4(1.0, 0.71, 0.76, 1.0);
    public static const colour lightSalmon = float4(1.0, 0.63, 0.48, 1.0);
    public static const colour lightSeaGreen = float4(0.13, 0.7, 0.67, 1.0);
    public static const colour lightSkyBlue = float4(0.53, 0.81, 0.98, 1.0);
    public static const colour lightSlateGray = float4(0.47, 0.53, 0.6, 1.0);
    public static const colour lightSteelBlue = float4(0.69, 0.77, 0.87, 1.0);
    public static const colour lightYellow = float4(1.0, 1.0, 0.88, 1.0);
    public static const colour lime = float4(0.0, 1.0, 0.0, 1.0);
    public static const colour limeGreen = float4(0.2, 0.8, 0.2, 1.0);
    public static const colour linen = float4(0.98, 0.94, 0.9, 1.0);
    public static const colour maroon = float4(0.5, 0.0, 0.0, 1.0);
    public static const colour mediumAquamarine = float4(0.4, 0.8, 0.67, 1.0);
    public static const colour mediumBlue = float4(0.0, 0.0, 0.8, 1.0);
    public static const colour mediumOrchid = float4(0.73, 0.33, 0.83, 1.0);
    public static const colour mediumPurple = float4(0.58, 0.44, 0.86, 1.0);
    public static const colour mediumSeaGreen = float4(0.24, 0.7, 0.44, 1.0);
    public static const colour mediumSlateBlue = float4(0.48, 0.41, 0.93, 1.0);
    public static const colour mediumSpringGreen = float4(0.0, 0.98, 0.6, 1.0);
    public static const colour mediumTurquoise = float4(0.28, 0.82, 0.8, 1.0);
    public static const colour mediumVioletRed = float4(0.78, 0.08, 0.52, 1.0);
    public static const colour midnightBlue = float4(0.1, 0.1, 0.44, 1.0);
    public static const colour mintCream = float4(0.96, 1.0, 0.98, 1.0);
    public static const colour mistyRose = float4(1.0, 0.89, 0.88, 1.0);
    public static const colour moccasin = float4(1.0, 0.89, 0.71, 1.0);
    public static const colour navajoWhite = float4(1.0, 0.87, 0.68, 1.0);
    public static const colour navy = float4(0.0, 0.0, 0.5, 1.0);
    public static const colour oldLace = float4(0.99, 0.96, 0.9, 1.0);
    public static const colour olive = float4(0.5, 0.5, 0.0, 1.0);
    public static const colour oliveDrab = float4(0.42, 0.56, 0.14, 1.0);
    public static const colour orange = float4(1.0, 0.65, 0.0, 1.0);
    public static const colour orangeRed = float4(1.0, 0.27, 0.0, 1.0);
    public static const colour orchid = float4(0.85, 0.44, 0.84, 1.0);
    public static const colour paleGoldenrod = float4(0.93, 0.91, 0.67, 1.0);
    public static const colour paleGreen = float4(0.6, 0.98, 0.6, 1.0);
    public static const colour paleTurquoise = float4(0.69, 0.93, 0.93, 1.0);
    public static const colour paleVioletRed = float4(0.86, 0.44, 0.58, 1.0);
    public static const colour papayaWhip = float4(1.0, 0.94, 0.84, 1.0);
    public static const colour peachPuff = float4(1.0, 0.85, 0.73, 1.0);
    public static const colour peru = float4(0.8, 0.52, 0.25, 1.0);
    public static const colour pink = float4(1.0, 0.75, 0.8, 1.0);
    public static const colour plum = float4(0.87, 0.63, 0.87, 1.0);
    public static const colour powderBlue = float4(0.69, 0.88, 0.9, 1.0);
    public static const colour purple = float4(0.5, 0.0, 0.5, 1.0);
    public static const colour rosyBrown = float4(0.74, 0.56, 0.56, 1.0);
    public static const colour royalBlue = float4(0.25, 0.41, 0.88, 1.0);
    public static const colour saddleBrown = float4(0.55, 0.27, 0.07, 1.0);
    public static const colour salmon = float4(0.98, 0.5, 0.45, 1.0);
    public static const colour sandyBrown = float4(0.96, 0.64, 0.38, 1.0);
    public static const colour seaGreen = float4(0.18, 0.55, 0.34, 1.0);
    public static const colour seaShell = float4(1.0, 0.96, 0.93, 1.0);
    public static const colour sienna = float4(0.63, 0.32, 0.18, 1.0);
    public static const colour silver = float4(0.75, 0.75, 0.75, 1.0);
    public static const colour skyBlue = float4(0.53, 0.81, 0.92, 1.0);
    public static const colour slateBlue = float4(0.42, 0.35, 0.8, 1.0);
    public static const colour slateGray = float4(0.44, 0.5, 0.56, 1.0);
    public static const colour snow = float4(1.0, 0.98, 0.98, 1.0);
    public static const colour springGreen = float4(0.0, 1.0, 0.5, 1.0);
    public static const colour steelBlue = float4(0.27, 0.51, 0.71, 1.0);
    public static const colour tan = float4(0.82, 0.71, 0.55, 1.0);
    public static const colour teal = float4(0.0, 0.5, 0.5, 1.0);
    public static const colour thistle = float4(0.85, 0.75, 0.85, 1.0);
    public static const colour tomato = float4(1.0, 0.39, 0.28, 1.0);
    public static const colour turquoise = float4(0.25, 0.88, 0.82, 1.0);
    public static const colour violet = float4(0.93, 0.51, 0.93, 1.0);
    public static const colour wheat = float4(0.96, 0.87, 0.7, 1.0);
    public static const colour whiteSmoke = float4(0.96, 0.96, 0.96, 1.0);
    public static const colour yellowGreen = float4(0.6, 0.8, 0.2, 1.0);
}


/// Bringing light brighter than white back into 0..1 the way film and cameras do, instead of
/// clipping it flat. Each takes linear light (add glows up in it, as bright as they like) and
/// gives a colour for the screen, sRGB-encoded, alpha kept:
///
///     colour hdr = colours.navy.linearised();
///     hdr.rgb += glow * 6;                        // far past white
///     return tonemap.aces(hdr);
public namespace tonemap
{
    /// Reinhard's x / (1 + x): soft, never quite white, a little flat.
    public colour reinhard(colour hdr) { return colour(colours.toSrgb(hdr.rgb / (1.0 + hdr.rgb)), hdr.a); }

    /// Reinhard's with a `white`: light that bright lands on white, and brighter clips.
    public colour reinhard(colour hdr, float white)
    {
        float3 x = hdr.rgb;
        return colour(colours.toSrgb(x * (1.0 + x / (white * white)) / (1.0 + x)), hdr.a);
    }

    /// Narkowicz's quick fit of the ACES film curve: punchy, saturated highlights. The usual pick.
    public colour aces(colour hdr)
    {
        float3 x = hdr.rgb * 0.6;
        return colour(colours.toSrgb(saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14))), hdr.a);
    }

    /// Stephen Hill's closer ACES fit (with its colour matrices): bright colours drift towards
    /// white as real film's do.
    public colour acesFitted(colour hdr)
    {
        float3 v = hdr.rgb;
        v = float3(dot(v, float3(0.59719, 0.35458, 0.04823)), dot(v, float3(0.07600, 0.90834, 0.01566)), dot(v, float3(0.02840, 0.13383, 0.83777)));
        v = (v * (v + 0.0245786) - 0.000090537) / (v * (0.983729 * v + 0.4329510) + 0.238081);
        v = float3(dot(v, float3(1.60475, -0.53108, -0.07367)), dot(v, float3(-0.10208, 1.10813, -0.00605)), dot(v, float3(-0.00327, -0.07276, 1.07602)));
        return colour(colours.toSrgb(saturate(v)), hdr.a);
    }

    /// @hidden
    public float3 hableCurve(float3 x)
    {
        const float A = 0.15, B = 0.50, C = 0.10, D = 0.20, E = 0.02, F = 0.30;
        return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
    }

    /// John Hable's Uncharted 2 filmic curve: gentle toe and shoulder, a soft cinematic look.
    public colour filmic(colour hdr)
    {
        float3 v = hableCurve(hdr.rgb * 2.0) / hableCurve(float3(11.2));
        return colour(colours.toSrgb(saturate(v)), hdr.a);
    }

    /// AgX (Troy Sobotka's, Benjamin Wrensch's fit): the most natural of them; very bright
    /// colours fade towards white instead of skewing in hue (Blender's default).
    public colour agx(colour hdr)
    {
        float3 v = max(hdr.rgb, 1e-10);
        v = float3(dot(v, float3(0.842479062253094, 0.0784335999999992, 0.0792237451477643)),
                   dot(v, float3(0.0423282422610123, 0.878468636469772, 0.0791661274605434)),
                   dot(v, float3(0.0423756549057051, 0.0784336, 0.879142973793104)));
        const float lo = -12.47393, hi = 4.026069;
        v = (clamp(log2(v), lo, hi) - lo) / (hi - lo);
        float3 x2 = v * v, x4 = x2 * x2;
        v = 15.5 * x4 * x2 - 40.14 * x4 * v + 31.96 * x4 - 6.868 * x2 * v + 0.4298 * x2 + 0.1191 * v - 0.00232;
        v = float3(dot(v, float3(1.19687900512017, -0.0980208811401368, -0.0990297440797205)),
                   dot(v, float3(-0.0528968517574562, 1.15190312990417, -0.0989611768448433)),
                   dot(v, float3(-0.0529716355144438, -0.0980434501171241, 1.15107367264116)));
        return colour(saturate(v), hdr.a);
    }
}
