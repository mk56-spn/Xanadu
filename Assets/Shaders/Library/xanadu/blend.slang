// xanadu.blend: Photoshop-style blend modes, done in the shader (ported from the
// legacy BlendModes include). b is the colour underneath, s the one laid on.
// For the hardware modes (mix, add, subtract, multiply, …) pick the item's blend
// instead; these are for compositing layers inside one effect.
module blend;

public float3 blendNormal(float3 b, float3 s) { return s; }
public float3 blendMultiply(float3 b, float3 s) { return b * s; }
public float3 blendScreen(float3 b, float3 s) { return 1.0 - (1.0 - b) * (1.0 - s); }
public float3 blendDarken(float3 b, float3 s) { return min(b, s); }
public float3 blendLighten(float3 b, float3 s) { return max(b, s); }
public float3 blendOverlay(float3 b, float3 s) { return lerp(2.0 * b * s, 1.0 - 2.0 * (1.0 - b) * (1.0 - s), step(0.5, b)); }
public float3 blendHardLight(float3 b, float3 s) { return lerp(2.0 * b * s, 1.0 - 2.0 * (1.0 - b) * (1.0 - s), step(0.5, s)); }
public float3 blendSoftLight(float3 b, float3 s) { return lerp(2.0 * b * s + b * b * (1.0 - 2.0 * s), sqrt(b) * (2.0 * s - 1.0) + 2.0 * b * (1.0 - s), step(0.5, s)); }
public float3 blendDifference(float3 b, float3 s) { return abs(b - s); }
public float3 blendExclusion(float3 b, float3 s) { return b + s - 2.0 * b * s; }
// The legacy include named these two but never defined them.
public float3 blendDodge(float3 b, float3 s) { return saturate(b / max(1.0 - s, 1e-5)); }
public float3 blendBurn(float3 b, float3 s) { return 1.0 - saturate((1.0 - b) / max(s, 1e-5)); }

public static const int BLEND_NORMAL = 0;
public static const int BLEND_MULTIPLY = 1;
public static const int BLEND_SCREEN = 2;
public static const int BLEND_DARKEN = 3;
public static const int BLEND_LIGHTEN = 4;
public static const int BLEND_OVERLAY = 5;
public static const int BLEND_HARDLIGHT = 6;
public static const int BLEND_SOFTLIGHT = 7;
public static const int BLEND_DIFFERENCE = 8;
public static const int BLEND_EXCLUSION = 9;
public static const int BLEND_DODGE = 10;
public static const int BLEND_BURN = 11;

/// One mode by number (the constants above).
public float3 blendMode(int mode, float3 b, float3 s)
{
    switch (mode)
    {
    case BLEND_MULTIPLY: return blendMultiply(b, s);
    case BLEND_SCREEN: return blendScreen(b, s);
    case BLEND_DARKEN: return blendDarken(b, s);
    case BLEND_LIGHTEN: return blendLighten(b, s);
    case BLEND_OVERLAY: return blendOverlay(b, s);
    case BLEND_HARDLIGHT: return blendHardLight(b, s);
    case BLEND_SOFTLIGHT: return blendSoftLight(b, s);
    case BLEND_DIFFERENCE: return blendDifference(b, s);
    case BLEND_EXCLUSION: return blendExclusion(b, s);
    case BLEND_DODGE: return blendDodge(b, s);
    case BLEND_BURN: return blendBurn(b, s);
    }
    return s;
}

/// Straight-alpha "over" compositing of src onto dst.
public float4 alphaOver(float4 dst, float4 src)
{
    float a = src.a + dst.a * (1.0 - src.a);
    float3 c = (src.rgb * src.a + dst.rgb * dst.a * (1.0 - src.a)) / max(a, 1e-5);
    return float4(c, a);
}

/// `over` laid onto `base` with a mode, then composited by its alpha (the legacy BLEND_OVER).
public float4 blendOver(float4 base, float4 over, int mode)
{
    return alphaOver(base, float4(blendMode(mode, base.rgb, over.rgb), over.a));
}
