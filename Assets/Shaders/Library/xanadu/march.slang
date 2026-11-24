// xanadu.march: 3D by marching a ray through a distance field. The other 3D path to
// xanadu.mesh: a mesh bends a grid to a surface it can write as uv → point; a march
// finds a surface it can only measure (how far is p from it?), such as balls melting
// into each other. Same camera, same lights, same pixels.
//
//     float4 shade(Canvas canvas)
//     {
//         Camera camera = Camera(650);
//         Ray ray = camera.ray(canvas.centred);               // through this pixel
//         Ball ball = Ball(float3(0, 0, 0), 80);
//         Span inside = ball.grown(10).entered(ray);           // where the ray is near it
//         if (inside.missed)
//             return colours.transparent;                     // nothing to march
//         Hit hit = ray.marched(Cloud(), inside);              // Cloud: an IField of yours
//         return hit.found ? hit.lit(camera) : colours.transparent;
//     }
//
// A field is an IField: sampled(p) says how far p is from the surface, which way is out
// (the normal) and its colour there. Melt builds one from balls: add each, and the
// smooth min, its normal and its colours come out together, so the march needs no extra
// samples for the normal. Space is in pixels, as the mesh's: x right, y down, z ahead.
//
// Marching costs per pixel and per step, so march only where something is: draw a quad
// round each thing (camera.outlined in deform) and march only inside it (entered). See
// Assets/Shaders/Lab/raymarch_pool.
module march;
import xanadu.math;
import xanadu.colour;
import xanadu.mesh;

/// A ray: where it starts and the way it goes (normalised).
public struct Ray
{
    public float3 origin;
    public float3 direction;

    public __init(float3 origin, float3 direction)
    {
        this.origin = origin;
        this.direction = direction;
    }

    /// The point `t` along it.
    public float3 at(float t) { return origin + direction * t; }

    /// Marched through `field` within `span`: stepped by the distance the field gives until it
    /// is within `close` of the surface (found), past the span's far end, or out of `steps`.
    public Hit marched<F : IField>(F field, Span span, int steps = 48, float close = 0.05)
    {
        Hit hit = {};
        float t = span.near;
        for (int n = 0; n < steps; n++)
        {
            Sample s = field.sampled(at(t));
            if (s.distance < close)
            {
                hit.found = true;
                hit.t = t;
                hit.position = at(t);
                hit.normal = normalize(s.normal);
                hit.tint = s.tint;
                return hit;
            }

            t += s.distance;
            if (t > span.far)
                break;
        }

        return hit;
    }
};

/// Where along a ray something is: from `near` to `far`; missed when the ray never gets there.
public struct Span
{
    public float near;
    public float far;

    public __init(float near, float far)
    {
        this.near = near;
        this.far = far;
    }

    public property bool missed { get { return far < near; } }

    /// Both together: from the nearer start to the farther end.
    public Span joined(Span other) { return Span(min(near, other.near), max(far, other.far)); }
};

/// A ball: its centre and radius.
public struct Ball
{
    public float3 centre;
    public float radius;

    public __init(float3 centre, float radius)
    {
        this.centre = centre;
        this.radius = radius;
    }

    /// From one float4: the centre in xyz, the radius in w.
    public __init(float4 ball)
    {
        centre = ball.xyz;
        radius = ball.w;
    }

    /// The same ball, `by` bigger all round: what a melt can reach.
    public Ball grown(float by) { return Ball(centre, radius + by); }

    /// How far p is from its surface (negative inside).
    public float distance(float3 p) { return length(p - centre) - radius; }

    /// Where `ray` is inside it, from the eye on (a ray starting inside enters at 0); missed
    /// when it passes by.
    public Span entered(Ray ray)
    {
        float3 to = centre - ray.origin;
        float along = dot(ray.direction, to);
        float h = along * along - dot(to, to) + radius * radius;
        if (h < 0.0)
            return Span(1.0, 0.0);
        float half = sqrt(h);
        return Span(max(along - half, 0.0), along + half);
    }
};

/// A box along the axes, from its `low` corner to its `high` one: what a BVH node keeps round
/// the balls under it.
public struct Bounds
{
    public float3 low;
    public float3 high;

    public __init(float3 low, float3 high)
    {
        this.low = low;
        this.high = high;
    }

    /// The same box, `by` bigger on every side.
    public Bounds grown(float by) { return Bounds(low - by, high + by); }

    /// Where `ray` is inside it, from where it starts on; missed when it passes by. `inverse` is
    /// 1 / ray.direction, worked out once a ray when it crosses many boxes (a BVH walk).
    public Span entered(Ray ray, float3 inverse)
    {
        float3 a = (low - ray.origin) * inverse, b = (high - ray.origin) * inverse;
        float3 near = min(a, b), far = max(a, b);
        return Span(max(max(max(near.x, near.y), near.z), 0.0), min(min(far.x, far.y), far.z));
    }

    /// Where `ray` is inside it; see the other entered for crossing many.
    public Span entered(Ray ray) { return entered(ray, 1.0 / ray.direction); }
};

/// What a field says at a point: how far the surface is, which way is out, its colour there.
public struct Sample
{
    public float distance;
    public float3 normal;
    public colour tint;
};

/// Anything a ray can be marched through.
public interface IField
{
    public Sample sampled(float3 p);
};

/// Where a march met the surface: whether it did, how far along, where, facing which way, in which colour.
public struct Hit
{
    public bool found;
    public float t;
    public float3 position;
    public float3 normal;
    public colour tint;

    /// The point as the mesh's lights read one.
    public property SurfacePoint point { get { SurfacePoint s; s.position = position; s.normal = normal; return s; } }

    /// Its colour lit as a mesh surface is (lights.studio), seen from the camera.
    public colour lit(Camera camera, float shininess = 40.0) { return lights.studio(point, camera.eye, tint, shininess); }
};

/// Balls melted into each other over `blend`, built one at a time at one point: Quilez's
/// polynomial smooth min of their distances. Its gradient (the normal) is the two sides'
/// gradients mixed by the same weight the distances are, and so are the colours, so a melt
/// shades smoothly from one ball's colour to the next's. Add every ball, then read sample.
public struct Melt
{
    public float blend;
    public Sample sample;
    bool _started = false;

    public __init(float blend) { this.blend = max(blend, 1e-5); sample = {}; }

    /// Melts one more ball in, as seen at p, in `tint`.
    [mutating]
    public void add(Ball ball, float3 p, colour tint)
    {
        float3 to = p - ball.centre;
        float far = max(length(to), 1e-6);
        float d = far - ball.radius;
        float3 normal = to / far;
        if (!_started)
        {
            _started = true;
            sample.distance = d;
            sample.normal = normal;
            sample.tint = tint;
            return;
        }

        float h = saturate(0.5 + 0.5 * (d - sample.distance) / blend);
        sample.normal = lerp(normal, sample.normal, h);
        sample.tint = lerp(tint, sample.tint, h);
        sample.distance = lerp(d, sample.distance, h) - blend * h * (1.0 - h);
    }
};

/// What a ball takes on screen, as a box: from `low` to `high`, in pixels from the quad's centre.
public struct Footprint
{
    public float2 low;
    public float2 high;

    /// The point at `uv` across it (0..1): a quad's corner, from its own uv.
    public float2 at(float2 uv) { return lerp(low, high, uv); }
};

public extension Camera
{
    /// The ray from the eye through a point on screen, in pixels from the quad's centre.
    public Ray ray(float2 pixel)
    {
        float3 d = normalize(float3(pixel, focal));
        // The camera's own space back to the world: the turns viewed makes, undone.
        d = rotate3(d, float3(-pitch, 0.0, 0.0));
        d = rotate3(d, float3(0.0, yaw, 0.0));
        return Ray(eye, d);
    }

    /// The exact box a ball takes on screen: across each axis, the two planes through the eye
    /// that just touch it. Draw a quad there (in deform) and march only inside it.
    public Footprint outlined(Ball ball)
    {
        float3 c = viewed(ball.centre);
        float r2 = ball.radius * ball.radius;
        float under = max(c.z * c.z - r2, 1e-4);
        float2 half = ball.radius * sqrt(max(c.xy * c.xy + c.z * c.z - r2, 0.0));
        Footprint o;
        o.low = focal * (c.xy * c.z - half) / under;
        o.high = focal * (c.xy * c.z + half) / under;
        return o;
    }

    /// How far ahead of the camera a ball's nearest point is: its depth to draw at.
    public float depth(Ball ball) { return depth(ball.centre) - ball.radius; }
};
