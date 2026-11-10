// xanadu.blend: Photoshop-style blend modes done in the shader, all under
// `blend.`. `under` is the colour underneath, `over` the one laid on. For the
// hardware modes (mix, add, multiply, …) pick the item's blend instead; these
// are for compositing inside one effect.
//
//     blend.overlay(under, over)   blend.by(blend.Mode.Screen, under, over)
module blend;

public namespace blend
{
    public enum Mode
    {
        Normal,
        Multiply,
        Screen,
        Darken,
        Lighten,
        Overlay,
        HardLight,
        SoftLight,
        Difference,
        Exclusion,
        Dodge,
        Burn,
    }

    public float3 normal(float3 under, float3 over) { return over; }
    public float3 multiply(float3 under, float3 over) { return under * over; }
    public float3 screen(float3 under, float3 over) { return 1.0 - (1.0 - under) * (1.0 - over); }
    public float3 darken(float3 under, float3 over) { return min(under, over); }
    public float3 lighten(float3 under, float3 over) { return max(under, over); }
    public float3 overlay(float3 under, float3 over) { return lerp(2.0 * under * over, 1.0 - 2.0 * (1.0 - under) * (1.0 - over), step(0.5, under)); }
    public float3 hardLight(float3 under, float3 over) { return lerp(2.0 * under * over, 1.0 - 2.0 * (1.0 - under) * (1.0 - over), step(0.5, over)); }
    public float3 softLight(float3 under, float3 over) { return lerp(2.0 * under * over + under * under * (1.0 - 2.0 * over), sqrt(under) * (2.0 * over - 1.0) + 2.0 * under * (1.0 - over), step(0.5, over)); }
    public float3 difference(float3 under, float3 over) { return abs(under - over); }
    public float3 exclusion(float3 under, float3 over) { return under + over - 2.0 * under * over; }
    public float3 dodge(float3 under, float3 over) { return saturate(under / max(1.0 - over, 1e-5)); }
    public float3 burn(float3 under, float3 over) { return 1.0 - saturate((1.0 - under) / max(over, 1e-5)); }

    /// One mode chosen at run time.
    public float3 by(Mode mode, float3 under, float3 over)
    {
        switch (mode)
        {
        case Mode.Multiply: return multiply(under, over);
        case Mode.Screen: return screen(under, over);
        case Mode.Darken: return darken(under, over);
        case Mode.Lighten: return lighten(under, over);
        case Mode.Overlay: return overlay(under, over);
        case Mode.HardLight: return hardLight(under, over);
        case Mode.SoftLight: return softLight(under, over);
        case Mode.Difference: return difference(under, over);
        case Mode.Exclusion: return exclusion(under, over);
        case Mode.Dodge: return dodge(under, over);
        case Mode.Burn: return burn(under, over);
        }
        return over;
    }

    /// `over` laid on `under` with a mode, then composited by its alpha (straight alpha).
    public float4 composite(Mode mode, float4 under, float4 over)
    {
        float3 mixed = by(mode, under.rgb, over.rgb);
        float a = over.a + under.a * (1.0 - over.a);
        float3 c = (mixed * over.a + under.rgb * under.a * (1.0 - over.a)) / max(a, 1e-5);
        return float4(c, a);
    }
}
