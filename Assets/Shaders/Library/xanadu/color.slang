// xanadu.color: colour helpers and the X11/web colours by name, all under
// `color.` so none of them crowd the global names (values from OpenTK's Color4).
//
//     color.gold            a float4 colour
//     color.hsv(h, s, v)    hue, saturation, value (each 0..1) to RGB
//     color.hue(h)          the hue at full saturation and value
//     color.over(top, bottom)  straight-alpha "over"
module color;

public namespace color
{
    /// Hue, saturation and value (each 0..1) to RGB.
    public float3 hsv(float hue, float saturation, float value)
    {
        float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
        float3 p = abs(frac(float3(hue) + k.xyz) * 6.0 - k.www);
        return value * lerp(k.xxx, saturate(p - k.xxx), saturation);
    }

    /// The hue at full saturation and value, cycling red, green, blue as it goes 0..1.
    public float3 hue(float hue)
    {
        return saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
    }

    /// One colour over another with straight (not premultiplied) alpha.
    public float4 over(float4 top, float4 bottom)
    {
        float a = top.a + bottom.a * (1.0 - top.a);
        if (a == 0.0)
            return float4(0.0);
        return float4((top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / a, a);
    }

    public static const float4 transparent = float4(0.0, 0.0, 0.0, 0.0);
    public static const float4 black = float4(0.0, 0.0, 0.0, 1.0);
    public static const float4 white = float4(1.0, 1.0, 1.0, 1.0);
    public static const float4 red = float4(1.0, 0.0, 0.0, 1.0);
    public static const float4 green = float4(0.0, 1.0, 0.0, 1.0);
    public static const float4 blue = float4(0.0, 0.0, 1.0, 1.0);
    public static const float4 yellow = float4(1.0, 1.0, 0.0, 1.0);
    public static const float4 cyan = float4(0.0, 1.0, 1.0, 1.0); // Also known as Aqua
    public static const float4 magenta = float4(1.0, 0.0, 1.0, 1.0); // Also known as Fuchsia
    public static const float4 gray = float4(0.5, 0.5, 0.5, 1.0);
    public static const float4 aliceBlue = float4(0.94, 0.97, 1.0, 1.0);
    public static const float4 antiqueWhite = float4(0.98, 0.92, 0.84, 1.0);
    public static const float4 aqua = float4(0.0, 1.0, 1.0, 1.0);
    public static const float4 aquamarine = float4(0.5, 1.0, 0.83, 1.0);
    public static const float4 azure = float4(0.94, 1.0, 1.0, 1.0);
    public static const float4 beige = float4(0.96, 0.96, 0.86, 1.0);
    public static const float4 bisque = float4(1.0, 0.89, 0.77, 1.0);
    public static const float4 blanchedAlmond = float4(1.0, 0.92, 0.8, 1.0);
    public static const float4 blueViolet = float4(0.54, 0.17, 0.89, 1.0);
    public static const float4 brown = float4(0.65, 0.16, 0.16, 1.0);
    public static const float4 burlyWood = float4(0.87, 0.72, 0.53, 1.0);
    public static const float4 cadetBlue = float4(0.37, 0.62, 0.63, 1.0);
    public static const float4 chartreuse = float4(0.5, 1.0, 0.0, 1.0);
    public static const float4 chocolate = float4(0.82, 0.41, 0.12, 1.0);
    public static const float4 coral = float4(1.0, 0.5, 0.31, 1.0);
    public static const float4 cornflowerBlue = float4(0.39, 0.58, 0.93, 1.0);
    public static const float4 cornsilk = float4(1.0, 0.97, 0.86, 1.0);
    public static const float4 crimson = float4(0.86, 0.08, 0.24, 1.0);
    public static const float4 darkBlue = float4(0.0, 0.0, 0.55, 1.0);
    public static const float4 darkCyan = float4(0.0, 0.55, 0.55, 1.0);
    public static const float4 darkGoldenrod = float4(0.72, 0.53, 0.04, 1.0);
    public static const float4 darkGray = float4(0.66, 0.66, 0.66, 1.0);
    public static const float4 darkGreen = float4(0.0, 0.39, 0.0, 1.0);
    public static const float4 darkKhaki = float4(0.74, 0.72, 0.42, 1.0);
    public static const float4 darkMagenta = float4(0.55, 0.0, 0.55, 1.0);
    public static const float4 darkOliveGreen = float4(0.33, 0.42, 0.18, 1.0);
    public static const float4 darkOrange = float4(1.0, 0.55, 0.0, 1.0);
    public static const float4 darkOrchid = float4(0.6, 0.2, 0.8, 1.0);
    public static const float4 darkRed = float4(0.55, 0.0, 0.0, 1.0);
    public static const float4 darkSalmon = float4(0.91, 0.59, 0.48, 1.0);
    public static const float4 darkSeaGreen = float4(0.56, 0.74, 0.56, 1.0);
    public static const float4 darkSlateBlue = float4(0.28, 0.24, 0.55, 1.0);
    public static const float4 darkSlateGray = float4(0.18, 0.31, 0.31, 1.0);
    public static const float4 darkTurquoise = float4(0.0, 0.81, 0.82, 1.0);
    public static const float4 darkViolet = float4(0.58, 0.0, 0.83, 1.0);
    public static const float4 deepPink = float4(1.0, 0.08, 0.58, 1.0);
    public static const float4 deepSkyBlue = float4(0.0, 0.75, 1.0, 1.0);
    public static const float4 dimGray = float4(0.41, 0.41, 0.41, 1.0);
    public static const float4 dodgerBlue = float4(0.12, 0.56, 1.0, 1.0);
    public static const float4 firebrick = float4(0.7, 0.13, 0.13, 1.0);
    public static const float4 floralWhite = float4(1.0, 0.98, 0.94, 1.0);
    public static const float4 forestGreen = float4(0.13, 0.55, 0.13, 1.0);
    public static const float4 fuchsia = float4(1.0, 0.0, 1.0, 1.0);
    public static const float4 gainsboro = float4(0.86, 0.86, 0.86, 1.0);
    public static const float4 ghostWhite = float4(0.97, 0.97, 1.0, 1.0);
    public static const float4 gold = float4(1.0, 0.84, 0.0, 1.0);
    public static const float4 goldenrod = float4(0.85, 0.65, 0.13, 1.0);
    public static const float4 greenYellow = float4(0.68, 1.0, 0.18, 1.0);
    public static const float4 honeydew = float4(0.94, 1.0, 0.94, 1.0);
    public static const float4 hotPink = float4(1.0, 0.41, 0.71, 1.0);
    public static const float4 indianRed = float4(0.8, 0.36, 0.36, 1.0);
    public static const float4 indigo = float4(0.29, 0.0, 0.51, 1.0);
    public static const float4 ivory = float4(1.0, 1.0, 0.94, 1.0);
    public static const float4 khaki = float4(0.94, 0.9, 0.55, 1.0);
    public static const float4 lavender = float4(0.9, 0.9, 0.98, 1.0);
    public static const float4 lavenderBlush = float4(1.0, 0.94, 0.96, 1.0);
    public static const float4 lawnGreen = float4(0.49, 0.99, 0.0, 1.0);
    public static const float4 lemonChiffon = float4(1.0, 0.98, 0.8, 1.0);
    public static const float4 lightBlue = float4(0.68, 0.85, 0.9, 1.0);
    public static const float4 lightCoral = float4(0.94, 0.5, 0.5, 1.0);
    public static const float4 lightCyan = float4(0.88, 1.0, 1.0, 1.0);
    public static const float4 lightGoldenrodYellow = float4(0.98, 0.98, 0.82, 1.0);
    public static const float4 lightGray = float4(0.83, 0.83, 0.83, 1.0);
    public static const float4 lightGreen = float4(0.56, 0.93, 0.56, 1.0);
    public static const float4 lightPink = float4(1.0, 0.71, 0.76, 1.0);
    public static const float4 lightSalmon = float4(1.0, 0.63, 0.48, 1.0);
    public static const float4 lightSeaGreen = float4(0.13, 0.7, 0.67, 1.0);
    public static const float4 lightSkyBlue = float4(0.53, 0.81, 0.98, 1.0);
    public static const float4 lightSlateGray = float4(0.47, 0.53, 0.6, 1.0);
    public static const float4 lightSteelBlue = float4(0.69, 0.77, 0.87, 1.0);
    public static const float4 lightYellow = float4(1.0, 1.0, 0.88, 1.0);
    public static const float4 lime = float4(0.0, 1.0, 0.0, 1.0);
    public static const float4 limeGreen = float4(0.2, 0.8, 0.2, 1.0);
    public static const float4 linen = float4(0.98, 0.94, 0.9, 1.0);
    public static const float4 maroon = float4(0.5, 0.0, 0.0, 1.0);
    public static const float4 mediumAquamarine = float4(0.4, 0.8, 0.67, 1.0);
    public static const float4 mediumBlue = float4(0.0, 0.0, 0.8, 1.0);
    public static const float4 mediumOrchid = float4(0.73, 0.33, 0.83, 1.0);
    public static const float4 mediumPurple = float4(0.58, 0.44, 0.86, 1.0);
    public static const float4 mediumSeaGreen = float4(0.24, 0.7, 0.44, 1.0);
    public static const float4 mediumSlateBlue = float4(0.48, 0.41, 0.93, 1.0);
    public static const float4 mediumSpringGreen = float4(0.0, 0.98, 0.6, 1.0);
    public static const float4 mediumTurquoise = float4(0.28, 0.82, 0.8, 1.0);
    public static const float4 mediumVioletRed = float4(0.78, 0.08, 0.52, 1.0);
    public static const float4 midnightBlue = float4(0.1, 0.1, 0.44, 1.0);
    public static const float4 mintCream = float4(0.96, 1.0, 0.98, 1.0);
    public static const float4 mistyRose = float4(1.0, 0.89, 0.88, 1.0);
    public static const float4 moccasin = float4(1.0, 0.89, 0.71, 1.0);
    public static const float4 navajoWhite = float4(1.0, 0.87, 0.68, 1.0);
    public static const float4 navy = float4(0.0, 0.0, 0.5, 1.0);
    public static const float4 oldLace = float4(0.99, 0.96, 0.9, 1.0);
    public static const float4 olive = float4(0.5, 0.5, 0.0, 1.0);
    public static const float4 oliveDrab = float4(0.42, 0.56, 0.14, 1.0);
    public static const float4 orange = float4(1.0, 0.65, 0.0, 1.0);
    public static const float4 orangeRed = float4(1.0, 0.27, 0.0, 1.0);
    public static const float4 orchid = float4(0.85, 0.44, 0.84, 1.0);
    public static const float4 paleGoldenrod = float4(0.93, 0.91, 0.67, 1.0);
    public static const float4 paleGreen = float4(0.6, 0.98, 0.6, 1.0);
    public static const float4 paleTurquoise = float4(0.69, 0.93, 0.93, 1.0);
    public static const float4 paleVioletRed = float4(0.86, 0.44, 0.58, 1.0);
    public static const float4 papayaWhip = float4(1.0, 0.94, 0.84, 1.0);
    public static const float4 peachPuff = float4(1.0, 0.85, 0.73, 1.0);
    public static const float4 peru = float4(0.8, 0.52, 0.25, 1.0);
    public static const float4 pink = float4(1.0, 0.75, 0.8, 1.0);
    public static const float4 plum = float4(0.87, 0.63, 0.87, 1.0);
    public static const float4 powderBlue = float4(0.69, 0.88, 0.9, 1.0);
    public static const float4 purple = float4(0.5, 0.0, 0.5, 1.0);
    public static const float4 rosyBrown = float4(0.74, 0.56, 0.56, 1.0);
    public static const float4 royalBlue = float4(0.25, 0.41, 0.88, 1.0);
    public static const float4 saddleBrown = float4(0.55, 0.27, 0.07, 1.0);
    public static const float4 salmon = float4(0.98, 0.5, 0.45, 1.0);
    public static const float4 sandyBrown = float4(0.96, 0.64, 0.38, 1.0);
    public static const float4 seaGreen = float4(0.18, 0.55, 0.34, 1.0);
    public static const float4 seaShell = float4(1.0, 0.96, 0.93, 1.0);
    public static const float4 sienna = float4(0.63, 0.32, 0.18, 1.0);
    public static const float4 silver = float4(0.75, 0.75, 0.75, 1.0);
    public static const float4 skyBlue = float4(0.53, 0.81, 0.92, 1.0);
    public static const float4 slateBlue = float4(0.42, 0.35, 0.8, 1.0);
    public static const float4 slateGray = float4(0.44, 0.5, 0.56, 1.0);
    public static const float4 snow = float4(1.0, 0.98, 0.98, 1.0);
    public static const float4 springGreen = float4(0.0, 1.0, 0.5, 1.0);
    public static const float4 steelBlue = float4(0.27, 0.51, 0.71, 1.0);
    public static const float4 tan = float4(0.82, 0.71, 0.55, 1.0);
    public static const float4 teal = float4(0.0, 0.5, 0.5, 1.0);
    public static const float4 thistle = float4(0.85, 0.75, 0.85, 1.0);
    public static const float4 tomato = float4(1.0, 0.39, 0.28, 1.0);
    public static const float4 turquoise = float4(0.25, 0.88, 0.82, 1.0);
    public static const float4 violet = float4(0.93, 0.51, 0.93, 1.0);
    public static const float4 wheat = float4(0.96, 0.87, 0.7, 1.0);
    public static const float4 whiteSmoke = float4(0.96, 0.96, 0.96, 1.0);
    public static const float4 yellowGreen = float4(0.6, 0.8, 0.2, 1.0);
}
