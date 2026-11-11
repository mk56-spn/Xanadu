// xanadu.paint: building a picture in layers. A colour paints another on top
// of itself where a shape (or any 0..1 coverage) is, with the top colour's own
// alpha and, if wanted, a blend (blend.screen, blend.behind, …):
//
//     colour c = colours.midnightBlue;
//     c.paint(plate, colours.darkSlateBlue);
//     c.paint(ring.stroked(3), colours.gold);
//     c.paint(disc.haloed(40), colours.hotPink, blend.screen);
//     c.erase(hole);                                   // cuts a hole
//
//     c.paint(box.outlined(side.top, 2));              // no colour: white
//     c.paint(box, Look(colours.slateBlue, colours.white, 2));   // fill and outline in one
//
// paint and erase change the colour; painted and erased give a changed copy, for chains.
module paint;
import xanadu.colour;
import xanadu.shape;
import xanadu.blend;

/// How a shape is painted: its fill, then an outline just inside its edge. Every part
/// starts see-through, so set only what you want:
///
///     c.paint(box, Look(colours.darkSlateBlue, colours.white, 2));
///     Look look = Look(colours.gold);
///     look.sides = side.top | side.bottom;    // only a Box's outline cares
public struct Look
{
    public colour fill = colours.transparent;
    public colour outline = colours.transparent;
    /// The outline's width, inside the edge.
    public float width = 1.0;
    /// Which of a Box's sides are outlined; other shapes are outlined all round.
    public uint sides = side.all;
    /// How far edges are smoothed.
    public float soft = 1.0;

    public __init() {}

    public __init(colour fill, colour outline = colours.transparent, float width = 1.0, uint sides = side.all)
    {
        this.fill = fill;
        this.outline = outline;
        this.width = width;
        this.sides = sides;
    }
}

public extension float4
{
    /// Lays `top` on this colour where `coverage` (0..1) is, by `how`: its mode mixes the
    /// colours, its alpha choice says what happens to the alpha (blend.behind: `top` goes
    /// underneath, showing only where this colour is see-through; blend.erase: a see-through `top` erases).
    [mutating] public void paint(float coverage, colour top = colours.white, blend.Blend how = blend.normal)
    {
        this = blend.composite(how, this, top, coverage);
    }

    /// Lays `top` on this colour inside `shape` (its fill), by `how`.
    [mutating] public void paint<S : IShape>(S shape, colour top = colours.white, blend.Blend how = blend.normal)
    {
        this.paint(shape.filled(), top, how);
    }

    /// Paints `shape` in `look`: its fill, then its outline on top, by `how`.
    [mutating] public void paint<S : IShape>(S shape, Look look, blend.Blend how = blend.normal)
    {
        this.paint(shape.filled(look.soft), look.fill, how);
        this.paint(shape.outlined(look.width, look.soft), look.outline, how);
    }

    /// Paints a box in `look`, its outline only on `look.sides`.
    [mutating] public void paint(Box box, Look look, blend.Blend how = blend.normal)
    {
        this.paint(box.filled(look.soft), look.fill, how);
        this.paint(box.outlined(look.sides, look.width, look.soft), look.outline, how);
    }

    /// A copy with `shape` painted in `look`.
    public colour painted<S : IShape>(S shape, Look look, blend.Blend how = blend.normal)
    {
        colour r = this;
        r.paint(shape, look, how);
        return r;
    }

    /// A copy with a box painted in `look`, its outline only on `look.sides`.
    public colour painted(Box box, Look look, blend.Blend how = blend.normal)
    {
        colour r = this;
        r.paint(box, look, how);
        return r;
    }

    /// A copy with `top` laid on where `coverage` (0..1) is.
    public colour painted(float coverage, colour top = colours.white, blend.Blend how = blend.normal)
    {
        colour r = this;
        r.paint(coverage, top, how);
        return r;
    }

    /// A copy with `top` laid on inside `shape`.
    public colour painted<S : IShape>(S shape, colour top = colours.white, blend.Blend how = blend.normal)
    {
        colour r = this;
        r.paint(shape.filled(), top, how);
        return r;
    }

    /// Makes this colour see-through where `coverage` (0..1) is, partly where it's partly
    /// covered: painting colours.transparent with blend.erase.
    [mutating] public void erase(float coverage)
    {
        this.paint(coverage, colours.transparent, blend.erase);
    }

    /// Makes this colour see-through inside `shape` (its fill, soft edge and all).
    [mutating] public void erase<S : IShape>(S shape)
    {
        this.erase(shape.filled());
    }

    /// A copy made see-through where `coverage` (0..1) is.
    public colour erased(float coverage)
    {
        colour r = this;
        r.erase(coverage);
        return r;
    }

    /// A copy made see-through inside `shape`.
    public colour erased<S : IShape>(S shape)
    {
        colour r = this;
        r.erase(shape.filled());
        return r;
    }
}
