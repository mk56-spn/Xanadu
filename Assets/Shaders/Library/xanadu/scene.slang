// xanadu.scene: 3D scenes of solids, composed in code and marched fast. A graph's [Pass] records
// the scene once a frame into two buffers; a [Pixels] pass shoots it, a tile of pixels at a time.
//
//     [Image(rgba16f)] RWTexture2D<float4> shown;
//     [Image(r32f)] RWTexture2D<float> shownDepth;
//     [Buffer(512)] RWStructuredBuffer<scene.Part> parts;       // every solid, as data
//     [Buffer(129)] RWStructuredBuffer<scene.Object> objects;   // 0 is the header, then one per object
//
//     [Pass(1, over: 1)]
//     void record(uint i)
//     {
//         Scene s = Scene(parts, objects);
//         s.add(Block(float3(2600, 1000, 2600)).inverted(), Material(colours.slateGray, 0));   // a room
//         s.add(Block(170, 14).turned(float3(0, beat, 0)).scaled(1 + 0.3 * beats.pulse()), colours.hotPink);
//         s.melt(Orb(55).moved(float3(0, -130, 0)), colours.gold, 40);    // melts into the box
//         s.cut(Orb(50).moved(float3(80, 0, -80)));                      // a bite out of both
//         s.finish();
//     }
//
//     [Pixels(8, 8)]
//     void look(ItemInput i, Canvas canvas)
//     {
//         float depth;
//         shown[group.pixel] = scene.shot(parts, objects, Camera(1100, 1000).orbited(time * 0.2, 0.3), canvas, Lighting(), depth);
//         shownDepth[group.pixel] = depth;
//     }
//
//     void graph() { record(); look(); present(shown, shownDepth); }
//
// add starts a new object; melt, cut and keep work on the object being built (melt: a smooth
// union whose materials blend; cut: carved out, softened by a blend; keep: only what is inside
// stays). Objects are a hard union, each with its own box, and that box is what makes it fast:
// each tile lists the objects it sees and those that can shadow it, each ray marches only
// those whose box it crosses, and shadows and occlusion work an object out fully only where
// its box is near enough to matter. A cut inside an object costs only that object.
//
// Solids are values, placed as mesh surfaces are (moved, turned, scaled), then shaped:
// rounded, shelled, inverted, mirrored, copied. Moves before a mirror or copies place the one
// solid inside them; moves after place the whole set. Space is xanadu.march's, in pixels: x
// right, y down (up is -y), z away from a camera with no orbit.
module scene;
import xanadu.stage;
import xanadu.math;
import xanadu.colour;
import xanadu.mesh;
import xanadu.march;
import xanadu.light;

// ---- Solids --------------------------------------------------------------------------

/// A solid, before it is recorded: its kind and size, where it is, and how it is shaped.
public struct Solid : IPlaced3
{
    /// Where it is: the whole set's place once a mirror or copies is on.
    public Placement3 placement3;
    /// Where the one solid sits inside its mirror or copies.
    public Placement3 inner;
    public uint kind;
    public float4 size;
    public uint flags;
    public float3 spacing;
    public float3 count;
    public float round;
    public float shell;

    public __init(uint kind, float4 size)
    {
        placement3 = Placement3();
        inner = Placement3();
        this.kind = kind;
        this.size = size;
        flags = 0;
        spacing = float3(0.0);
        count = float3(0.0);
        round = 0.0;
        shell = 0.0;
    }

    /// Grown by `by` all round, its edges rounding off.
    public Solid rounded(float by) { Solid s = this; s.round += by; return s; }

    /// Only a skin `thickness` thick round its surface: a hollow shell; cut it open to see in.
    public Solid shelled(float thickness) { Solid s = this; s.shell = thickness; s.flags |= scene.SHELLED; return s; }

    /// Inside out: solid everywhere but inside it. A room is a block inverted.
    public Solid inverted() { Solid s = this; s.flags ^= scene.INVERTED; return s; }

    /// Mirrored across the axes picked (1 mirrors, 0 doesn't): one moved off to a corner and
    /// mirrored in x and z is four corners.
    public Solid mirrored(float3 axes)
    {
        Solid s = domained();
        s.flags |= (axes.x > 0.5 ? scene.MIRROR_X : 0u) | (axes.y > 0.5 ? scene.MIRROR_Y : 0u) | (axes.z > 0.5 ? scene.MIRROR_Z : 0u);
        if ((s.flags & scene.COPIES) != 0)
            s.flags |= scene.MIRROR_FIRST;
        return s;
    }

    /// Copies every `spacing` along each axis, `count` more each way (0 for none on that axis):
    /// count 2 makes 5 in a row, centred.
    public Solid copied(float3 spacing, float3 count)
    {
        Solid s = domained();
        s.flags |= scene.COPIES;
        s.spacing = spacing;
        s.count = count;
        return s;
    }

    /// The same solid with its place moved inside its mirror or copies (the first time), so
    /// moves after place the whole set.
    Solid domained()
    {
        Solid s = this;
        if ((flags & (scene.COPIES | scene.MIRROR_X | scene.MIRROR_Y | scene.MIRROR_Z)) == 0)
        {
            s.inner = placement3;
            s.placement3 = Placement3();
        }
        return s;
    }
};

/// A box `size` across (x, y, z) with its edges rounded by `round`.
public Solid Block(float3 size, float round = 0.0) { return Solid(scene.BLOCK, float4(size, round)); }
/// A cube `size` across.
public Solid Block(float size, float round = 0.0) { return Solid(scene.BLOCK, float4(size, size, size, round)); }
/// A ball `radius` round.
public Solid Orb(float radius) { return Solid(scene.ORB, float4(radius, 0.0, 0.0, 0.0)); }
/// Everything on one side of a flat plane through the origin: `normal` points out of it, up
/// (-y) by default, so it is a floor; move it down to where the floor is.
public Solid Plane(float3 normal = float3(0.0, -1.0, 0.0)) { return Solid(scene.PLANE, float4(normalize(normal), 0.0)); }
/// A standing cylinder: `radius` round, `height` tall along y, centred.
public Solid Pillar(float radius, float height) { return Solid(scene.PILLAR, float4(radius, height, 0.0, 0.0)); }
/// A capsule standing along y: `height` between the middles of its round ends, `radius` round.
public Solid Pill(float radius, float height) { return Solid(scene.PILL, float4(radius, height, 0.0, 0.0)); }
/// A ring lying flat (round y): `radius` to the middle of its tube, `thickness` the tube's radius.
public Solid Hoop(float radius, float thickness) { return Solid(scene.HOOP, float4(radius, thickness, 0.0, 0.0)); }
/// A cone standing on its base, tip up: `radius` across the base, `height` tall, centred.
public Solid Cone(float radius, float height) { return Solid(scene.CONE, float4(radius, height, 0.0, 0.0)); }

// ---- Recording ---------------------------------------------------------------------------

/// The scene being recorded, in a BUILD pass, into the buffers it was given. add starts an
/// object, melt / cut / keep change it, finish closes the last one and writes the count.
public struct Scene
{
    RWStructuredBuffer<scene.Part> parts;
    RWStructuredBuffer<scene.Object> objects;
    uint partCount;
    uint objectCount;
    uint mostParts;
    uint mostObjects;
    scene.Object open;
    bool isOpen;

    public __init(RWStructuredBuffer<scene.Part> parts, RWStructuredBuffer<scene.Object> objects)
    {
        this.parts = parts;
        this.objects = objects;
        uint n, stride;
        parts.GetDimensions(n, stride);
        mostParts = n;
        objects.GetDimensions(n, stride);
        mostObjects = n > 0 ? n - 1 : 0;
        partCount = 0;
        objectCount = 0;
        open = {};
        isOpen = false;
    }

    /// A new object: the solid, in `m`.
    [mutating]
    public void add(Solid solid, Material m)
    {
        close();
        if (objectCount >= mostObjects || partCount >= mostParts)
            return;
        Bounds box = scene.boundsOf(solid);
        open.low = box.low;
        open.high = box.high;
        open.first = partCount;
        open.count = 0;
        isOpen = true;
        put(solid, m, scene.ADD, 0.0);
    }

    [mutating] public void add(Solid solid, colour c) { add(solid, Material(c)); }

    /// The solid melted into the object being built over `blend` pixels; the materials blend too.
    [mutating]
    public void melt(Solid solid, Material m, float blend)
    {
        if (!isOpen)
        {
            add(solid, m);
            return;
        }
        Bounds box = scene.boundsOf(solid).grown(blend);
        open.low = min(open.low, box.low);
        open.high = max(open.high, box.high);
        put(solid, m, scene.MELT, blend);
    }

    [mutating] public void melt(Solid solid, colour c, float blend) { melt(solid, Material(c), blend); }

    /// The solid carved out of the object being built; `blend` softens the cut's edge.
    [mutating] public void cut(Solid solid, float blend = 0.0) { if (isOpen) put(solid, Material(colours.black), scene.CUT, blend); }

    /// Only what is inside the solid kept, of the object being built.
    [mutating] public void keep(Solid solid) { if (isOpen) put(solid, Material(colours.black), scene.KEEP, 0.0); }

    /// Closes the last object and writes how many there are: call it last.
    [mutating]
    public void finish()
    {
        close();
        scene.Object header = {};
        header.first = objectCount;
        header.count = partCount;
        objects[0] = header;
    }

    [mutating]
    void put(Solid solid, Material m, uint op, float blend)
    {
        if (partCount >= mostParts)
            return;
        parts[partCount++] = scene.partOf(solid, m, op, blend);
        open.count++;
    }

    [mutating]
    void close()
    {
        if (!isOpen)
            return;
        objects[1 + objectCount++] = open;
        isOpen = false;
    }
};

public namespace scene
{
    /// Kinds of solid.
    public static const uint BLOCK = 0, ORB = 1, PLANE = 2, PILLAR = 3, PILL = 4, HOOP = 5, CONE = 6;
    /// What a part does to its object (flags bits 0..1).
    public static const uint ADD = 0, MELT = 1, CUT = 2, KEEP = 3;
    /// Its shaping, in the flags.
    public static const uint MIRROR_X = 4, MIRROR_Y = 8, MIRROR_Z = 16, INVERTED = 32, MIRROR_FIRST = 64, COPIES = 128, SHELLED = 256;
    /// Past this a box is everywhere (planes, inverted solids).
    public static const float ENDLESS = 1e19;
    /// Objects a tile lists; past that its pixels go through every object.
    public static const uint LISTED = 256;
    /// Objects one ray marches at once: the nearest-entering, gathered again past them.
    public static const uint RAY = 16;

    /// A solid as recorded: both places as world-to-local matrices, its shape and its material.
    public struct Part
    {
        public float4 outer0, outer1, outer2;
        public float4 inner0, inner1, inner2;
        public float4 size;
        public float4 spacing;          // xyz, w: round
        public float4 count;            // xyz, w: shell
        public float4 tint;
        public uint kind;
        public uint flags;
        public float shine;
        public float glow;
        public float blend;
        public float innerScale;
        public float outerScale;
        public float pad;
    };

    /// An object: its box and its parts (first, count). Object 0 is the header: first is how
    /// many objects follow it. Laid out as bvh.Node is, to build a tree over them one day.
    public struct Object
    {
        public float3 low;
        public uint first;
        public float3 high;
        public uint count;

        public property Bounds box { get { return Bounds(low, high); } }
    };

    /// Where a march met the scene: whether, how far along, where, facing which way, which
    /// object, and the steps it took.
    public struct Shot
    {
        public bool found;
        public float t;
        public float3 position;
        public float3 normal;
        public uint object;
        public uint steps;
    };

    // ---- recording ------------------------------------------------------------------

    /// A placement's world-to-local as three rows (xyz, w the move).
    void rowsOf(Placement3 at, out float4 r0, out float4 r1, out float4 r2)
    {
        float3 c0 = unrotate3(float3(1.0, 0.0, 0.0), at.angles);
        float3 c1 = unrotate3(float3(0.0, 1.0, 0.0), at.angles);
        float3 c2 = unrotate3(float3(0.0, 0.0, 1.0), at.angles);
        float s = 1.0 / at.scale;
        r0 = float4(float3(c0.x, c1.x, c2.x) * s, 0.0);
        r1 = float4(float3(c0.y, c1.y, c2.y) * s, 0.0);
        r2 = float4(float3(c0.z, c1.z, c2.z) * s, 0.0);
        r0.w = -dot(r0.xyz, at.offset);
        r1.w = -dot(r1.xyz, at.offset);
        r2.w = -dot(r2.xyz, at.offset);
    }

    /// A solid as a part, in `m`, doing `op` to its object.
    public Part partOf(Solid solid, Material m, uint op, float blend)
    {
        Part p;
        rowsOf(solid.placement3, p.outer0, p.outer1, p.outer2);
        rowsOf(solid.inner, p.inner0, p.inner1, p.inner2);
        p.size = solid.size;
        p.spacing = float4(solid.spacing, solid.round);
        p.count = float4(solid.count, solid.shell);
        p.tint = m.base;
        p.kind = solid.kind;
        p.flags = solid.flags | op;
        p.shine = m.shine;
        p.glow = m.glow;
        p.blend = blend;
        p.innerScale = solid.inner.scale;
        p.outerScale = solid.placement3.scale;
        p.pad = 0.0;
        return p;
    }

    /// How far a kind reaches from its centre, in its own space.
    float3 reachOf(uint kind, float4 size)
    {
        switch (kind)
        {
        case BLOCK: return size.xyz * 0.5;
        case ORB: return float3(size.x);
        case PILLAR: return float3(size.x, size.y * 0.5, size.x);
        case PILL: return float3(size.x, size.y * 0.5 + size.x, size.x);
        case HOOP: return float3(size.x + size.y, size.y, size.x + size.y);
        case CONE: return float3(size.x, size.y * 0.5, size.x);
        default: return float3(ENDLESS);
        }
    }

    /// A box through a placement: round its eight corners, placed.
    Bounds placed(Bounds b, Placement3 at)
    {
        Bounds r = Bounds(float3(1e30), float3(-1e30));
        for (uint k = 0; k < 8; k++)
        {
            float3 c = float3((k & 1) != 0 ? b.high.x : b.low.x, (k & 2) != 0 ? b.high.y : b.low.y, (k & 4) != 0 ? b.high.z : b.low.z);
            float3 w = at.toWorld(c);
            r.low = min(r.low, w);
            r.high = max(r.high, w);
        }
        return r;
    }

    /// The box round a mirror: each mirrored axis as wide on both sides.
    Bounds mirroredBox(Bounds b, uint flags)
    {
        bool3 on = bool3((flags & MIRROR_X) != 0, (flags & MIRROR_Y) != 0, (flags & MIRROR_Z) != 0);
        float3 most = max(abs(b.low), abs(b.high));
        return Bounds(select(on, -most, b.low), select(on, most, b.high));
    }

    /// The world box round a solid, grown by its rounding and shell: everywhere for a plane
    /// or an inverted solid.
    public Bounds boundsOf(Solid s)
    {
        float3 reach = reachOf(s.kind, s.size) + s.round + s.shell * 0.5;
        if ((s.flags & INVERTED) != 0 || reach.x >= ENDLESS)
            return Bounds(float3(-ENDLESS * 2.0), float3(ENDLESS * 2.0));
        Bounds b = placed(Bounds(-reach, reach), s.inner);
        bool mirrors = (s.flags & (MIRROR_X | MIRROR_Y | MIRROR_Z)) != 0;
        bool mirrorFirst = (s.flags & MIRROR_FIRST) != 0;
        // Undone innermost first: the op applied to p last.
        if (mirrors && !mirrorFirst)
            b = mirroredBox(b, s.flags);
        if ((s.flags & COPIES) != 0)
            b = Bounds(b.low - s.count * s.spacing, b.high + s.count * s.spacing);
        if (mirrors && mirrorFirst)
            b = mirroredBox(b, s.flags);
        return placed(b, s.placement3);
    }

    // ---- distances ------------------------------------------------------------------

    /// How far q, in a kind's own space, is from its surface: negative inside.
    public float kindDistance(uint kind, float4 size, float3 q)
    {
        switch (kind)
        {
        case BLOCK:
        {
            float r = min(size.w, min(size.x, min(size.y, size.z)) * 0.5);
            float3 d = abs(q) - size.xyz * 0.5 + r;
            return length(max(d, 0.0)) + min(max(d.x, max(d.y, d.z)), 0.0) - r;
        }
        case ORB:
            return length(q) - size.x;
        case PLANE:
            return dot(q, size.xyz);
        case PILLAR:
        {
            float2 d = float2(length(q.xz) - size.x, abs(q.y) - size.y * 0.5);
            return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
        }
        case PILL:
        {
            float3 c = q;
            c.y -= clamp(c.y, -size.y * 0.5, size.y * 0.5);
            return length(c) - size.x;
        }
        case HOOP:
            return length(float2(length(q.xz) - size.x, q.y)) - size.y;
        case CONE:
        {
            // Quilez's capped cone with its top radius 0, up as -y.
            float h = size.y * 0.5;
            float2 w = float2(length(q.xz), -q.y);
            float2 k1 = float2(0.0, h);
            float2 k2 = float2(-size.x, 2.0 * h);
            float2 ca = float2(w.x - min(w.x, w.y < 0.0 ? size.x : 0.0), abs(w.y) - h);
            float2 cb = w - k1 + k2 * saturate(dot(k1 - w, k2) / dot(k2, k2));
            float s = cb.x < 0.0 && ca.y < 0.0 ? -1.0 : 1.0;
            return s * sqrt(min(dot(ca, ca), dot(cb, cb)));
        }
        default:
            return 1e30;
        }
    }

    float3 applied(float4 r0, float4 r1, float4 r2, float3 p) { return float3(dot(r0.xyz, p) + r0.w, dot(r1.xyz, p) + r1.w, dot(r2.xyz, p) + r2.w); }

    float3 mirroredPoint(float3 q, uint flags)
    {
        return select(bool3((flags & MIRROR_X) != 0, (flags & MIRROR_Y) != 0, (flags & MIRROR_Z) != 0), abs(q), q);
    }

    float3 copiedPoint(float3 q, float4 spacing, float4 count)
    {
        float3 step = max(spacing.xyz, 1e-5);
        float3 n = clamp(round(q / step), -count.xyz, count.xyz);
        return q - select(spacing.xyz > 0.0, n * step, float3(0.0));
    }

    /// How far world point p is from a part's solid: negative inside.
    public float partDistance(Part a, float3 p)
    {
        float3 q = applied(a.outer0, a.outer1, a.outer2, p);
        bool mirrors = (a.flags & (MIRROR_X | MIRROR_Y | MIRROR_Z)) != 0;
        bool mirrorFirst = (a.flags & MIRROR_FIRST) != 0;
        if (mirrors && mirrorFirst)
            q = mirroredPoint(q, a.flags);
        if ((a.flags & COPIES) != 0)
            q = copiedPoint(q, a.spacing, a.count);
        if (mirrors && !mirrorFirst)
            q = mirroredPoint(q, a.flags);
        float d = kindDistance(a.kind, a.size, applied(a.inner0, a.inner1, a.inner2, q)) * a.innerScale - a.spacing.w;
        if ((a.flags & SHELLED) != 0)
            d = abs(d) - a.count.w * 0.5;
        if ((a.flags & INVERTED) != 0)
            d = -d;
        return d * a.outerScale;
    }

    /// How far world point p is from an object: its parts, in order.
    public float objectDistance(RWStructuredBuffer<Part> parts, Object o, float3 p)
    {
        float d = 1e30;
        for (uint k = o.first; k < o.first + o.count; k++)
        {
            Part a = parts[k];
            float e = partDistance(a, p);
            uint op = a.flags & 3u;
            if (op == ADD)
                d = min(d, e);
            else if (op == MELT)
            {
                float b = max(a.blend, 1e-5);
                float h = saturate(0.5 + 0.5 * (e - d) / b);
                d = lerp(e, d, h) - b * h * (1.0 - h);
            }
            else if (op == CUT)
            {
                if (a.blend <= 0.0)
                    d = max(d, -e);
                else
                {
                    float h = saturate(0.5 - 0.5 * (-e - d) / a.blend);
                    d = lerp(-e, d, h) + a.blend * h * (1.0 - h);
                }
            }
            else
                d = max(d, e);
        }
        return d;
    }

    /// An object's material at p (on its surface): its parts' materials, melts blended.
    public Material objectMaterial(RWStructuredBuffer<Part> parts, Object o, float3 p)
    {
        float d = 1e30;
        Material m = Material(colours.white);
        for (uint k = o.first; k < o.first + o.count; k++)
        {
            Part a = parts[k];
            uint op = a.flags & 3u;
            if (op != ADD && op != MELT)
                continue;
            float e = partDistance(a, p);
            Material here = Material(a.tint, a.shine, a.glow);
            if (op == ADD)
            {
                if (e < d)
                {
                    d = e;
                    m = here;
                }
                continue;
            }
            float b = max(a.blend, 1e-5);
            float h = saturate(0.5 + 0.5 * (e - d) / b);
            m = here.mixed(m, h);
            d = lerp(e, d, h) - b * h * (1.0 - h);
        }
        return m;
    }

    /// How far p is from an object's box: 0 inside, never more than the way to its surface.
    public float boxDistance(Object o, float3 p) { return length(max(max(o.low - p, p - o.high), 0.0)); }

    /// How many objects the scene holds.
    public uint objectCount(RWStructuredBuffer<Object> objects) { return objects[0].first; }

    /// Whether an object's box is everywhere (a plane, an inverted solid).
    bool endless(Object o) { return o.high.x - o.low.x > ENDLESS; }

    // ---- shooting -------------------------------------------------------------------

    groupshared uint seen[LISTED];
    groupshared uint seenCount;
    groupshared uint shadowing[LISTED];
    groupshared uint shadowingCount;

    /// What a tile sees: inside the four planes through the eye and its corners, nearer than `far`.
    public Frustum tileView(Camera camera, Canvas canvas, float far)
    {
        float2 corners[4] = { group.low, float2(group.high.x, group.low.y), group.high, float2(group.low.x, group.high.y) };
        float3 dirs[4];
        Frustum v;
        v.far = far;
        v.eye = camera.eye;
        for (uint k = 0; k < 4; k++)
            dirs[k] = camera.ray((canvas.uvAt(corners[k]) - 0.5) * canvas.size).direction;
        float3 centre = camera.ray((canvas.uvAt((group.low + group.high) * 0.5) - 0.5) * canvas.size).direction;
        for (uint k = 0; k < 4; k++)
        {
            float3 plane = normalize(cross(dirs[k], dirs[(k + 1) & 3]));
            v.planes[k] = dot(plane, centre) < 0.0 ? -plane : plane;
        }
        return v;
    }

    /// Everywhere an object's shadow can fall: its box swept away from the sun and each lamp.
    Bounds shadowBox(Object o, Lighting light)
    {
        Bounds r = o.box;
        if (any(light.sunlight.rgb > 0.0))
        {
            float3 away = -light.sun * light.far;
            r.low = min(r.low, o.low + away);
            r.high = max(r.high, o.high + away);
        }
        for (uint l = 0; l < light.lamps; l++)
        {
            for (uint k = 0; k < 8; k++)
            {
                float3 c = float3((k & 1) != 0 ? o.high.x : o.low.x, (k & 2) != 0 ? o.high.y : o.low.y, (k & 4) != 0 ? o.high.z : o.low.z);
                float3 w = c + normalize(c - light.lampAt[l].xyz + 1e-3) * light.far;
                r.low = min(r.low, w);
                r.high = max(r.high, w);
            }
        }
        return r;
    }

    /// The tile's lists: the objects it sees, and those whose shadows can fall in it. Every
    /// thread of the tile calls it (it waits for them all).
    void listTile(RWStructuredBuffer<Object> objects, Frustum v, Lighting light)
    {
        if (group.index == 0)
        {
            seenCount = 0;
            shadowingCount = 0;
        }
        GroupMemoryBarrierWithGroupSync();
        uint n = objectCount(objects);
        bool shadows = light.shadows > 0.0;
        for (uint k = group.index; k < n; k += group.threads)
        {
            Object o = objects[1 + k];
            bool always = endless(o);
            uint at;
            if (always || v.sees(o.box))
            {
                InterlockedAdd(seenCount, 1, at);
                if (at < LISTED)
                    seen[at] = k;
            }
            if (shadows && (always || v.sees(shadowBox(o, light))))
            {
                InterlockedAdd(shadowingCount, 1, at);
                if (at < LISTED)
                    shadowing[at] = k;
            }
        }
        GroupMemoryBarrierWithGroupSync();
    }

    /// The objects one ray marches at once: the RAY whose boxes it enters first, past `from`.
    struct RayList
    {
        uint id[RAY];
        float enter[RAY];
        float leave[RAY];
        uint kept;
        /// The nearest entry of anything left out: march no further before gathering again.
        float cut;

        [mutating]
        void gather(RWStructuredBuffer<Object> objects, Ray ray, float from, float limit, bool listed)
        {
            kept = 0;
            cut = limit;
            float3 inverse = 1.0 / ray.direction;
            uint n = listed ? seenCount : objectCount(objects);
            for (uint k = 0; k < n; k++)
            {
                uint who = listed ? seen[k] : k;
                Object o = objects[1 + who];
                Span s = endless(o) ? Span(0.0, limit) : o.box.entered(ray, inverse);
                if (s.missed || s.far < from || s.near >= limit)
                    continue;
                add(who, s);
            }
        }

        [mutating]
        void add(uint who, Span s)
        {
            if (kept == RAY)
            {
                if (s.near >= enter[RAY - 1])
                {
                    cut = min(cut, s.near);
                    return;
                }
                cut = min(cut, enter[RAY - 1]);
                kept--;
            }
            int at = int(kept);
            for (; at > 0 && enter[at - 1] > s.near; at--)
            {
                id[at] = id[at - 1];
                enter[at] = enter[at - 1];
                leave[at] = leave[at - 1];
            }
            id[at] = who;
            enter[at] = s.near;
            leave[at] = s.far;
            kept++;
        }

        /// A safe step from t along the ray (p there): how far each object still ahead is,
        /// worked out fully only for those whose box the ray is in or about to enter.
        float distance(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, float3 p, float t, inout uint nearest)
        {
            float d = 1e30;
            for (uint k = 0; k < kept; k++)
            {
                if (t > leave[k])
                    continue;
                float ahead = enter[k] - t;
                if (ahead > 1.0)
                {
                    d = min(d, ahead);
                    continue;
                }
                float e = objectDistance(parts, objects[1 + id[k]], p);
                if (e < d)
                {
                    d = e;
                    nearest = id[k];
                }
            }
            return d;
        }

        /// How far p is from what is near it of the list: for the normal at a hit.
        float near(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, float3 p, float reach)
        {
            float d = 1e30;
            for (uint k = 0; k < kept; k++)
            {
                Object o = objects[1 + id[k]];
                if (boxDistance(o, p) <= reach)
                    d = min(d, objectDistance(parts, o, p));
            }
            return d;
        }
    };

    /// The ray marched through the scene out to `limit`, gathering what it crosses as it goes.
    public Shot marched(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, Ray ray, float limit, bool listed)
    {
        Shot s = {};
        RayList list = {};
        float from = 0.0;
        for (uint round = 0; round < 4; round++)
        {
            list.gather(objects, ray, from, limit, listed);
            if (list.kept == 0)
                break;
            float end = min(list.cut, limit);
            float t = max(from, list.enter[0]);
            for (; s.steps < 256 && t <= end; s.steps++)
            {
                float3 p = ray.at(t);
                uint nearest = 0;
                float d = list.distance(parts, objects, p, t, nearest);
                float close = max(0.05, t * 2e-4);
                if (d < close)
                {
                    s.found = true;
                    s.t = t;
                    s.position = p;
                    s.object = nearest;
                    float e = max(0.5, t * 5e-4);
                    const float2 k = float2(1.0, -1.0);
                    s.normal = normalize(k.xyy * list.near(parts, objects, p + k.xyy * e, 4.0 * e) + k.yyx * list.near(parts, objects, p + k.yyx * e, 4.0 * e)
                                       + k.yxy * list.near(parts, objects, p + k.yxy * e, 4.0 * e) + k.xxx * list.near(parts, objects, p + k.xxx * e, 4.0 * e));
                    return s;
                }
                t += d;
            }
            if (list.cut >= limit || s.steps >= 256)
                break;
            from = list.cut;
        }
        return s;
    }

    /// How much of the light towards `toLight` reaches p, 0..1, looking `far`: soft at
    /// `hardness` (Quilez's penumbra from the closest miss). Only the tile's shadowing objects
    /// whose box the way passes near are asked, and fully only when near enough to darken it.
    public float shadow(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, float3 p, float3 toLight, float far,
        float hardness, bool listed)
    {
        uint ids[RAY];
        uint kept = 0;
        Ray ray = Ray(p, toLight);
        float3 inverse = 1.0 / toLight;
        float margin = far / max(hardness, 1e-3);
        uint n = listed ? shadowingCount : objectCount(objects);
        for (uint k = 0; k < n && kept < RAY; k++)
        {
            uint who = listed ? shadowing[k] : k;
            Object o = objects[1 + who];
            if (!endless(o))
            {
                Span s = o.box.grown(margin).entered(ray, inverse);
                if (s.missed || s.near > far)
                    continue;
            }
            ids[kept++] = who;
        }
        if (kept == 0)
            return 1.0;

        float lit = 1.0;
        float t = 1.0;
        for (uint step = 0; step < 64 && t < far; step++)
        {
            float3 q = ray.at(t);
            float matters = t / max(hardness, 1e-3) + 1.0;   // nearer than this darkens
            float h = 1e30, move = 1e30;
            for (uint k = 0; k < kept; k++)
            {
                Object o = objects[1 + ids[k]];
                float b = boxDistance(o, q);
                if (b > matters)
                {
                    move = min(move, b);
                    continue;
                }
                float e = objectDistance(parts, o, q);
                h = min(h, e);
                move = min(move, e);
            }
            if (h < 0.05)
                return 0.0;
            lit = min(lit, hardness * h / t);
            t += clamp(move, 1.0, 400.0);
        }
        return saturate(lit);
    }

    /// How open p is, 0..1: 1 out in the open, less in creases and corners. Five steps out
    /// along the normal, `reach` pixels in all; an object only counts where its box is nearer
    /// than the step.
    public float occlusion(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, float3 p, float3 normal, float reach, bool listed)
    {
        float closed = 0.0;
        float weight = 1.0;
        uint n = listed ? seenCount : objectCount(objects);
        for (uint i = 1; i <= 5; i++)
        {
            float h = reach * i / 5.0;
            float3 q = p + normal * h;
            float d = h;
            for (uint k = 0; k < n; k++)
            {
                Object o = objects[1 + (listed ? seen[k] : k)];
                if (boxDistance(o, q) < d)
                    d = min(d, objectDistance(parts, o, q));
            }
            closed += (h - d) / reach * weight;
            weight *= 0.6;
        }
        return saturate(1.0 - closed * 1.5);
    }

    /// A recorded scene as a shadow caster, for Lighting.lit and deferred: its soft shadows and
    /// its occlusion, asked of the tile's lists when `listed`.
    public struct Shadows : IShadows
    {
        RWStructuredBuffer<Part> parts;
        RWStructuredBuffer<Object> objects;
        bool listed;

        public __init(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, bool listed)
        {
            this.parts = parts;
            this.objects = objects;
            this.listed = listed;
        }

        public float shadow(float3 p, float3 toLight, float far, float hardness)
        {
            return scene.shadow(parts, objects, p, toLight, far, hardness, listed);
        }

        public float open(float3 p, float3 normal, float reach) { return occlusion(parts, objects, p, normal, reach, listed); }
    };

    /// The scene casting shadows on this pixel pass's tile, whoever drew what is there: lists
    /// the tile first, so every thread of the tile must call it.
    public Shadows shadows(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, Camera camera, Canvas canvas, Lighting light)
    {
        listTile(objects, tileView(camera, canvas, light.far), light);
        return Shadows(parts, objects, seenCount <= LISTED && shadowingCount <= LISTED);
    }

    /// A hit lit: Lighting.lit with the scene's own shadows and occlusion.
    public colour lit(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, Shot hit, Ray ray, Camera camera, Lighting light, bool listed)
    {
        Surface s = Surface(hit.normal, objectMaterial(parts, objects[1 + hit.object], hit.position));
        return light.lit(s, hit.position, camera, Shadows(parts, objects, listed));
    }

    /// The scene through `camera` into this pixel pass's Surfaces, out to `light.far` but only
    /// where it is nearer than what is there already: draw cheap things first and the march
    /// stops at them, so pixels they cover cost little. Lists the tile, so every thread of the
    /// tile must call it. Light them after with Lighting.deferred.
    public void shoot(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, Camera camera, Canvas canvas, Lighting light, Surfaces into)
    {
        listTile(objects, tileView(camera, canvas, light.far), light);
        bool listed = seenCount <= LISTED && shadowingCount <= LISTED;
        float drawn = into.depthAt(group.pixel);
        float limit = drawn < Surfaces.EMPTY * 0.5 ? min(light.far, camera.along(canvas.centred, drawn)) : light.far;
        Shot hit = marched(parts, objects, camera.ray(canvas.centred), limit, listed);
        if (!hit.found)
            return;
        Surface s = Surface(hit.normal, objectMaterial(parts, objects[1 + hit.object], hit.position));
        s.depth = camera.screenDepth(hit.position);
        into.put(group.pixel, s);
    }

    /// The heat scale the cost views use: dark, red, yellow, white.
    float3 heat(float x)
    {
        x = saturate(x);
        return saturate(float3(1.5 * x, 2.2 * x - 0.6, 3.0 * x - 2.0) + float3(0.05, 0.04, 0.12) * (1.0 - x));
    }

    /// What `shot` shows: the picture, or what it cost.
    public static const uint LIT = 0, STEPS = 1, TILE_LISTS = 2, NORMALS = 3;

    /// The scene through `camera` at this [Pixels] pass's pixel, in `light`: the tile lists what
    /// it sees and what can shadow it, the ray marches what it crosses, the hit is lit. `depth`
    /// is how far into the screen it is, for present (1e6 for the background; 0 where nothing
    /// shows). Every thread of the tile must call it. `view` shows the cost instead:
    /// scene.STEPS, scene.TILE_LISTS (red where a list overflowed), scene.NORMALS.
    public colour shot(RWStructuredBuffer<Part> parts, RWStructuredBuffer<Object> objects, Camera camera, Canvas canvas, Lighting light,
        out float depth, uint view = LIT)
    {
        depth = 0.0;
        listTile(objects, tileView(camera, canvas, light.far), light);
        bool listed = seenCount <= LISTED && shadowingCount <= LISTED;

        Ray ray = camera.ray(canvas.centred);
        Shot hit = marched(parts, objects, ray, light.far, listed);
        if (view == STEPS)
            return colour(heat(hit.steps / 128.0), 1.0);
        if (view == TILE_LISTS)
            return colour(!listed ? float3(0.9, 0.12, 0.2) : heat(max(seenCount, shadowingCount) / 64.0), 1.0);
        if (!hit.found)
        {
            if (light.background.a > 0.0)
                depth = 1e6;
            return light.background;
        }
        depth = camera.screenDepth(hit.position);
        if (view == NORMALS)
            return colour(hit.normal * 0.5 + 0.5, 1.0);
        return lit(parts, objects, hit, ray, camera, light, listed);
    }
}
