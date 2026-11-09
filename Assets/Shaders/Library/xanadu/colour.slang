// xanadu.colour: colour-space helpers, compositing, and the X11/web colours by name
// (ported from the legacy Colours include, values from OpenTK's Color4).
module colour;

/// Hue, saturation and value (each 0..1) to RGB.
public float3 hsvToRgb(float3 c)
{
    float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + k.xyz) * 6.0 - k.www);
    return c.z * lerp(k.xxx, saturate(p - k.xxx), c.y);
}

/// The same hue at full saturation and value, cycling red, green, blue.
public float3 hueCycle(float hue)
{
    return saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
}

/// One colour over another with straight (not premultiplied) alpha.
public float4 blendOverAlpha(float4 top, float4 bottom)
{
    float a = top.a + bottom.a * (1.0 - top.a);
    if (a == 0.0)
        return float4(0.0);
    return float4((top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / a, a);
}

public static const float4 TRANSPARENT = float4(0.0, 0.0, 0.0, 0.0);
public static const float4 BLACK = float4(0.0, 0.0, 0.0, 1.0);
public static const float4 WHITE = float4(1.0, 1.0, 1.0, 1.0);
public static const float4 RED = float4(1.0, 0.0, 0.0, 1.0);
public static const float4 GREEN = float4(0.0, 1.0, 0.0, 1.0);
public static const float4 BLUE = float4(0.0, 0.0, 1.0, 1.0);
public static const float4 YELLOW = float4(1.0, 1.0, 0.0, 1.0);
public static const float4 CYAN = float4(0.0, 1.0, 1.0, 1.0); // Also known as Aqua
public static const float4 MAGENTA = float4(1.0, 0.0, 1.0, 1.0); // Also known as Fuchsia
public static const float4 GRAY = float4(0.5, 0.5, 0.5, 1.0);
public static const float4 ALICE_BLUE = float4(0.94, 0.97, 1.0, 1.0);
public static const float4 ANTIQUE_WHITE = float4(0.98, 0.92, 0.84, 1.0);
public static const float4 AQUA = float4(0.0, 1.0, 1.0, 1.0);
public static const float4 AQUAMARINE = float4(0.5, 1.0, 0.83, 1.0);
public static const float4 AZURE = float4(0.94, 1.0, 1.0, 1.0);
public static const float4 BEIGE = float4(0.96, 0.96, 0.86, 1.0);
public static const float4 BISQUE = float4(1.0, 0.89, 0.77, 1.0);
public static const float4 BLANCHED_ALMOND = float4(1.0, 0.92, 0.8, 1.0);
public static const float4 BLUE_VIOLET = float4(0.54, 0.17, 0.89, 1.0);
public static const float4 BROWN = float4(0.65, 0.16, 0.16, 1.0);
public static const float4 BURLY_WOOD = float4(0.87, 0.72, 0.53, 1.0);
public static const float4 CADET_BLUE = float4(0.37, 0.62, 0.63, 1.0);
public static const float4 CHARTREUSE = float4(0.5, 1.0, 0.0, 1.0);
public static const float4 CHOCOLATE = float4(0.82, 0.41, 0.12, 1.0);
public static const float4 CORAL = float4(1.0, 0.5, 0.31, 1.0);
public static const float4 CORNFLOWER_BLUE = float4(0.39, 0.58, 0.93, 1.0);
public static const float4 CORNSILK = float4(1.0, 0.97, 0.86, 1.0);
public static const float4 CRIMSON = float4(0.86, 0.08, 0.24, 1.0);
public static const float4 DARK_BLUE = float4(0.0, 0.0, 0.55, 1.0);
public static const float4 DARK_CYAN = float4(0.0, 0.55, 0.55, 1.0);
public static const float4 DARK_GOLDENROD = float4(0.72, 0.53, 0.04, 1.0);
public static const float4 DARK_GRAY = float4(0.66, 0.66, 0.66, 1.0);
public static const float4 DARK_GREEN = float4(0.0, 0.39, 0.0, 1.0);
public static const float4 DARK_KHAKI = float4(0.74, 0.72, 0.42, 1.0);
public static const float4 DARK_MAGENTA = float4(0.55, 0.0, 0.55, 1.0);
public static const float4 DARK_OLIVE_GREEN = float4(0.33, 0.42, 0.18, 1.0);
public static const float4 DARK_ORANGE = float4(1.0, 0.55, 0.0, 1.0);
public static const float4 DARK_ORCHID = float4(0.6, 0.2, 0.8, 1.0);
public static const float4 DARK_RED = float4(0.55, 0.0, 0.0, 1.0);
public static const float4 DARK_SALMON = float4(0.91, 0.59, 0.48, 1.0);
public static const float4 DARK_SEA_GREEN = float4(0.56, 0.74, 0.56, 1.0);
public static const float4 DARK_SLATE_BLUE = float4(0.28, 0.24, 0.55, 1.0);
public static const float4 DARK_SLATE_GRAY = float4(0.18, 0.31, 0.31, 1.0);
public static const float4 DARK_TURQUOISE = float4(0.0, 0.81, 0.82, 1.0);
public static const float4 DARK_VIOLET = float4(0.58, 0.0, 0.83, 1.0);
public static const float4 DEEP_PINK = float4(1.0, 0.08, 0.58, 1.0);
public static const float4 DEEP_SKY_BLUE = float4(0.0, 0.75, 1.0, 1.0);
public static const float4 DIM_GRAY = float4(0.41, 0.41, 0.41, 1.0);
public static const float4 DODGER_BLUE = float4(0.12, 0.56, 1.0, 1.0);
public static const float4 FIREBRICK = float4(0.7, 0.13, 0.13, 1.0);
public static const float4 FLORAL_WHITE = float4(1.0, 0.98, 0.94, 1.0);
public static const float4 FOREST_GREEN = float4(0.13, 0.55, 0.13, 1.0);
public static const float4 FUCHSIA = float4(1.0, 0.0, 1.0, 1.0);
public static const float4 GAINSBORO = float4(0.86, 0.86, 0.86, 1.0);
public static const float4 GHOST_WHITE = float4(0.97, 0.97, 1.0, 1.0);
public static const float4 GOLD = float4(1.0, 0.84, 0.0, 1.0);
public static const float4 GOLDENROD = float4(0.85, 0.65, 0.13, 1.0);
public static const float4 GREEN_YELLOW = float4(0.68, 1.0, 0.18, 1.0);
public static const float4 HONEYDEW = float4(0.94, 1.0, 0.94, 1.0);
public static const float4 HOT_PINK = float4(1.0, 0.41, 0.71, 1.0);
public static const float4 INDIAN_RED = float4(0.8, 0.36, 0.36, 1.0);
public static const float4 INDIGO = float4(0.29, 0.0, 0.51, 1.0);
public static const float4 IVORY = float4(1.0, 1.0, 0.94, 1.0);
public static const float4 KHAKI = float4(0.94, 0.9, 0.55, 1.0);
public static const float4 LAVENDER = float4(0.9, 0.9, 0.98, 1.0);
public static const float4 LAVENDER_BLUSH = float4(1.0, 0.94, 0.96, 1.0);
public static const float4 LAWN_GREEN = float4(0.49, 0.99, 0.0, 1.0);
public static const float4 LEMON_CHIFFON = float4(1.0, 0.98, 0.8, 1.0);
public static const float4 LIGHT_BLUE = float4(0.68, 0.85, 0.9, 1.0);
public static const float4 LIGHT_CORAL = float4(0.94, 0.5, 0.5, 1.0);
public static const float4 LIGHT_CYAN = float4(0.88, 1.0, 1.0, 1.0);
public static const float4 LIGHT_GOLDENROD_YELLOW = float4(0.98, 0.98, 0.82, 1.0);
public static const float4 LIGHT_GRAY = float4(0.83, 0.83, 0.83, 1.0);
public static const float4 LIGHT_GREEN = float4(0.56, 0.93, 0.56, 1.0);
public static const float4 LIGHT_PINK = float4(1.0, 0.71, 0.76, 1.0);
public static const float4 LIGHT_SALMON = float4(1.0, 0.63, 0.48, 1.0);
public static const float4 LIGHT_SEA_GREEN = float4(0.13, 0.7, 0.67, 1.0);
public static const float4 LIGHT_SKY_BLUE = float4(0.53, 0.81, 0.98, 1.0);
public static const float4 LIGHT_SLATE_GRAY = float4(0.47, 0.53, 0.6, 1.0);
public static const float4 LIGHT_STEEL_BLUE = float4(0.69, 0.77, 0.87, 1.0);
public static const float4 LIGHT_YELLOW = float4(1.0, 1.0, 0.88, 1.0);
public static const float4 LIME = float4(0.0, 1.0, 0.0, 1.0);
public static const float4 LIME_GREEN = float4(0.2, 0.8, 0.2, 1.0);
public static const float4 LINEN = float4(0.98, 0.94, 0.9, 1.0);
public static const float4 MAROON = float4(0.5, 0.0, 0.0, 1.0);
public static const float4 MEDIUM_AQUAMARINE = float4(0.4, 0.8, 0.67, 1.0);
public static const float4 MEDIUM_BLUE = float4(0.0, 0.0, 0.8, 1.0);
public static const float4 MEDIUM_ORCHID = float4(0.73, 0.33, 0.83, 1.0);
public static const float4 MEDIUM_PURPLE = float4(0.58, 0.44, 0.86, 1.0);
public static const float4 MEDIUM_SEA_GREEN = float4(0.24, 0.7, 0.44, 1.0);
public static const float4 MEDIUM_SLATE_BLUE = float4(0.48, 0.41, 0.93, 1.0);
public static const float4 MEDIUM_SPRING_GREEN = float4(0.0, 0.98, 0.6, 1.0);
public static const float4 MEDIUM_TURQUOISE = float4(0.28, 0.82, 0.8, 1.0);
public static const float4 MEDIUM_VIOLET_RED = float4(0.78, 0.08, 0.52, 1.0);
public static const float4 MIDNIGHT_BLUE = float4(0.1, 0.1, 0.44, 1.0);
public static const float4 MINT_CREAM = float4(0.96, 1.0, 0.98, 1.0);
public static const float4 MISTY_ROSE = float4(1.0, 0.89, 0.88, 1.0);
public static const float4 MOCCASIN = float4(1.0, 0.89, 0.71, 1.0);
public static const float4 NAVAJO_WHITE = float4(1.0, 0.87, 0.68, 1.0);
public static const float4 NAVY = float4(0.0, 0.0, 0.5, 1.0);
public static const float4 OLD_LACE = float4(0.99, 0.96, 0.9, 1.0);
public static const float4 OLIVE = float4(0.5, 0.5, 0.0, 1.0);
public static const float4 OLIVE_DRAB = float4(0.42, 0.56, 0.14, 1.0);
public static const float4 ORANGE = float4(1.0, 0.65, 0.0, 1.0);
public static const float4 ORANGE_RED = float4(1.0, 0.27, 0.0, 1.0);
public static const float4 ORCHID = float4(0.85, 0.44, 0.84, 1.0);
public static const float4 PALE_GOLDENROD = float4(0.93, 0.91, 0.67, 1.0);
public static const float4 PALE_GREEN = float4(0.6, 0.98, 0.6, 1.0);
public static const float4 PALE_TURQUOISE = float4(0.69, 0.93, 0.93, 1.0);
public static const float4 PALE_VIOLET_RED = float4(0.86, 0.44, 0.58, 1.0);
public static const float4 PAPAYA_WHIP = float4(1.0, 0.94, 0.84, 1.0);
public static const float4 PEACH_PUFF = float4(1.0, 0.85, 0.73, 1.0);
public static const float4 PERU = float4(0.8, 0.52, 0.25, 1.0);
public static const float4 PINK = float4(1.0, 0.75, 0.8, 1.0);
public static const float4 PLUM = float4(0.87, 0.63, 0.87, 1.0);
public static const float4 POWDER_BLUE = float4(0.69, 0.88, 0.9, 1.0);
public static const float4 PURPLE = float4(0.5, 0.0, 0.5, 1.0);
public static const float4 ROSY_BROWN = float4(0.74, 0.56, 0.56, 1.0);
public static const float4 ROYAL_BLUE = float4(0.25, 0.41, 0.88, 1.0);
public static const float4 SADDLE_BROWN = float4(0.55, 0.27, 0.07, 1.0);
public static const float4 SALMON = float4(0.98, 0.5, 0.45, 1.0);
public static const float4 SANDY_BROWN = float4(0.96, 0.64, 0.38, 1.0);
public static const float4 SEA_GREEN = float4(0.18, 0.55, 0.34, 1.0);
public static const float4 SEA_SHELL = float4(1.0, 0.96, 0.93, 1.0);
public static const float4 SIENNA = float4(0.63, 0.32, 0.18, 1.0);
public static const float4 SILVER = float4(0.75, 0.75, 0.75, 1.0);
public static const float4 SKY_BLUE = float4(0.53, 0.81, 0.92, 1.0);
public static const float4 SLATE_BLUE = float4(0.42, 0.35, 0.8, 1.0);
public static const float4 SLATE_GRAY = float4(0.44, 0.5, 0.56, 1.0);
public static const float4 SNOW = float4(1.0, 0.98, 0.98, 1.0);
public static const float4 SPRING_GREEN = float4(0.0, 1.0, 0.5, 1.0);
public static const float4 STEEL_BLUE = float4(0.27, 0.51, 0.71, 1.0);
public static const float4 TAN = float4(0.82, 0.71, 0.55, 1.0);
public static const float4 TEAL = float4(0.0, 0.5, 0.5, 1.0);
public static const float4 THISTLE = float4(0.85, 0.75, 0.85, 1.0);
public static const float4 TOMATO = float4(1.0, 0.39, 0.28, 1.0);
public static const float4 TURQUOISE = float4(0.25, 0.88, 0.82, 1.0);
public static const float4 VIOLET = float4(0.93, 0.51, 0.93, 1.0);
public static const float4 WHEAT = float4(0.96, 0.87, 0.7, 1.0);
public static const float4 WHITE_SMOKE = float4(0.96, 0.96, 0.96, 1.0);
public static const float4 YELLOW_GREEN = float4(0.6, 0.8, 0.2, 1.0);
