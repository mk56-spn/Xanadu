// xanadu.particles: the pieces a particle system is made of, as plain functions over a
// particle's place and velocity (pixels from the middle, y down, and px/s), so a graph
// pass only says which ones it runs and in what order:
//
//     v = particles.forced(v, float2(0, 400));         // gravity
//     v = particles.dragged(v, 0.5);
//     v = particles.attracted(p, v, float2(0, 0), 3000, 0, 80);
//     particles.bounce(p, v, -world / 2, world / 2, bool4(true), 0.5, 0.1);
//
// Where a birth lands on an emitter's shape and which way it leaves (particles.onShape,
// particles.launched), the moves a frame makes (forces, drag, a curl-noise flow,
// attractors, vortices, a pusher, a field's slope, Physarum steering, walls, speed
// limits), and the looks a particle is drawn with (particles.cover). The moves read `dt`
// (the frame's seconds) and `time` themselves. Lab/particles runs all of them.
module particles;
import xanadu.math;
import xanadu.stage;
import xanadu.shape;
import xanadu.noise;

public namespace particles
{
    // ---- where births land ---------------------------------------------------------

    /// An emitter's shapes, for particles.onShape: a point, a line `size.x` long, a circle and a
    /// disc of radius `size.x`, a box `size` across, and a ring from radius `size.x` out to `size.y`.
    public static const uint POINT = 0;
    public static const uint LINE = 1;
    public static const uint CIRCLE = 2;
    public static const uint DISC = 3;
    public static const uint BOX = 4;
    public static const uint RING = 5;

    /// A point on an emitter's shape, round its middle, before it is turned and placed;
    /// `u` and `v` are two 0..1 randoms, spread evenly over the shape's area.
    public float2 onShape(uint shape, float2 size, float u, float v)
    {
        switch (shape)
        {
        case LINE:
            return float2((u - 0.5) * size.x, 0);
        case CIRCLE:
            return fromPolar(size.x, u * TAU);
        case DISC:
            return fromPolar(size.x * sqrt(v), u * TAU);
        case BOX:
            return (float2(u, v) - 0.5) * size;
        case RING:
            return fromPolar(lerp(size.x, max(size.x, size.y), sqrt(v)), u * TAU);
        default:
            return float2(0, 0);
        }
    }

    /// The velocity a birth leaves with: `speed` along `heading` (radians, 0 right, -PI/2 up),
    /// bent `outward` (0..1) away from the shape's middle at `local`, and the share `tangent`
    /// of it sent round the middle instead (orbits, spirals; negative turns the other way).
    public float2 launched(float2 local, float heading, float outward, float speed, float tangent)
    {
        float2 direction = fromPolar(1, heading);
        float2 out = length(local) > 1e-3 ? normalize(local) : direction;
        direction = normalize(lerp(direction, out, outward) + 1e-6);
        return direction * speed * (1 - abs(tangent)) + float2(-out.y, out.x) * speed * tangent;
    }

    // ---- moves: each gives the velocity after a frame ------------------------------

    /// A steady push: `accel` px/s² (gravity, wind, buoyancy).
    public float2 forced(float2 velocity, float2 accel) { return velocity + accel * dt; }

    /// Slowed down: `linear` is the share of the speed lost a second (air), `quadratic` more
    /// loss the faster it goes (per 100 px/s).
    public float2 dragged(float2 velocity, float linear, float quadratic = 0.0)
    {
        return velocity * exp(-(linear + quadratic * length(velocity) * 0.01) * dt);
    }

    /// The way a swirling curl-noise flow runs at `position`: `scale` px across an eddy,
    /// `evolve` how fast it changes in place. Roughly unit length.
    public float2 flowAt(float2 position, float scale, float evolve)
    {
        return noise.curl(position / max(scale, 1), time * evolve);
    }

    /// Pushed along the flow (particles.flowAt) by `strength` px/s².
    public float2 flowed(float2 position, float2 velocity, float strength, float scale, float evolve)
    {
        return velocity + flowAt(position, scale, evolve) * strength * dt;
    }

    /// Carried by the flow at `speed` px/s, taking its speed at `grip` a second.
    public float2 carried(float2 position, float2 velocity, float speed, float scale, float evolve, float grip)
    {
        return damped(velocity, flowAt(position, scale, evolve) * speed, max(grip, 0.01), dt);
    }

    /// Pulled towards `at` by `strength` px/s² (negative pushes away), easing off over `core`
    /// px near it; nothing past `reach` px (0 reaches everywhere).
    public float2 attracted(float2 position, float2 velocity, float2 at, float strength, float reach, float core)
    {
        float2 to = at - position;
        float r = length(to);
        if (reach > 0 && r >= reach)
            return velocity;
        return velocity + to / max(r, 1) * strength / (1 + (r * r) / max(core * core, 1)) * dt;
    }

    /// Spun round `at` by `swirl` px/s² (negative the other way) and pulled in by `pull`,
    /// both fading out to nothing at `reach` px (0 reaches everywhere).
    public float2 swirled(float2 position, float2 velocity, float2 at, float swirl, float reach, float pull)
    {
        float2 to = at - position;
        float r = length(to);
        float near = reach > 0 ? saturate(1 - r / reach) : 1;
        float2 dir = to / max(r, 1);
        return velocity + (float2(-dir.y, dir.x) * swirl + dir * pull) * near * dt;
    }

    /// The ways particles.pushed pushes: away from the point, towards it, or round it.
    public static const uint AWAY = 0;
    public static const uint TOWARDS = 1;
    public static const uint ROUND = 2;

    /// Pushed by a point (a pointer, a passing body) `strength` px/s² right under it, falling
    /// off to nothing at `reach` px, AWAY, TOWARDS or ROUND it.
    public float2 pushed(float2 position, float2 velocity, float2 at, float strength, float reach, uint way)
    {
        float2 to = at - position;
        float r = length(to);
        float near = saturate(1 - r / max(reach, 1));
        float2 dir = to / max(r, 1);
        float2 push = way == AWAY ? -dir : way == TOWARDS ? dir : float2(-dir.y, dir.x);
        return velocity + push * strength * near * near * dt;
    }

    /// Physarum's turn: a particle heading `heading` (radians) that sensed `ahead`, `left` and
    /// `right` (a field `angle` either side) turns `turn` radians a second towards the most,
    /// plus `wander` radians a second of random turning; `coin` (0..1, a fresh random) breaks
    /// a tie and `jitter` (0..1, another) wanders. Gives the new heading.
    public float steered(float heading, float ahead, float left, float right, float turn, float wander, float coin, float jitter)
    {
        float by = turn * dt;
        if (ahead >= left && ahead >= right)
            by = 0;
        else if (left > right)
            by = -by;
        else if (left == right)
            by *= coin < 0.5 ? -1 : 1;
        return heading + by + (jitter - 0.5) * wander * dt;
    }

    /// Kept between `least` and `most` px/s.
    public float2 limited(float2 velocity, float least, float most)
    {
        float s = length(velocity);
        return s > 1e-4 ? velocity * clamp(s, least, max(least, most)) / s : velocity;
    }

    // ---- walls ---------------------------------------------------------------------

    /// Which of the walls `low`..`high` a position is past: x left, y right, z top, w bottom,
    /// only those `walls` names.
    public bool4 pastWalls(float2 position, float2 low, float2 high, bool4 walls)
    {
        return bool4(position.x < low.x && walls.x, position.x > high.x && walls.y,
                     position.y < low.y && walls.z, position.y > high.y && walls.w);
    }

    /// Bounces off the walls it is past: back inside, keeping `keep` of its speed into the
    /// wall and losing `friction` of its speed along it.
    public void bounce(inout float2 position, inout float2 velocity, float2 low, float2 high, bool4 walls, float keep, float friction)
    {
        bool4 past = pastWalls(position, low, high, walls);
        for (int k = 0; k < 2; k++)
        {
            if (!past[2 * k] && !past[2 * k + 1])
                continue;
            position[k] = clamp(position[k], low[k], high[k]);
            velocity[k] = -velocity[k] * keep;
            velocity[1 - k] *= 1 - friction;
        }
    }

    /// Wraps round: past a wall, it comes in at the other.
    public void wrap(inout float2 position, float2 low, float2 high, bool4 walls)
    {
        bool4 past = pastWalls(position, low, high, walls);
        for (int k = 0; k < 2; k++)
        {
            if (past[2 * k])
                position[k] = high[k];
            else if (past[2 * k + 1])
                position[k] = low[k];
        }
    }

    // ---- looks ---------------------------------------------------------------------

    /// How a particle is drawn, for particles.cover: a soft core with a halo, a plain dot,
    /// a ring, a four-pointed spark, a square. Each stretches along the velocity when asked.
    public static const uint GLOW = 0;
    public static const uint DOT = 1;
    public static const uint RINGED = 2;
    public static const uint SPARK = 3;
    public static const uint SQUARE = 4;

    /// How far past its spine a look of `radius` reaches: the half height of the quad it needs.
    public float reachOf(uint look, float radius) { return look == GLOW ? radius * 2.5 : radius + 1; }

    /// How much of a particle covers `local` (px from its middle, x along its velocity): a
    /// capsule `halfLength` either side of the middle and `radius` round it (a circle when
    /// still), drawn as `look`. 0..1.
    public float cover(uint look, float2 local, float halfLength, float radius)
    {
        float r = radius;
        float2 q = float2(local.x - clamp(local.x, -halfLength, halfLength), local.y);
        float d = length(q) - r;
        switch (look)
        {
        case GLOW:
            return saturate(sdf.filled(d + r * 0.5, 1.5) * 0.8 + sdf.haloed(d + r, r * 2.5, 2.5));
        case DOT:
            return sdf.filled(d, 1.2);
        case RINGED:
            return sdf.stroked(d + r * 0.2, max(r * 0.3, 1), 1.2);
        case SPARK:
        {
            float2 s = abs(q) / max(r, 0.5);
            return saturate(exp(-s.x * 6) * exp(-s.y) + exp(-s.y * 6) * exp(-s.x) + sdf.filled(d + r * 0.7, 1));
        }
        default:
            return sdf.filled(max(abs(local.x) - halfLength, abs(local.y)) - r, 1.2);
        }
    }
}
