// xanadu.blend: how paint goes onto a picture inside one effect, all under
// `blend.`. A Blend is two choices: a mode that mixes the colours (Photoshop's
// and W3C's formulas) and what happens to the alpha. Each mode is a preset,
// and .behind / .erasing change what it does to the alpha:
//
//     c.paint(glow, colours.hotPink, blend.screen);
//     c.paint(grid, colours.white, blend.behind);          // only where c is see-through
//     c.paint(hole, colours.transparent, blend.erase);     // see-through paint erases
//     c.paint(glow, colours.gold, blend.screen.behind);
//
// The colour formulas on their own (float3 under, float3 over) are under
// `blend.colours.`. To add a whole item's light to what is behind it, pick the
// item's blend instead; blend.add adds paint within one effect.
module blend;

public namespace blend
{
    /// How the colours mix.
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
        Add,
    }

    /// What painting does to the alpha.
    public enum Alpha
    {
        /// Laid on top by the paint's alpha: never more see-through.
        Over,
        /// Multiplied by the paint's alpha: a see-through paint makes it see-through (erases).
        Multiply,
        /// Laid behind: shows where the colour is see-through, partly where it's partly, never on an opaque one.
        Behind,
    }

    /// A mode for the colours and what happens to the alpha; the presets (blend.screen, …) are the usual start.
    public struct Blend
    {
        public Mode mode;
        public Alpha alpha;

        public __init(Mode mode, Alpha alpha = Alpha.Over)
        {
            this.mode = mode;
            this.alpha = alpha;
        }

        /// The same mode, laid on top (the presets already are).
        public property Blend over { get { return Blend(mode, Alpha.Over); } }

        /// The same mode, laid behind: shows only where the picture is see-through.
        public property Blend behind { get { return Blend(mode, Alpha.Behind); } }

        /// The same mode, multiplying the alpha by the paint's: see-through paint erases.
        public property Blend erasing { get { return Blend(mode, Alpha.Multiply); } }
    }

    /// The paint as it is.
    public static const Blend normal = Blend(Mode.Normal);
    /// Darkens: under × over; white leaves it as it was.
    public static const Blend multiply = Blend(Mode.Multiply);
    /// Lightens: the inverse of multiply; black leaves it as it was.
    public static const Blend screen = Blend(Mode.Screen);
    /// The darker of the two, channel by channel.
    public static const Blend darken = Blend(Mode.Darken);
    /// The lighter of the two, channel by channel.
    public static const Blend lighten = Blend(Mode.Lighten);
    /// More contrast, keyed on the colour underneath.
    public static const Blend overlay = Blend(Mode.Overlay);
    /// More contrast, keyed on the paint.
    public static const Blend hardLight = Blend(Mode.HardLight);
    /// A gentler hard light (Photoshop's curve).
    public static const Blend softLight = Blend(Mode.SoftLight);
    /// |under − over|: black leaves it, white inverts it.
    public static const Blend difference = Blend(Mode.Difference);
    /// Like difference, with less contrast.
    public static const Blend exclusion = Blend(Mode.Exclusion);
    /// Brightens the colour underneath by the paint (colour dodge).
    public static const Blend dodge = Blend(Mode.Dodge);
    /// Darkens the colour underneath by the paint (colour burn).
    public static const Blend burn = Blend(Mode.Burn);
    /// Adds the paint's light to the colour underneath, as glows and flares do (W3C's plus-lighter).
    public static const Blend add = Blend(Mode.Add);

    /// Normal, laid behind: the paint shows only where the picture is see-through.
    public static const Blend behind = Blend(Mode.Normal, Alpha.Behind);
    /// Normal, multiplying the alpha: see-through paint erases (what c.erase does).
    public static const Blend erase = Blend(Mode.Normal, Alpha.Multiply);

    /// The colour formulas on their own: `under` is the colour underneath, `over` the one laid on.
    public namespace colours
    {
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
        public float3 add(float3 under, float3 over) { return min(under + over, 1.0); }

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
            case Mode.Add: return add(under, over);
            }
            return over;
        }
    }

    /// `over` laid on `under` where `coverage` (0..1) is (straight alpha): `how.mode` mixes
    /// the colours, `how.alpha` says what happens to the alpha. Over lays `over` on top by
    /// its alpha, Multiply multiplies by it (a see-through `over` erases), Behind lays it
    /// underneath (it shows only where `under` is see-through).
    public float4 composite(Blend how, float4 under, float4 over, float coverage = 1.0)
    {
        float k = saturate(coverage);
        float strength = over.a * k;
        // Where `under` is see-through there is nothing to blend with, so `over` shows as it is.
        float3 mixed = lerp(over.rgb, colours.by(how.mode, under.rgb, over.rgb), under.a);
        if (how.alpha == Alpha.Multiply)
            return float4(lerp(under.rgb, mixed, strength), under.a * lerp(1.0, over.a, k));
        if (how.alpha == Alpha.Behind)
        {
            // `under` laid over the paint: the same over, the other way round.
            float behind = under.a + strength * (1.0 - under.a);
            return float4((under.rgb * under.a + mixed * strength * (1.0 - under.a)) / max(behind, 1e-5), behind);
        }

        float a = strength + under.a * (1.0 - strength);
        float3 c = (mixed * strength + under.rgb * under.a * (1.0 - strength)) / max(a, 1e-5);
        return float4(c, a);
    }
}
