// xanadu.paint: building a picture in layers. A colour paints another on top
// of itself where a shape (or any 0..1 coverage) is, with the top colour's own
// alpha and, if wanted, a blend mode:
//
//     colour c = colours.midnightBlue;
//     c.paint(plate, colours.darkSlateBlue);
//     c.paint(ring.stroke(3), colours.gold);
//     c.paint(halo.glow(40), colours.hotPink, blend.Mode.Screen);
//
// paint changes the colour; painted gives a painted copy, for chains.
module paint;
import xanadu.colour;
import xanadu.shape;
import xanadu.blend;

public extension float4
{
    /// Lays `top` on this colour where `coverage` (0..1) is, by its own alpha and `mode`.
    [mutating] public void paint(float coverage, colour top, blend.Mode mode = blend.Mode.Normal)
    {
        this = blend.composite(mode, this, colour(top.rgb, top.a * saturate(coverage)));
    }

    /// Lays `top` on this colour inside `shape` (its fill), by its own alpha and `mode`.
    [mutating] public void paint<S : IShape>(S shape, colour top, blend.Mode mode = blend.Mode.Normal)
    {
        this.paint(shape.fill(), top, mode);
    }

    /// A copy with `top` laid on where `coverage` (0..1) is.
    public colour painted(float coverage, colour top, blend.Mode mode = blend.Mode.Normal)
    {
        colour r = this;
        r.paint(coverage, top, mode);
        return r;
    }

    /// A copy with `top` laid on inside `shape`.
    public colour painted<S : IShape>(S shape, colour top, blend.Mode mode = blend.Mode.Normal)
    {
        colour r = this;
        r.paint(shape.fill(), top, mode);
        return r;
    }
}
