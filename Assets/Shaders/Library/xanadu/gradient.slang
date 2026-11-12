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

/// A gradient: how far along it a point is, in its own space. Placed like a
/// shape (IPlaced), so move, rotate, scale and anchor work on it.
public interface IGradient : IPlaced
{
    /// How far along a point in the gradient's own space is: 0 at its start, 1 at its end, unclamped.
    public float local(float2 p);
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
