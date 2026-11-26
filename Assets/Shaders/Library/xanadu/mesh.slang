// xanadu.mesh: 3D and 2D meshes. A surface says where each uv (0..1 across and down) sits
// in 3D; a generator ([Pass], mesh.grid) builds it into the pool, pages of triangles in
// buffers; one counted [Draw] pulls every page's corners and places them through a camera
// (pool.pulled, 3D) or flat on the item (pool.flat, 2D). "The pool" below has the whole of it.
//
// Space is in pixels: x right, y down, z into the screen, and with the camera's
// default lens something at depth 0 shows 1:1, so Sphere(160) is 320 px across.
//
// Each corner carries its depth, so where a surface overlaps itself (a torus seen edge-on, a
// tight tube, hills in front of hills) the nearest part wins per pixel, and a closed
// surface's far side is hidden by its near side. A draw's shade gets the world point and
// normal (i.world, i.normal) to light by (mesh.lit).
//
// Shapes in shade still draw on the surface: i.uv is the surface's own, so
// Checker(0.08).filled(i.uv) is painted on the sphere.
module mesh;
import xanadu.math;
import xanadu.stage;
import xanadu.colour;
import xanadu.curve;
import xanadu.noise;
import xanadu.shape;
import xanadu.store;

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

    /// A camera at `eye` looking at the origin, `focal` its lens: a lamp's view for its shadow map.
    public __init(float3 eye, float focal)
    {
        distance = max(length(eye), 1e-3);
        this.focal = focal;
        // eye = -distance (sin yaw cos pitch, sin pitch, cos yaw cos pitch), from the orbit's turns.
        pitch = asin(clamp(-eye.y / distance, -1.0, 1.0));
        yaw = atan2(-eye.x, -eye.z);
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
    /// plane), as ItemVertex.depth, present's depth and Surfaces measure it.
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

/// A 2D curve drawn as a flat strip `width` wide, lying in the plane (z = 0): a brush stroke,
/// a road, a vine. u along the curve, v across it; `taper` scales its width at the end (0 a
/// point). For mesh.flat, the 2D door. Made by path.stroked(width).
public struct Stroke<C : ICurve2> : ISurface
{
    public Placement3 placement3 = Placement3();
    public C path;
    public float width;
    public float taper = 1.0;

    public float3 local(float2 uv)
    {
        float2 along = normalize(path.tangent(uv.x) + float2(1e-6, 0.0));
        float2 side = float2(-along.y, along.x);
        return float3(path.at(uv.x) + side * (uv.y - 0.5) * width * lerp(1.0, taper, uv.x), 0.0);
    }
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

    /// Drawn as a flat strip `width` wide along it, `taper` times as wide at its end.
    public Stroke<C> stroked(float width, float taper = 1.0)
    {
        Stroke<C> s; s.placement3 = Placement3(); s.path = this; s.width = width; s.taper = taper; return s;
    }
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
/// u goes round the ring, v across the band. One-sided: `lit` lights both sides, and depth
/// sorts where it passes behind itself.
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

// ---- The pool: meshes built once, drawn by one draw ------------------------------------
//
// A graph keeps its meshes in a pool of pages (meshlets): each page holds up to mesh.PAGE
// corners and mesh.PAGE triangles of one object, its triangles three corner numbers inside the
// page. A generator is a [Pass] writing pages; on [Cached] buffers it runs only when the
// material's values move, so a mesh is built once and every frame only draws it. One counted
// draw shows every page, a page a copy, its corners pulled from the pool by their numbers:
//
//     static const uint PAGES = 2 * 16;                  // two objects, a 32 x 32 grid each (mesh.pagesFor)
//     [Cached] [Pages(PAGES)] Meshes pool;
//
//     [Pass(128, over: PAGES * 128)]
//     void build(uint i)
//     {
//         MeshPage p = pool.page(i, 16);                 // which object, its thread, its first page
//         if (p.object == 0) pool.grid(Sphere(100), p, uint2(32));
//         else pool.grid(Torus(90, 30), p, uint2(32));
//     }
//
//     [Draw(64, 1, shade: look, copies: PAGES)]                // 64 cells: a page's 128 triangles
//     ItemVertex shown(ItemVertex v) { return pool.pulled(v, placed(pool.objectOf(copy)), camera()); }
//
//     float4 look(ItemInput i) { return colour(mesh.lit(i, camera(), colours.gold), 1); }
//
// The same pool has two front doors: pool.pulled for 3D (a placement and a camera, lit from
// the world point and normal it hands the shade) and pool.flat for 2D (x and y in pixels on
// the item, z the layer, no camera, unlit). A mesh whose shape moves every frame (an ocean, a
// waving flag) is built into a pool without [Cached] instead and rebuilt each frame; one that only
// turns or moves stays cached and is placed by the draw.

/// One corner in the pool: where it sits in its object's own space, which way it faces, its uv.
public struct MeshVertex
{
    public float3 position;
    public float u;
    public float3 normal;
    public float v;

    /// Its uv on the surface it was built from.
    public property float2 uv { get { return float2(u, v); } }
};

/// One page of the pool: whose it is, how many of its triangles and corners are used, and its bounds
/// in the object's own space (pool.bound works them out): a ball round its corners, and the cone its
/// triangles face within (axis, and cutoff 1 when they face every way).
public struct Meshlet
{
    public uint object;
    public uint triangles;
    public uint corners;
    public uint pad;
    public float3 centre;
    public float radius;
    public float3 axis;
    public float cutoff;
};

/// Any list of triangles, for pool.list: how many, and each corner of each.
public interface ITriangles
{
    /// How many triangles it has.
    public uint count();
    /// Corner k (0, 1 or 2) of triangle t.
    public MeshVertex corner(uint t, uint k);
};

/// Triangles as three corners each, one after another: [Host] or built lists of MeshVertex.
public struct CornerList : ITriangles
{
    public RWStructuredBuffer<MeshVertex> corners;
    public uint triangles;

    public __init(RWStructuredBuffer<MeshVertex> corners, uint triangles) { this.corners = corners; this.triangles = triangles; }
    public uint count() { return triangles; }
    public MeshVertex corner(uint t, uint k) { return corners[t * 3 + k]; }
};

/// Triangles as three corner numbers each into a list of corners, as model files keep them.
public struct IndexedList : ITriangles
{
    public RWStructuredBuffer<MeshVertex> vertices;
    public RWStructuredBuffer<uint> indices;
    public uint triangles;

    public __init(RWStructuredBuffer<MeshVertex> vertices, RWStructuredBuffer<uint> indices, uint triangles)
    {
        this.vertices = vertices;
        this.indices = indices;
        this.triangles = triangles;
    }
    public uint count() { return triangles; }
    public MeshVertex corner(uint t, uint k) { return vertices[indices[t * 3 + k]]; }
};

public extension ItemVertex
{
    /// Folded away: every corner of a triangle a page doesn't use lands on one point, so it
    /// draws nothing.
    [mutating] public void fold()
    {
        position = rest.position;
        depth = 0.0;
    }
}


public namespace mesh
{
    /// Whether the pixel's triangle faces the screen as it was built: false where a bend turned
    /// it over (a page curling, a card turning round), so its back can be painted apart.
    public bool facing()
    {
        return frontFacing;
    }

    /// The triangles' edges at `uv` on a grid `cells` across and down, `width` screen pixels wide:
    /// each cell's sides and the diagonal that splits it into two triangles. Paint it on to see
    /// the mesh a surface is made of (the generator's cells, mesh.grid).
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

    /// Corners and triangles a page holds; a draw of the pool is [Draw(64, 1, …)], a page a copy.
    public static const uint PAGE = 128;
    /// Cells across and down a page of a grid holds (9 × 9 corners, 128 triangles).
    public static const uint PATCH = 8;

    /// How many pages a grid of `cells` across and down takes: a page every 8 × 8 cells.
    public uint pagesFor(uint2 cells)
    {
        uint2 n = (cells + PATCH - 1) / PATCH;
        return n.x * n.y;
    }

    /// Triangles a page of a triangle list holds: each its own three corners (pool.list).
    public static const uint LISTED = 42;

    /// How many pages a list of `triangles` takes.
    public uint pagesForTriangles(uint triangles) { return (triangles + LISTED - 1) / LISTED; }

    /// `base` lit at a pool draw's pixel as `lit` lights a surface (the studio's key, fill, sheen
    /// and rim), from the world point and normal its vertex stage handed on. Two-sided.
    public colour lit(ItemInput i, Camera camera, colour base, float shininess = 40.0)
    {
        SurfacePoint s;
        s.position = i.world;
        s.normal = normalize(i.normal);
        return lights.studio(s, camera.eye, base, shininess);
    }
}

/// Which object thread `i` of a pool's build works on, when each object takes `each` pages:
/// its thread within that object and its first page (Meshes.page).
public struct MeshPage
{
    public uint object;
    public uint thread;
    public uint first;
};

/// A graph's mesh pool, built into and drawn from: [Pages(n)] Meshes pool; (with [Cached] to
/// build it only when the values move). Its corners, triangles and pages are the build's own, and so
/// is its book: the pages taken so far (what `copies: pool` draws), then each object's first page.
public struct Meshes
{
    public RWStructuredBuffer<MeshVertex> corners;
    public RWStructuredBuffer<uint> triangles;
    public RWStructuredBuffer<Meshlet> pages;
    public RWStructuredBuffer<uint> book;
    public uint most;

    public __init(RWStructuredBuffer<MeshVertex> corners, RWStructuredBuffer<uint> triangles, RWStructuredBuffer<Meshlet> pages,
        RWStructuredBuffer<uint> book, uint most)
    {
        this.corners = corners;
        this.triangles = triangles;
        this.pages = pages;
        this.book = book;
        this.most = most;
    }

    // ---- pages taken as needed: objects of any size, packed one after another ----
    //
    //     [Pass(1, over: 1)] void restart(uint i) { pool.clear(); }
    //     [Pass(64, over: OBJECTS)] void claim(uint o) { pool.place(o, pool.take(mesh.pagesFor(cellsOf(o)))); }
    //     [Pass(128, over: OBJECTS * MOST * 128)] void build(uint i) { MeshPage p = pool.taken(i, MOST); … }
    //     [Draw(64, 1, shade: look, copies: pool)] …      // only the pages taken

    /// Gives every page back: run it alone in a pass before the claims.
    public void clear() { book[0] = 0; }

    /// The pages taken so far.
    public property uint used { get { return min(book[0], most); } }

    /// Takes `count` pages one after another and returns the first; past the pool's end
    /// (most), nothing is built there.
    public uint take(uint count)
    {
        uint first;
        InterlockedAdd(book[0], count, first);
        return first;
    }

    /// Notes that `object`'s pages start at `first`, for the builders (firstOf).
    public void place(uint object, uint first) { book[1 + object] = first; }

    /// Where `object`'s pages start, as place noted it.
    public uint firstOf(uint object) { return book[1 + object]; }

    /// Thread `i` of a build where each object takes up to `each` pages, from the first it took.
    public MeshPage taken(uint i, uint each)
    {
        MeshPage p = page(i, each);
        p.first = firstOf(p.object);
        return p;
    }

    // ---- bounds and culling: draw only the pages a camera sees ----
    //
    //     [Size(PAGES)] Gathered<uint> seen;
    //     [Pass(64, over: PAGES)] void bounds(uint i) { pool.bound(i); }      // after building (cached with it)
    //     [Pass(1, over: 1)] void forget(uint i) { seen.clear(); }
    //     [Pass(64, over: PAGES)] void look(uint i) { pool.cull(i, placed(pool.objectOf(i)), camera(), VIEW, seen); }
    //     [Draw(64, 1, shade: lit, copies: seen)]
    //     ItemVertex shown(ItemVertex v) { uint p = seen[copy]; return pool.pulled(v, placed(pool.objectOf(p)), camera(), p); }

    /// Works out page `n`'s bounds from its corners: a ball round them, and the cone its triangles
    /// face within. Run it a thread a page, after the pages are built.
    public void bound(uint n)
    {
        if (n >= most)
            return;
        Meshlet m = pages[n];
        uint base = n * mesh.PAGE;
        float3 low = float3(1e30), high = float3(-1e30);
        for (uint k = 0; k < m.corners; k++)
        {
            low = min(low, corners[base + k].position);
            high = max(high, corners[base + k].position);
        }
        float3 centre = (low + high) * 0.5;
        float radius = 0.0;
        float3 sum = float3(0.0);
        for (uint k = 0; k < m.corners; k++)
        {
            radius = max(radius, length(corners[base + k].position - centre));
            sum += corners[base + k].normal;
        }
        float3 axis = length(sum) > 1e-6 ? normalize(sum) : float3(0.0, 0.0, -1.0);
        float least = 1.0;
        for (uint k = 0; k < m.corners; k++)
            least = min(least, dot(axis, normalize(corners[base + k].normal + float3(1e-9))));
        m.centre = centre;
        m.radius = radius;
        m.axis = axis;
        // Facing within the cone's half angle: cut off by its sine; a cone of 90° or more faces every way.
        m.cutoff = least > 0.0 ? sqrt(1.0 - least * least) : 1.0;
        pages[n] = m;
    }

    /// Whether page `n`, its object placed by `placed`, shows through `camera` in a view `halfView`
    /// pixels each way from the centre: in front of the camera and on screen, and with `oneSided` its
    /// triangles not all facing away.
    public bool sees(uint n, Placement3 placed, Camera camera, float2 halfView, bool oneSided = false)
    {
        if (n >= used && used > 0 || n >= most)
            return false;
        Meshlet m = pages[n];
        if (m.triangles == 0)
            return false;
        float3 centre = placed.toWorld(m.centre);
        float radius = m.radius * placed.scale;
        float ahead = camera.depth(centre);
        if (ahead + radius <= 1.0)
            return false;
        float near = max(ahead - radius, 1.0);
        float2 at = camera.projected(centre);
        float reach = radius * camera.focal / near;
        if (any(abs(at) - reach > halfView))
            return false;
        if (oneSided && m.cutoff < 1.0)
        {
            float3 axis = rotate3(m.axis, placed.angles);
            float3 to = centre - camera.eye;
            if (dot(to, axis) >= m.cutoff * length(to) + radius)
                return false;
        }
        return true;
    }

    /// Adds page `n` to `seen` when the camera sees it (sees); a thread a page, after seen.clear().
    public void cull(uint n, Placement3 placed, Camera camera, float2 halfView, Gathered<uint> seen, bool oneSided = false)
    {
        if (sees(n, placed, camera, halfView, oneSided))
            seen.add(n);
    }

    /// Thread `i` of a build where every object takes `each` pages, one after another from page 0.
    public MeshPage page(uint i, uint each)
    {
        MeshPage p;
        p.object = i / (each * mesh.PAGE);
        p.thread = i % (each * mesh.PAGE);
        p.first = p.object * each;
        return p;
    }

    /// The object drawn page `n` (a draw's `copy`) belongs to.
    public uint objectOf(uint n) { return pages[n].object; }

    /// Thread `t` of building triangle list `list` into the pages from `first` on, each marked
    /// `object`: run it over mesh.pagesForTriangles(n) × mesh.PAGE threads (t from 0). Each page holds
    /// mesh.LISTED triangles with their own corners, so any mesh goes in: a model's, a host's, one a
    /// pass worked out.
    public void list<L : ITriangles>(L list, uint t, uint first, uint object)
    {
        uint page = t / mesh.PAGE, k = t % mesh.PAGE;
        uint count = list.count();
        uint start = page * mesh.LISTED;
        if (start >= count || first + page >= most)
            return;
        uint here = min(count - start, mesh.LISTED);
        uint slot = (first + page) * mesh.PAGE + k;
        if (k < here * 3)
            corners[slot] = list.corner(start + k / 3, k % 3);
        if (k < here)
            triangles[slot] = (k * 3) | ((k * 3 + 1) << 8) | ((k * 3 + 2) << 16);
        if (k == 0)
        {
            Meshlet m = {};
            m.object = object;
            m.triangles = here;
            m.corners = here * 3;
            m.cutoff = 1.0;
            pages[first + page] = m;
        }
    }

    /// Builds surface `s` as a grid of `cells` for page p's object, at its pages.
    public void grid<S : ISurface>(S s, MeshPage p, uint2 cells) { grid(s, p.thread, cells, p.first, p.object); }

    /// Thread `t` of building surface `s` as a grid of `cells`, into the pages from `first` on,
    /// each marked `object`: run it over mesh.pagesFor(cells) × mesh.PAGE threads (t from 0). Each
    /// page is 8 × 8 cells of the grid (fewer at its right and bottom edges), cut into triangles as
    /// the item's grid is, each corner placed with its normal and uv.
    public void grid<S : ISurface>(S s, uint t, uint2 cells, uint first, uint object)
    {
        uint page = t / mesh.PAGE, k = t % mesh.PAGE;
        uint2 count2 = (cells + mesh.PATCH - 1) / mesh.PATCH;
        if (page >= count2.x * count2.y)
            return;
        uint2 at = uint2(page % count2.x, page / count2.x) * mesh.PATCH;    // its first cell
        uint2 size = min(cells - at, uint2(mesh.PATCH));                     // its cells
        uint slot = (first + page) * mesh.PAGE + k;
        uint across = size.x + 1;
        uint2 corner = uint2(k % across, k / across);
        if (corner.y <= size.y)
        {
            float2 uv = float2(at + corner) / float2(cells);
            MeshVertex c;
            c.position = s.at(uv);
            c.normal = s.normalAt(uv);
            c.u = uv.x;
            c.v = uv.y;
            corners[slot] = c;
        }

        uint count = 2 * size.x * size.y;
        if (k < count)
        {
            uint cell = k / 2;
            uint a = (cell / size.x) * across + cell % size.x, b = a + 1, c = a + across, d = c + 1;
            // Split top left to bottom right, as the item's grid is.
            uint3 corners3 = (k & 1) == 0 ? uint3(a, b, d) : uint3(a, d, c);
            triangles[slot] = corners3.x | (corners3.y << 8) | (corners3.z << 16);
        }

        if (k == 0)
        {
            Meshlet m = {};
            m.object = object;
            m.triangles = count;
            m.corners = across * (size.y + 1);
            m.cutoff = 1.0;
            pages[first + page] = m;
        }
    }

    /// The corner the draw's vertex `v` stands for (page `page`, or the draw's `copy` when it is
    /// -1; triangle v.index / 3); false for a triangle its page doesn't use.
    public bool corner(ItemVertex v, out MeshVertex c, int page = -1)
    {
        uint n = page < 0 ? copy : uint(page);
        uint t = v.index / 3;
        Meshlet m = pages[n];
        if (t >= m.triangles)
        {
            c = {};
            return false;
        }
        uint packed = triangles[n * mesh.PAGE + t];
        c = corners[n * mesh.PAGE + ((packed >> (8 * (v.index % 3))) & 0xFF)];
        return true;
    }

    /// A 3D draw of the pool: the corner `v` stands for, placed by `placed` and seen through
    /// `camera`, with its world point, normal and uv set for the shade (i.world, i.normal, i.uv).
    /// It draws page `page`, or the draw's `copy` when that is -1 (a culled draw passes seen[copy]).
    public ItemVertex pulled(ItemVertex v, Placement3 placed, Camera camera, int page = -1)
    {
        MeshVertex c;
        if (!corner(v, c, page))
        {
            v.fold();
            return v;
        }
        float3 p = placed.toWorld(c.position);
        v.place(p, camera);
        v.uv = c.uv;
        v.world = p;
        v.normal = rotate3(c.normal, placed.angles);
        return v;
    }

    /// A 2D draw of the pool: the corner's x and y are pixels on the item from its middle, z its
    /// layer (nearer layers win, as depth does), turned by `angle` radians, scaled by `scale` and
    /// moved by `offset`. No camera and no light: shade colours it as any item.
    public ItemVertex flat(ItemVertex v, float2 offset, float angle, float scale, int page = -1)
    {
        MeshVertex c;
        if (!corner(v, c, page))
        {
            v.fold();
            return v;
        }
        float2 xy = rotate(c.position.xy * scale, angle) + offset;
        v.position = v.rest.position + xy;
        v.depth = c.position.z;
        v.uv = c.uv;
        v.local = xy;
        v.world = float3(xy, c.position.z);
        v.normal = float3(0.0, 0.0, -1.0);
        return v;
    }
};
