// xanadu.mesh: 3D from a grid of triangles. The quad comes as a grid of cells
// with uv 0..1 across it; a surface says where each uv sits in 3D, a camera
// says where that lands on screen, and `deform` moves every corner there. The
// GPU does one small sum per corner, no per-pixel geometry.
//
//     Sphere ball() { return Sphere(160).turned(float3(0.3, time, 0)); }
//     Camera camera() { return Camera(900).orbited(0, 0.3); }
//
//     [Grid(64, 64)] ItemVertex deform(ItemVertex v) { return ball().deformed(v, camera()); }
//
//     float4 shade(ItemInput i)
//     {
//         if (!mesh.facing()) return colours.transparent;       // its far side
//         colour c = colours.navy;
//         c.paint(Checker(0.08).filled(i.uv), colours.gold);      // painted on in uv
//         return ball().lit(i.uv, camera(), c);
//     }
//
// Space is in pixels: x right, y down, z into the screen, and with the camera's
// default lens something at depth 0 shows 1:1, so Sphere(160) is 320 px across.
// Build the surface in one function and call it from both stages: deform places
// the corners, shade asks it again for the normal at the pixel's uv.
//
// deformed() sets each corner's depth, so where a surface overlaps itself (a
// torus seen edge-on, a tight tube, hills in front of hills) the nearest part
// wins per pixel. Translucent surfaces still blend in grid order: hide a closed
// surface's far side with mesh.facing() if it should not show through.
//
// Shapes in shade still draw on the surface: canvas.centred runs with uv over
// the mesh, so Circle(100).filled() is a circle painted on the sphere.
module mesh;
import xanadu.math;
import xanadu.stage;
import xanadu.colour;
import xanadu.curve;
import xanadu.noise;
import xanadu.shape;

/// p turned by `angles` radians: round x (pitch, nodding), then y (yaw, turning), then z (roll).
public float3 rotate3(float3 p, float3 angles)
{
    float c = cos(angles.x), s = sin(angles.x);
    p = float3(p.x, c * p.y - s * p.z, s * p.y + c * p.z);
    c = cos(angles.y); s = sin(angles.y);
    p = float3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z);
    c = cos(angles.z); s = sin(angles.z);
    return float3(c * p.x - s * p.y, s * p.x + c * p.y, p.z);
}

/// Where a 3D thing sits: moved by offset, turned by angles (radians round x, y, then z) and
/// sized by scale, each round its own centre.
public struct Placement3
{
    public float3 offset = float3(0.0);
    public float3 angles = float3(0.0);
    public float scale = 1.0;

    /// A point in the thing's own space, placed.
    public float3 toWorld(float3 p) { return rotate3(p * scale, angles) + offset; }

    /// A world point in the thing's own space: toWorld undone.
    public float3 toLocal(float3 p) { return unrotate3(p - offset, angles) / scale; }
}

/// p turned back by `angles`: rotate3 undone (z, then y, then x, each the other way).
public float3 unrotate3(float3 p, float3 angles)
{
    p = rotate3(p, float3(0.0, 0.0, -angles.z));
    p = rotate3(p, float3(0.0, -angles.y, 0.0));
    return rotate3(p, float3(-angles.x, 0.0, 0.0));
}

/// Anything placed in 3D: surfaces. moved, turned and scaled work on all of them.
public interface IPlaced3
{
    public property Placement3 placement3 { get; set; }
}

/// A perspective camera on the origin from `distance` in front (it looks along +z), `focal` the
/// lens in pixels: the default (`focal` = `distance`) shows depth 0 at 1:1. orbited swings it
/// round the origin.
public struct Camera
{
    public float distance;
    public float focal;
    /// How far it has swung round the origin: right (yaw), up (pitch), in radians.
    public float yaw = 0.0;
    public float pitch = 0.0;

    public __init(float distance, float focal = 0.0)
    {
        this.distance = distance;
        this.focal = focal > 0.0 ? focal : distance;
    }

    /// A copy swung round the origin, `yaw` to the right and `pitch` up, in radians.
    public Camera orbited(float yaw, float pitch)
    {
        Camera c = this;
        c.yaw += yaw;
        c.pitch += pitch;
        return c;
    }

    /// A world point in the camera's own space: x right, y down, z ahead of it.
    public float3 viewed(float3 p)
    {
        // Swinging the camera one way is turning the world the other.
        p = rotate3(p, float3(0.0, -yaw, 0.0));
        p = rotate3(p, float3(pitch, 0.0, 0.0));
        return p + float3(0.0, 0.0, distance);
    }

    /// Where a world point lands, in pixels from the quad's centre.
    public float2 projected(float3 p)
    {
        float3 v = viewed(p);
        return v.xy * focal / max(v.z, 1.0);
    }

    /// How far ahead of the camera a world point is.
    public float depth(float3 p) { return viewed(p).z; }

    /// How far into the screen a world point is, in pixels: 0 on the orbit's centre (the item's
    /// plane), as ItemVertex.depth, traced.depth and Surfaces measure it.
    public float screenDepth(float3 p) { return depth(p) - distance; }

    /// How much fog lies between the camera and `p`, 0 nearer than `near` to 1 at `far` and
    /// beyond: lerp a colour towards the sky by it so distance fades.
    public float fog(float3 p, float near, float far) { return smoothstep(near, far, depth(p)); }

    /// Where the camera is, in world space.
    public property float3 eye
    {
        get
        {
            float3 e = float3(0.0, 0.0, -distance);
            e = rotate3(e, float3(-pitch, 0.0, 0.0));
            return rotate3(e, float3(0.0, yaw, 0.0));
        }
    }
}

/// A point on a surface: where it is and which way it faces (towards the outside: the side whose
/// uv reads left to right, top to bottom when you look at it).
public struct SurfacePoint
{
    public float3 position;
    public float3 normal;
};

/// A surface: where each uv (0..1 across the quad's grid) sits in its own space, then placed.
/// From outside, u should run left to right and v top to bottom, so its normal points out.
public interface ISurface : IPlaced3
{
    /// Where uv sits, in the surface's own space, before its placement.
    public float3 local(float2 uv);
}

// ---- Surfaces ---------------------------------------------------------------------

/// A flat card `size` wide and high facing the camera: flip it, tilt it, wave it.
public struct Sheet : ISurface
{
    public Placement3 placement3 = Placement3();
    public float2 size;

    public __init(float2 size) { this.size = size; }

    public float3 local(float2 uv) { return float3((uv - 0.5) * size, 0.0); }
}

/// A flat round disc `radius` across, lying flat and facing up: u goes round it, v from its rim
/// (0) in to its middle (1), so a hole of `hole` (0..1 of the radius) makes a washer. A cap for
/// a cylinder, a record, a floor.
public struct Disc : ISurface
{
    public Placement3 placement3 = Placement3();
    public float radius;
    public float hole = 0.0;

    public __init(float radius, float hole = 0.0) { this.radius = radius; this.hole = hole; }

    public float3 local(float2 uv)
    {
        float a = (uv.x - 0.5) * TAU;
        float r = radius * lerp(1.0, hole, uv.y);
        return float3(sin(a) * r, 0.0, -cos(a) * r);
    }
}

/// A ball `radius` round: u goes round it (its seam at the back), v from the top pole to the bottom.
public struct Sphere : ISurface
{
    public Placement3 placement3 = Placement3();
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float3 local(float2 uv)
    {
        float a = (uv.x - 0.5) * TAU;
        float b = uv.y * PI;
        float ring = sin(b) * radius;
        return float3(sin(a) * ring, -cos(b) * radius, -cos(a) * ring);
    }
}

/// A ring doughnut lying flat: `radius` from its middle to the middle of the tube, the tube
/// `thickness` round. u goes round the ring, v round the tube from its top.
public struct Torus : ISurface
{
    public Placement3 placement3 = Placement3();
    public float radius;
    public float thickness;

    public __init(float radius, float thickness) { this.radius = radius; this.thickness = thickness; }

    public float3 local(float2 uv)
    {
        float a = (uv.x - 0.5) * TAU;
        float b = uv.y * TAU;
        float3 outward = float3(sin(a), 0.0, -cos(a));
        return outward * (radius + sin(b) * thickness) + float3(0.0, -cos(b) * thickness, 0.0);
    }
}

/// An open tube standing up, `radius` round and `height` tall; `top` and `bottom` scale its two
/// ends (a cone is top 0, a drum is both 1).
public struct Cylinder : ISurface
{
    public Placement3 placement3 = Placement3();
    public float radius;
    public float height;
    public float top = 1.0;
    public float bottom = 1.0;

    public __init(float radius, float height, float top = 1.0, float bottom = 1.0)
    {
        this.radius = radius; this.height = height; this.top = top; this.bottom = bottom;
    }

    public float3 local(float2 uv)
    {
        float a = (uv.x - 0.5) * TAU;
        float r = radius * lerp(top, bottom, uv.y);
        return float3(sin(a) * r, (uv.y - 0.5) * height, -cos(a) * r);
    }
}

/// A 2D curve spun round the upright axis, like wood on a lathe: the curve's x is the distance
/// out from the axis and its y the height (down), from its start (v = 0) to its end (v = 1).
/// Vases, bells, goblets, spinning tops. Made by profile.lathed().
public struct Lathe<C : ICurve2> : ISurface
{
    public Placement3 placement3 = Placement3();
    public C profile;

    public float3 local(float2 uv)
    {
        float a = (uv.x - 0.5) * TAU;
        float2 at = profile.at(uv.y);
        return float3(sin(a) * at.x, at.y, -cos(a) * at.x);
    }
}

/// A 2D curve stood up as a wall `depth` deep, running into the screen: a ribbon of a fence, a
/// track's barrier, a letter's side. u along the curve, v through the depth. Made by path.extruded(depth).
public struct Extrusion<C : ICurve2> : ISurface
{
    public Placement3 placement3 = Placement3();
    public C path;
    public float depth;

    public float3 local(float2 uv) { return float3(path.at(uv.x), (uv.y - 0.5) * depth); }
}

/// A flat sheet `size` wide and high with a 2D shape raised out of it `height` px towards the
/// camera, its sides sloping over `bevel` px: any library shape made solid, an embossed badge, a
/// logo, a button. Made by shape.relief(size, height, bevel). The grid's cells set how crisp
/// its edge is.
public struct Relief<S : IShape> : ISurface
{
    public Placement3 placement3 = Placement3();
    public S shape;
    public float2 size;
    public float height;
    public float bevel;

    public float3 local(float2 uv)
    {
        float2 p = (uv - 0.5) * size;
        float up = smoothstep(bevel * 0.5, -bevel * 0.5, shape.distance(p));
        return float3(p, -up * height);
    }
}

/// What every 2D curve gives in 3D.
public extension<C : ICurve2> C
{
    /// Spun round the upright axis: x out from it, y down it.
    public Lathe<C> lathed() { Lathe<C> l; l.placement3 = Placement3(); l.profile = this; return l; }

    /// Stood up as a wall `depth` deep into the screen.
    public Extrusion<C> extruded(float depth) { Extrusion<C> e; e.placement3 = Placement3(); e.path = this; e.depth = depth; return e; }
}

/// What every 2D shape gives in 3D.
public extension<S : IShape> S
{
    /// Raised `height` px out of a sheet `size` wide and high, its sides sloping over `bevel` px.
    public Relief<S> relief(float2 size, float height, float bevel = 12.0)
    {
        Relief<S> r; r.placement3 = Placement3(); r.shape = this; r.size = size; r.height = height; r.bevel = bevel; return r;
    }
}

/// A flat strip `width` wide along a curve (a Bézier, a spline, a helix), `twist` turns of it
/// round the curve from start to end. u runs along the curve, v across it. Made by spine.ribbon(…).
public struct Ribbon<C : ICurve3> : ISurface
{
    public Placement3 placement3 = Placement3();
    public C spine;
    public float width;
    public float twist;

    public float3 local(float2 uv)
    {
        float3 side, up;
        mesh.frameAt(spine.tangent(uv.x), side, up);
        float a = twist * TAU * uv.x;
        float3 across = side * cos(a) + up * sin(a);
        return spine.at(uv.x) + across * (uv.y - 0.5) * width;
    }
}

/// A round tube `radius` thick along a curve: a pipe, a worm, a cable, a spring. u runs along the
/// curve, v round the tube; `taper` scales the far end's radius. Made by spine.tube(…).
public struct Tube<C : ICurve3> : ISurface
{
    public Placement3 placement3 = Placement3();
    public C spine;
    public float radius;
    public float taper;

    public float3 local(float2 uv)
    {
        float3 side, up;
        mesh.frameAt(spine.tangent(uv.x), side, up);
        float b = uv.y * TAU;
        float r = radius * lerp(1.0, taper, uv.x);
        return spine.at(uv.x) + (up * cos(b) + side * sin(b)) * r;
    }
}

/// What every curve gives: a tube or a ribbon along it.
public extension<C : ICurve3> C
{
    /// A round tube `radius` thick along it, the far end `taper` times as thick.
    public Tube<C> tube(float radius, float taper = 1.0)
    {
        Tube<C> t; t.placement3 = Placement3(); t.spine = this; t.radius = radius; t.taper = taper; return t;
    }

    /// A flat strip `width` wide along it, `twist` turns round it from start to end.
    public Ribbon<C> ribbon(float width, float twist = 0.0)
    {
        Ribbon<C> r; r.placement3 = Placement3(); r.spine = this; r.width = width; r.twist = twist; return r;
    }
}

/// A Möbius strip lying flat: a band `width` wide round a ring `radius` round that turns over
/// `turns` times on its way (0.5, the classic, has one side and one edge; 1.5 knots it further).
/// u goes round the ring, v across the band. One-sided, so don't hide its "back" with
/// mesh.facing(): `lit` lights both sides, and depth sorts where it passes behind itself.
public struct Mobius : ISurface
{
    public Placement3 placement3 = Placement3();
    public float radius;
    public float width;
    public float turns;

    public __init(float radius, float width, float turns = 0.5) { this.radius = radius; this.width = width; this.turns = turns; }

    public float3 local(float2 uv)
    {
        float a = (uv.x - 0.5) * TAU;
        float t = a * turns;
        float s = (uv.y - 0.5) * width;
        float3 outward = float3(sin(a), 0.0, -cos(a));
        return outward * (radius + s * cos(t)) + float3(0.0, -s * sin(t), 0.0);
    }
}

/// A Klein bottle in the figure-8 way (it passes through itself: a true one needs four
/// dimensions): a figure-8 tube `thickness` across, swept round a ring `radius` round and
/// half-turned on the way, so it meets itself inside out. u goes round the ring, v round the 8.
/// One-sided like the Möbius strip: light both sides and let depth sort it.
public struct Klein : ISurface
{
    public Placement3 placement3 = Placement3();
    public float radius;
    public float thickness;

    public __init(float radius, float thickness) { this.radius = radius; this.thickness = thickness; }

    public float3 local(float2 uv)
    {
        float a = (uv.x - 0.5) * TAU;
        float b = uv.y * TAU;
        float h = a * 0.5;
        float across = cos(h) * sin(b) - sin(h) * sin(2.0 * b);
        float up = sin(h) * sin(b) + cos(h) * sin(2.0 * b);
        float3 outward = float3(sin(a), 0.0, -cos(a));
        return outward * (radius + thickness * across) - float3(0.0, thickness * up, 0.0);
    }
}

/// A seashell: a tube coiling `turns` times down a cone `height` tall, swelling from nothing at
/// its tip to `radius` at its mouth, each whorl lying over the last (`overlap` 0 just touching,
/// 1 tucked well in). u runs from the tip to the mouth, v round the tube. Its whorls hide each
/// other, which depth sorts.
public struct Shell : ISurface
{
    public Placement3 placement3 = Placement3();
    public float radius;
    public float height;
    public float turns;
    public float overlap;

    public __init(float radius, float height, float turns = 4.0, float overlap = 0.4)
    {
        this.radius = radius; this.height = height; this.turns = turns; this.overlap = overlap;
    }

    public float3 local(float2 uv)
    {
        float a = uv.x * turns * TAU;
        // Grows the same share each turn, as a shell does (a logarithmic spiral).
        float grown = exp((uv.x - 1.0) * turns * 0.9);
        float tube = radius * grown * (0.55 + 0.25 * overlap);
        float out = radius * grown;
        float down = (grown - 0.5) * height;
        float b = uv.y * TAU;
        float3 outward = float3(sin(a), 0.0, -cos(a));
        return outward * (out + cos(b) * tube) + float3(0.0, down - sin(b) * tube, 0.0);
    }
}

/// A pill standing up: a cylinder `radius` round and `length` long between two half balls. u goes
/// round it, v from the top pole to the bottom, spaced evenly along its outline so the caps get
/// their share of the grid.
public struct Capsule : ISurface
{
    public Placement3 placement3 = Placement3();
    public float radius;
    public float length;

    public __init(float radius, float length) { this.radius = radius; this.length = length; }

    public float3 local(float2 uv)
    {
        float a = (uv.x - 0.5) * TAU;
        float cap = PI * 0.5 * radius;
        float s = uv.y * (2.0 * cap + length);
        float ring, y;
        if (s < cap)
        {
            float b = s / radius;
            ring = sin(b) * radius;
            y = -length * 0.5 - cos(b) * radius;
        }
        else if (s < cap + length)
        {
            ring = radius;
            y = s - cap - length * 0.5;
        }
        else
        {
            float b = (s - cap - length) / radius;
            ring = cos(b) * radius;
            y = length * 0.5 + sin(b) * radius;
        }
        return float3(sin(a) * ring, y, -cos(a) * ring);
    }
}

/// A ball squared off or pinched by two exponents (Barr's superquadric): `roundness` 1 is a
/// sphere, towards 0 a cube with rounded edges, 2 an octahedron, above 2 a star. `around` does
/// the same round the waist only (1 round, small square, 2 diamond).
public struct Superquadric : ISurface
{
    public Placement3 placement3 = Placement3();
    public float radius;
    public float roundness;
    public float around;

    public __init(float radius, float roundness = 0.3, float around = 0.3) { this.radius = radius; this.roundness = roundness; this.around = around; }

    public float3 local(float2 uv)
    {
        float a = (uv.x - 0.5) * TAU;
        float b = uv.y * PI;
        float ring = mesh.signedPower(sin(b), roundness);
        return radius * float3(ring * mesh.signedPower(sin(a), around), -mesh.signedPower(cos(b), roundness), -ring * mesh.signedPower(cos(a), around));
    }
}

/// A bicubic Bézier patch: a sheet `size` wide and high bent by a 4 × 4 grid of control points,
/// as car bodies and the Utah teapot are made. It starts flat; raise(column, row, offset) pulls
/// one control point (0..3 each, from the top left).
///
///     Patch hill = Patch(float2(300)).raised(1, 1, float3(0, 0, -120)).raised(2, 2, float3(0, 0, 80));
public struct Patch : ISurface
{
    public Placement3 placement3 = Placement3();
    public float3 points[16];

    public __init(float2 size)
    {
        for (int j = 0; j < 4; j++)
            for (int i = 0; i < 4; i++)
                points[j * 4 + i] = float3((float2(i, j) / 3.0 - 0.5) * size, 0.0);
    }

    /// Pulls control point (`column`, `row`) by `offset`.
    [mutating] public void raise(int column, int row, float3 offset) { points[clamp(row, 0, 3) * 4 + clamp(column, 0, 3)] += offset; }

    /// A copy with control point (`column`, `row`) pulled by `offset`.
    public Patch raised(int column, int row, float3 offset) { Patch r = this; r.raise(column, row, offset); return r; }

    public float3 local(float2 uv)
    {
        float4 bu = mesh.bernstein(uv.x), bv = mesh.bernstein(uv.y);
        float3 p = float3(0.0);
        for (int j = 0; j < 4; j++)
            for (int i = 0; i < 4; i++)
                p += points[j * 4 + i] * bu[i] * bv[j];
        return p;
    }
}

/// One surface turning into another: each uv slides from where `a` puts it to where `b` does by
/// `amount` (0 all `a`, 1 all `b`). A sheet folding into a ball on the beat. Made by a.morphed(b, t).
public struct Morph<A : ISurface, B : ISurface> : ISurface
{
    public Placement3 placement3 = Placement3();
    public A a;
    public B b;
    public float amount;

    public float3 local(float2 uv) { return lerp(a.at(uv), b.at(uv), amount); }
}

/// Rolling ground `size` wide and deep lying flat, hills up to `height` from fractal noise
/// `feature` px across; `travel` scrolls it towards the camera (pass time × a speed). v = 0 is
/// the far edge.
public struct Terrain : ISurface
{
    public Placement3 placement3 = Placement3();
    public float2 size;
    public float height;
    public float feature = 200.0;
    public float travel = 0.0;

    public __init(float2 size, float height, float feature = 200.0, float travel = 0.0)
    {
        this.size = size; this.height = height; this.feature = feature; this.travel = travel;
    }

    /// How high the ground is (0..1 of `height`) at a point on it.
    public float heightAt(float2 xz) { return noise.fbm(float2(xz.x, xz.y + travel) / max(feature, 1e-3), 4); }

    public float3 local(float2 uv)
    {
        float2 xz = float2((uv.x - 0.5) * size.x, (0.5 - uv.y) * size.y);
        return float3(xz.x, -heightAt(xz) * height, xz.y);
    }
}

/// Open water `size` wide and deep lying flat, made of six Gerstner waves (the trochoid waves
/// games use: points move in circles, so crests sharpen and troughs flatten). `height` is the
/// biggest wave's height, `wavelength` its length, `choppiness` 0..1 how sharp the crests get,
/// `wind` the direction in radians (0 rolling towards the camera). Moves with time.
public struct Ocean : ISurface
{
    public Placement3 placement3 = Placement3();
    public float2 size;
    public float height;
    public float wavelength = 260.0;
    public float choppiness = 0.6;
    public float wind = 0.0;

    public __init(float2 size, float height, float wavelength = 260.0, float choppiness = 0.6, float wind = 0.0)
    {
        this.size = size; this.height = height; this.wavelength = wavelength; this.choppiness = choppiness; this.wind = wind;
    }

    /// The water's surface point for a point `xz` on the still water, at `time`.
    public float3 displaced(float2 xz)
    {
        float3 p = float3(xz.x, 0.0, xz.y);
        for (int k = 0; k < 6; k++)
        {
            // Each wave a little shorter, lower and turned away from the wind.
            float scale = pow(0.62, float(k));
            float len = wavelength * scale;
            float amplitude = height * 0.5 * scale;
            float a = wind + (random(float2(k, 3.1)) - 0.5) * 1.6;
            float2 d = float2(sin(a), -cos(a));
            float kk = TAU / max(len, 1e-3);
            // Deep-water waves: speed grows with length (ω = √(g·k), g in pixels).
            float f = kk * dot(d, xz) - sqrt(2400.0 * kk) * time + random(float2(k, 9.7)) * TAU;
            float q = choppiness / max(kk * amplitude * 6.0, 1e-3);
            p.xz += q * amplitude * d * cos(f);
            p.y -= amplitude * sin(f);
        }
        return p;
    }

    public float3 local(float2 uv) { return displaced(float2((uv.x - 0.5) * size.x, (0.5 - uv.y) * size.y)); }

    /// How much of the surface at uv is crest (0..1): white water where the waves peak.
    public float crestAt(float2 uv) { return saturate(-local(uv).y / max(height, 1e-3) * 1.2 - 0.25); }
}

/// A surface pushed out along its normal by `amount` × a noise read at its uv: a lumpy rock, a
/// throbbing blob. `phase` makes the noise boil (pass time).
public struct Lumpy<S : ISurface> : ISurface
{
    public Placement3 placement3 = Placement3();
    public S surface;
    public float amount;
    public float feature;
    public float phase;

    public float3 local(float2 uv)
    {
        float3 p = surface.at(uv);
        float3 n = surface.normalAt(uv);
        // The noise read on the surface itself, so it has no seam where uv wraps.
        float k = noise.simplex(p / max(feature, 1e-3) + float3(0.0, 0.0, phase)) * 2.0 - 1.0;
        return p + n * k * amount;
    }
}

/// A surface rippling: pushed along its normal by a wave travelling across u.
public struct Waving<S : ISurface> : ISurface
{
    public Placement3 placement3 = Placement3();
    public S surface;
    public float amount;
    public float waves;
    public float phase;

    public float3 local(float2 uv)
    {
        return surface.at(uv) + surface.normalAt(uv) * sin((uv.x * waves - phase) * TAU) * amount;
    }
}

/// A surface wrung round its upright axis: turned by `angle` radians for every `span` px down
/// (Barr's twist). A twisted column, a wrung cloth, a drill bit from a box.
public struct Twisted<S : ISurface> : ISurface
{
    public Placement3 placement3 = Placement3();
    public S surface;
    public float angle;
    public float span;

    public float3 local(float2 uv)
    {
        float3 p = surface.at(uv);
        float a = p.y / max(span, 1e-3) * angle;
        return float3(p.x * cos(a) - p.z * sin(a), p.y, p.x * sin(a) + p.z * cos(a));
    }
}

/// A surface bent round an axis into the screen: its left-to-right line rolled onto an arc
/// that turns `angle` radians across `span` px (Barr's bend), curling up (positive) or down.
/// A plank into a hoop, a bar into a horseshoe.
public struct Bent<S : ISurface> : ISurface
{
    public Placement3 placement3 = Placement3();
    public S surface;
    public float angle;
    public float span;

    public float3 local(float2 uv)
    {
        float3 p = surface.at(uv);
        float k = angle / max(span, 1e-3);                  // radians per px along x
        if (abs(k) < 1e-6)
            return p;
        // The arc's centre sits r above (y up is negative), so positive bends lift the ends;
        // a point lower down (larger y) is further from it.
        float r = 1.0 / k;
        float a = p.x * k;
        float d = r + p.y;
        return float3(sin(a) * d, cos(a) * d - r, p.z);
    }
}

/// A surface thinned or swelled along its upright axis (Barr's taper): its width scaled from
/// `top` at y = -span/2 to `bottom` at y = span/2. A tower, a vase from a cylinder, a spike.
public struct Tapered<S : ISurface> : ISurface
{
    public Placement3 placement3 = Placement3();
    public S surface;
    public float top;
    public float bottom;
    public float span;

    public float3 local(float2 uv)
    {
        float3 p = surface.at(uv);
        float k = lerp(top, bottom, saturate(p.y / max(span, 1e-3) + 0.5));
        return float3(p.x * k, p.y, p.z * k);
    }
}

// ---- What every placed thing can do -------------------------------------------------

public extension<P : IPlaced3> P
{
    /// Moves it by `offset` in 3D.
    [mutating] public void move(float3 offset) { var p = placement3; p.offset += offset; placement3 = p; }

    /// Turns it by `angles` radians round x (nod), y (turn) and z (roll).
    [mutating] public void turn(float3 angles) { var p = placement3; p.angles += angles; placement3 = p; }

    /// Makes it `factor` times bigger round its centre.
    [mutating] public void scale(float factor) { var p = placement3; p.scale *= factor; placement3 = p; }

    /// A copy moved by `offset` in 3D.
    public P moved(float3 offset) { P r = this; r.move(offset); return r; }

    /// A copy turned by `angles` radians round x (nod), y (turn) and z (roll).
    public P turned(float3 angles) { P r = this; r.turn(angles); return r; }

    /// A copy `factor` times bigger.
    public P scaled(float factor) { P r = this; r.scale(factor); return r; }
}

// ---- What every surface can do ---------------------------------------------------------

public extension<S : ISurface> S
{
    /// Where uv sits in the world, placed.
    public float3 at(float2 uv) { return placement3.toWorld(local(uv)); }

    /// Which way the surface faces at uv, in the world: out of its front (the side whose uv
    /// reads left to right, top to bottom). From two nearby points, so any surface has one.
    public float3 normalAt(float2 uv)
    {
        const float e = 1e-3;
        float3 du = at(uv + float2(e, 0.0)) - at(uv - float2(e, 0.0));
        float3 dv = at(uv + float2(0.0, e)) - at(uv - float2(0.0, e));
        float3 n = cross(dv, du);
        float l = length(n);
        return l > 1e-9 ? n / l : float3(0.0, 0.0, -1.0);
    }

    /// Where uv sits and which way it faces.
    public SurfacePoint pointAt(float2 uv)
    {
        SurfacePoint s;
        s.position = at(uv);
        s.normal = normalAt(uv);
        return s;
    }

    /// The corner moved to where its uv lands through `camera`: what deform returns.
    public ItemVertex deformed(ItemVertex v, Camera camera) { return deformed(v, camera, v.uv); }

    /// The corner moved to where `uv` lands through `camera`, its own uv left as it is: for a
    /// piece of a split grid (mesh.split), whose uv is the piece's.
    public ItemVertex deformed(ItemVertex v, Camera camera, float2 uv)
    {
        v.place(at(uv), camera);
        return v;
    }

    /// `base` lit at uv as a studio would: a key light from the upper left, a fill, a sheen and a
    /// rim, seen through `camera`. Two-sided: the back of a sheet is lit as its front.
    public colour lit(float2 uv, Camera camera, colour base, float shininess = 40.0)
    {
        return lights.studio(pointAt(uv), camera.eye, base, shininess);
    }

    /// Pushed out along its normal by up to `amount` px of noise `feature` px across, boiling with `phase`.
    public Lumpy<S> lumpy(float amount, float feature = 80.0, float phase = 0.0)
    {
        Lumpy<S> r; r.placement3 = Placement3(); r.surface = this; r.amount = amount; r.feature = feature; r.phase = phase; return r;
    }

    /// Turning into `other` by `amount` (0 this, 1 the other): each uv slides between the two.
    public Morph<S, T> morphed<T : ISurface>(T other, float amount)
    {
        Morph<S, T> r; r.placement3 = Placement3(); r.a = this; r.b = other; r.amount = amount; return r;
    }

    /// Wrung round its upright axis: turned `angle` radians for every `span` px down.
    public Twisted<S> twisted(float angle, float span = 100.0)
    {
        Twisted<S> r; r.placement3 = Placement3(); r.surface = this; r.angle = angle; r.span = span; return r;
    }

    /// Bent round an axis into the screen: its left-to-right line rolled onto an arc turning
    /// `angle` radians across `span` px (2π across its width rolls it into a hoop).
    public Bent<S> bent(float angle, float span)
    {
        Bent<S> r; r.placement3 = Placement3(); r.surface = this; r.angle = angle; r.span = span; return r;
    }

    /// Thinned or swelled from `top` (its width's scale at y = -span/2) to `bottom` (at span/2).
    public Tapered<S> tapered(float top, float bottom, float span)
    {
        Tapered<S> r; r.placement3 = Placement3(); r.surface = this; r.top = top; r.bottom = bottom; r.span = span; return r;
    }

    /// Rippling: `waves` waves across u, `amount` px high, moving with `phase` (pass time).
    public Waving<S> waving(float amount, float waves = 3.0, float phase = 0.0)
    {
        Waving<S> r; r.placement3 = Placement3(); r.surface = this; r.amount = amount; r.waves = waves; r.phase = phase; return r;
    }
}

// ---- Bending in 2D: what deform does most -----------------------------------------------

/// The usual corner moves for `deform`, in the plane: each changes a corner where it lands
/// (the verb) or gives a moved copy (the participle), measured from the quad as it was at rest,
/// so they stack in any order. They need corners inside the quad to bend it (`[Grid]` on
/// deform); on four corners only squash, shake and tilt show.
///
///     ItemVertex deform(ItemVertex v)
///     {
///         v.wave(12, 200, time);                    // a flag
///         v.squash(0.15 * beats.pulse());           // a thump on every beat
///         return v;
///     }
public extension ItemVertex
{
    /// Moves the corner to where world point `p` lands through `camera`, and gives it p's depth
    /// so the nearest part of a surface wins where it overlaps itself: a hand-built deform's
    /// last step (`v.place(shaped(v.uv), camera()); return v;`).
    [mutating] public void place(float3 p, Camera camera)
    {
        position = rest.position + camera.projected(p);
        depth = camera.screenDepth(p);
    }

    /// A copy moved to where `p` lands through `camera`, with its depth.
    public ItemVertex placed(float3 p, Camera camera) { ItemVertex r = this; r.place(p, camera); return r; }

    /// This corner's place on the quad at rest, from its middle, in pixels.
    public property float2 fromMiddle { get { return position - rest.position; } }

    /// Waves it up and down: `amplitude` px, a wave every `wavelength` px across, moving with `phase` (pass time).
    [mutating] public void wave(float amplitude, float wavelength, float phase = 0.0)
    {
        position.y += sin((fromMiddle.x / max(wavelength, 1e-3) - phase) * TAU) * amplitude;
    }

    /// A copy waved up and down.
    public ItemVertex waved(float amplitude, float wavelength, float phase = 0.0) { ItemVertex r = this; r.wave(amplitude, wavelength, phase); return r; }

    /// Twists it round the quad's middle: the rim turns by `angle` radians, the middle stays (a whirlpool).
    [mutating] public void twist(float angle)
    {
        float2 d = fromMiddle;
        float reach = length(rest.size) * 0.5;
        position = rest.position + rotate(d, angle * saturate(length(d) / max(reach, 1e-3)));
    }

    /// A copy twisted round the middle.
    public ItemVertex twisted(float angle) { ItemVertex r = this; r.twist(angle); return r; }

    /// Swells it out round `centre` (px from the middle) within `radius`: `strength` 1 pushes
    /// corners there half way out, negative pinches them in (a lens, a bump).
    [mutating] public void bulge(float2 centre, float radius, float strength)
    {
        float2 d = fromMiddle - centre;
        float k = 1.0 - saturate(length(d) / max(radius, 1e-3));
        position += d * k * k * strength * 0.5;
    }

    /// A copy swollen round `centre`.
    public ItemVertex bulged(float2 centre, float radius, float strength) { ItemVertex r = this; r.bulge(centre, radius, strength); return r; }

    /// Squashes it from the bottom, keeping its area: `amount` 0.2 is a fifth shorter and wider;
    /// negative stretches it tall. Cartoon squash and stretch.
    [mutating] public void squash(float amount)
    {
        float2 d = fromMiddle;
        float tall = 1.0 - amount;
        float bottom = rest.size.y * 0.5;
        position = rest.position + float2(d.x / max(tall, 0.05), (d.y - bottom) * tall + bottom);
    }

    /// A copy squashed from the bottom.
    public ItemVertex squashed(float amount) { ItemVertex r = this; r.squash(amount); return r; }

    /// Leans it over from the bottom like grass in wind: the top moves `amount` px sideways,
    /// on a curve so it bends rather than shears.
    [mutating] public void sway(float amount)
    {
        float up = saturate(0.5 - fromMiddle.y / max(rest.size.y, 1e-3));   // 0 at the bottom, 1 at the top
        position.x += amount * up * up;
    }

    /// A copy leaned over from the bottom.
    public ItemVertex swayed(float amount) { ItemVertex r = this; r.sway(amount); return r; }

    /// Shakes it: every corner jumps together by up to `amount` px, `rate` times a second.
    [mutating] public void shake(float amount, float rate = 30.0)
    {
        float tick = floor(time * rate);
        position += (random2(float2(tick, 7.0)) - 0.5) * 2.0 * amount;
    }

    /// A copy shaken.
    public ItemVertex shaken(float amount, float rate = 30.0) { ItemVertex r = this; r.shake(amount, rate); return r; }

    /// Jitters it: each corner jumps on its own by up to `amount` px, `rate` times a second
    /// (shared corners stay joined): a nervous, hand-drawn boil.
    [mutating] public void jitter(float amount, float rate = 12.0)
    {
        float tick = floor(time * rate);
        position += (random2(float2(float(index), tick)) - 0.5) * 2.0 * amount;
    }

    /// A copy jittered.
    public ItemVertex jittered(float amount, float rate = 12.0) { ItemVertex r = this; r.jitter(amount, rate); return r; }

    /// Tilts it in 3D round its upright middle line by `angle` radians, through a camera `distance`
    /// px away (a card turning, a door opening): works on its four corners alone.
    [mutating] public void tilt(float angle, float distance = 900.0)
    {
        float2 d = fromMiddle;
        float3 p = rotate3(float3(d, 0.0), float3(0.0, angle, 0.0));
        position = rest.position + Camera(distance).projected(p);
        depth = p.z;
    }

    /// A copy tilted round its upright middle line.
    public ItemVertex tilted(float angle, float distance = 900.0) { ItemVertex r = this; r.tilt(angle, distance); return r; }

    /// Curls it up from its bottom right corner like a page being turned, `progress` 0 flat to 1
    /// turned right over, round a roll `radius` px thick.
    [mutating] public void curl(float progress, float radius = 40.0)
    {
        float2 d = fromMiddle;
        float2 corner = rest.size * 0.5;
        // The fold line runs across the diagonal, sweeping from the corner to past the far one.
        float2 across = normalize(float2(1.0, 0.6));
        float reach = dot(corner, across) * 2.0 + PI * radius;
        float fold = dot(corner, across) - progress * reach;
        float past = dot(d, across) - fold;
        if (past <= 0.0)
            return;
        // Wrapped round a cylinder of `radius`, then flat back over once it's gone half round.
        float a = past / max(radius, 1e-3);
        float3 bent = a < PI ? float3(sin(a) * radius, 0.0, -(1.0 - cos(a)) * radius)
                             : float3(-(past - PI * radius), 0.0, -2.0 * radius);
        float2 along = float2(-across.y, across.x);
        float2 flat = (fold + bent.x) * across + dot(d, along) * along;
        position = rest.position + Camera(900.0).projected(float3(flat, bent.z));
        depth = bent.z;
    }

    /// A copy curled like a page.
    public ItemVertex curled(float progress, float radius = 40.0) { ItemVertex r = this; r.curl(progress, radius); return r; }
}

// ---- Light and the grid ------------------------------------------------------------------

/// The usual lighting terms, each 0..1 to scale a colour by. Directions point away from the
/// surface (towards the light, towards the eye) and are normalised.
public namespace lights
{
    /// `base` lit as a studio would at a point of any surface (its position and normal), seen
    /// from `eye`: a key light from the upper left, a fill, a sheen and a rim. Two-sided. What
    /// surface.lit() does, for a point you worked out yourself.
    public colour studio(SurfacePoint s, float3 eye, colour base, float shininess = 40.0)
    {
        float3 toEye = normalize(eye - s.position);
        float3 n = dot(s.normal, toEye) < 0.0 ? -s.normal : s.normal;
        float3 key = normalize(float3(-0.5, -0.8, -0.6));
        float light = 0.18 + 0.82 * diffuse(n, key) + 0.15 * diffuse(n, normalize(float3(0.6, 0.3, -0.5)));
        float3 rgb = base.rgb * light;
        rgb += specular(n, key, toEye, shininess) * 0.5;
        rgb += rim(n, toEye, 3.0) * 0.25 * base.rgb;
        return colour(rgb, base.a);
    }

    /// Lambert's diffuse light: how squarely the surface faces the light.
    public float diffuse(float3 normal, float3 toLight) { return saturate(dot(normal, toLight)); }

    /// Diffuse light wrapped round the edge by `wrap` (0 plain, 1 all the way): soft, skin-like.
    public float wrapped(float3 normal, float3 toLight, float wrap = 0.5) { return saturate((dot(normal, toLight) + wrap) / (1.0 + wrap)); }

    /// Blinn-Phong's highlight: tighter with a higher `shininess`.
    public float specular(float3 normal, float3 toLight, float3 toEye, float shininess = 40.0)
    {
        float3 h = normalize(toLight + toEye);
        return pow(saturate(dot(normal, h)), shininess) * (shininess + 8.0) / 32.0;
    }

    /// A rim light: bright where the surface turns away at its edge (Fresnel's look); `power` tightens it.
    public float rim(float3 normal, float3 toEye, float power = 3.0) { return pow(1.0 - saturate(dot(normal, toEye)), power); }

    /// A matcap: the colour a lit studio ball would have where it faces the same way, from just
    /// the normal and the eye: `shadow` facing away, `base` side-on, `light` towards the upper
    /// left, a white sheen on top. Stylised and cheap: ceramic, clay, toys.
    public float3 matcap(float3 normal, float3 toEye, float3 shadow, float3 base, float3 light)
    {
        // The normal as the eye sees it: x right, y down on the screen.
        float3 side = normalize(cross(float3(0.0, -1.0, 0.0), toEye) + float3(1e-6, 0.0, 0.0));
        float3 up = cross(toEye, side);
        float2 m = float2(dot(normal, side), dot(normal, up));
        float t = saturate(dot(m, normalize(float2(-0.6, 0.6))) * 0.5 + 0.5);
        float3 rgb = t < 0.5 ? lerp(shadow, base, t * 2.0) : lerp(base, light, t * 2.0 - 1.0);
        float sheen = pow(saturate(1.0 - length(m - float2(-0.35, 0.4)) * 2.2), 4.0);
        return rgb + sheen * 0.6;
    }

    /// Cel shading: the diffuse light cut into `bands` flat steps.
    public float toon(float3 normal, float3 toLight, float bands = 3.0)
    {
        return floor(diffuse(normal, toLight) * bands + 0.5) / max(bands, 1.0);
    }
}

/// One piece of a grid split into several (mesh.split): which, and where in it.
public struct Piece
{
    /// Which piece: 0 at the top left, along the rows.
    public int index;
    /// Which column and row.
    public int2 at;
    /// 0..1 across this piece: give it to the piece's surface.
    public float2 uv;
    /// True on the one row or column of cells stretched between two pieces: hide it in shade.
    public bool seam;
};

public namespace mesh
{
    /// The quad's grid split into `count` pieces across and down, each its own surface: draw a
    /// different thing on each from one item. `cells` is the grid's size in cells (the
    /// deform's [Grid]); each piece should get whole cells. Call it with
    /// the same uv in deform (v.uv) and shade (i.uv), and hide `seam` pixels in shade.
    ///
    ///     Piece piece = mesh.split(v.uv, int2(3, 2));
    ///     return surfaceFor(piece.index).deformed(v, camera(), piece.uv);
    public Piece split(float2 uv, int2 count, float2 cells = float2(96.0, 54.0))
    {
        float2 n = float2(max(count, int2(1)));
        float2 per = cells / n;                                 // cells per piece
        float2 g = uv * cells;
        // A hair's nudge so a corner exactly on a boundary goes to the piece on its right / below.
        float2 which = clamp(floor((g + 0.01) / per), float2(0.0), n - 1.0);
        float2 inside = g - which * per;                        // cells into the piece
        Piece p;
        p.at = int2(which);
        p.index = p.at.y * count.x + p.at.x;
        // Each piece's last cell is the seam, so its own cells run 0..per - 1.
        p.uv = inside / max(per - 1.0, 1.0);
        p.seam = any(inside > per - 1.0 + 1e-3);
        return p;
    }

    /// Whether the pixel is on a surface's front, as you look at it: its uv reads left to right,
    /// top to bottom on screen there (mirrored means you see its back). No normal needed:
    /// return colours.transparent when it isn't, to hide a closed surface's far side.
    public bool facing()
    {
        // Measured in input(), before the effect's own branches could spoil the derivatives.
        return uvWinding > 0.0;
    }

    /// The triangles' edges at `uv` on a grid `cells` across and down, `width` screen pixels wide:
    /// each cell's sides and the diagonal that splits it into two triangles. Paint it on to see
    /// the mesh a surface is made of (on a split piece, its uv and its own cells).
    public float wireframe(float2 uv, float2 cells = float2(96.0, 54.0), float width = 1.0)
    {
        float2 g = uv * cells;
        float2 f = frac(g);
        float2 w = max(fwidth(g), float2(1e-5)) * width;
        float2 edge = min(f, 1.0 - f) / w;
        // The diagonal from a cell's top left to its bottom right, as the grid cuts its triangles.
        float diag = abs(f.x - f.y) / max(length(w) * 0.7071, 1e-5);
        float d = min(min(edge.x, edge.y), diag);
        return 1.0 - smoothstep(0.5, 1.0, d);
    }

    /// x to the power e keeping its sign: a superquadric's bend.
    /// @hidden
    public float signedPower(float x, float e) { return sign(x) * pow(abs(x), e); }

    /// The four cubic Bernstein weights at t: how much each of a Bézier's points pulls.
    /// @hidden
    public float4 bernstein(float t)
    {
        float u = 1.0 - t;
        return float4(u * u * u, 3.0 * u * u * t, 3.0 * u * t * t, t * t * t);
    }

    /// Side and up directions (unit, square to it) for a curve heading along `tangent`, for
    /// building round or across it. Up leans towards the screen's up.
    /// @hidden
    public void frameAt(float3 tangent, out float3 side, out float3 up)
    {
        float3 along = normalize(tangent + float3(1e-6, 0.0, 0.0));
        float3 reference = abs(along.y) > 0.95 ? float3(0.0, 0.0, -1.0) : float3(0.0, -1.0, 0.0);
        side = normalize(cross(along, reference));
        up = cross(side, along);
    }
}
