// xanadu.gradient: gradients as values, centred then placed like shapes. Each
// says how far along it the pixel is, 0..1; colour it, paint it or use the
// amount:
//
//     c.paint(Linear(400).rotated(PI / 2), colours.navy, colours.black);   // top to bottom
//     c.paint(Radial(200).anchored(anchor.top), colours.gold, colours.transparent);
//     float t = Conical().rotated(time).amount();
module gradient;
import xanadu.math;
import xanadu.stage;
import xanadu.shape;

/// A gradient: how far along it a point is, in its own space. Placed with the
/// same Placement as a shape, so move, rotate, scale and anchor work on it.
public interface IGradient
{
    /// How far along a point in the gradient's own space is: 0 at its start, 1 at its end, unclamped.
    public float local(float2 p);

    /// Where it sits; rotate, move, scale and anchor change it.
    public property Placement placement { get; set; }
}

/// From left (0) to right (1) across `length` pixels, centred on the quad.
/// Rotate it for other directions: rotated(PI / 2) runs top to bottom.
public struct Linear : IGradient
{
    public Placement placement = Placement();
    public float length;

    public __init(float length) { this.length = length; }

    public float local(float2 p) { return length > 0.0001 ? p.x / length + 0.5 : 0.0; }
}

/// Out from the centre (0) to a circle `radius` pixels round it (1).
public struct Radial : IGradient
{
    public Placement placement = Placement();
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float local(float2 p) { return radius > 0.0001 ? length(p) / radius : 0.0; }
}

/// Round the centre: 0 pointing right, going clockwise (y down) all the way
/// round to 1. Rotate it to start elsewhere.
public struct Conical : IGradient
{
    public Placement placement = Placement();

    public __init() {}

    public float local(float2 p) { return mod(atan2(p.y, p.x), TAU) / TAU; }
}

// ---- What every gradient can do ----------------------------------------------

public extension<G : IGradient> G
{
    /// How far along it `position` is, 0..1.
    public float amount(float2 position) { return saturate(local(placement.toLocal(position))); }

    /// How far along it the pixel being shaded is, 0..1, from the quad's centre.
    public float amount() { return amount(shading.canvas.centred); }

    /// `from` at its start fading to `to` at its end, at `position`.
    public float4 coloured(float2 position, float4 from, float4 to) { return lerp(from, to, amount(position)); }

    /// `from` at its start fading to `to` at its end, at the pixel being shaded.
    public float4 coloured(float4 from, float4 to) { return coloured(shading.canvas.centred, from, to); }

    /// Turns it by `angle` radians round its centre, clockwise (y down).
    [mutating] public void rotate(float angle) { placement.angle += angle; }

    /// Moves it by `offset`.
    [mutating] public void move(float2 offset) { placement.offset += offset; }

    /// Makes it `factor` times bigger round its centre.
    [mutating] public void scale(float factor) { placement.scale *= factor; }

    /// Moves it by a point of the quad: anchor.topLeft, anchor.right, … or any fraction of its size from the centre.
    [mutating] public void anchor(float2 place) { placement.offset += place * shading.canvas.size; }

    /// A copy turned by `angle` radians round its centre, clockwise (y down).
    public G rotated(float angle) { G r = this; r.rotate(angle); return r; }

    /// A copy moved by `offset`.
    public G moved(float2 offset) { G r = this; r.move(offset); return r; }

    /// A copy `factor` times bigger round its centre.
    public G scaled(float factor) { G r = this; r.scale(factor); return r; }

    /// A copy moved by a point of the quad.
    public G anchored(float2 place) { G r = this; r.anchor(place); return r; }
}

public namespace gradient
{
    /// Out from `centre` (0) in squares, to the square through `edge` (1).
    public float square(float2 position, float2 centre, float2 edge)
    {
        float2 d = abs(position - centre), r = abs(edge - centre);
        float radius = max(r.x, r.y);
        return radius > 0.0001 ? max(d.x, d.y) / radius : 0.0;
    }
}
