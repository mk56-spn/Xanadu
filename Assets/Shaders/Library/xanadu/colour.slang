// xanadu.colour: the colour type, its helpers, and the X11/web colours by
// name, all under `colours.` so none of them crowd the global names (values
// from OpenTK's Color4).
//
//     colour c = colours.gold;     a colour is a float4 (straight alpha), .rgb and all
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
