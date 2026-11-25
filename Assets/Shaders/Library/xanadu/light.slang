// xanadu.light: the shared scene. Every maker (a mesh draw, a march, anything) writes only
// how deep the nearest thing is and whose it is: Visibility, a depth image and an id image
// (makers.sdf, makers.mesh). One pass then reads both back at each pixel (Seen), rebuilds the
// surface there from its maker (scene.surface for a recorded scene, Seen.onMesh for a pool
// triangle) and lights it once, whoever made it, so a mesh drawn into a marched room gets the
// room's lamp, shadows and occlusion.
//
//     [Image(r32f, clear: 1e30)] RWTexture2D<float> depth;    // pixels into the screen; empty is Visibility.EMPTY
//     [Image(r32ui)]             RWTexture2D<uint>  ids;      // whose: makers.NONE where nothing is
//     [Image(rgba16f)]           RWTexture2D<float4> shown;
//
//     Visibility seen() { return Visibility(depth, ids); }
//
//     [Draw(64, 1, shade: ringSeen, copies: PAGES)]    // a pool draw writes depth and ids
//     ItemVertex ringDrawn(ItemVertex v) { return mesh.pulled(v, placed(), eye(v.rest.size), corners, triangles, meshlets); }
//     IdTexels ringSeen(ItemInput i) { return Visibility.texels(i.depth, makers.mesh(copy, drawnTriangle)); }
//
//     [Pixels(8, 8)]
//     void illuminate(ItemInput i, Canvas canvas)
//     {
//         Camera camera = eye(canvas.size);
//         Seen s = seen().at(group.pixel, camera, canvas);
//         Surface here = Surface(s.onMesh(placed(), corners, triangles).normal, colours.orange);
//         shown[group.pixel] = Lighting().deferred(s, here, camera, Unshadowed());
//     }
//
//     void graph()
//     {
//         ringDrawn(depth, ids);                         // an image for each field of IdTexels
//         illuminate();
//         present(shown, depth);
//     }
//
// Shadows and occlusion come from an IShadows: Unshadowed for none, scene.shadows(...) for a
// recorded scene's soft SDF shadows, cast on everything seen.
module light;
import xanadu.stage;
import xanadu.math;
import xanadu.colour;
import xanadu.mesh;
import xanadu.march;

// ---- Materials --------------------------------------------------------------------------

/// What a surface is made of: its colour, how tight its highlight is (0 none), how much light
/// of its own it gives (0 none, 1 its colour at full whatever the light, more for HDR).
public struct Material
{
    public colour base;
    public float shine;
    public float glow;

    public __init(colour base, float shine = 30.0, float glow = 0.0) { this.base = base; this.shine = shine; this.glow = glow; }

    /// A copy giving `amount` light of its own.
    public Material glowing(float amount) { Material m = this; m.glow = amount; return m; }

    /// Between this one and `other`, `t` of the way.
    public Material mixed(Material other, float t) { return Material(lerp(base, other.base, t), lerp(shine, other.shine, t), lerp(glow, other.glow, t)); }
};

// ---- The light ---------------------------------------------------------------------------

/// The light a scene is shot in: a sun, up to four lamps, the sky's light from above and the
/// ground's from below, and what shows where nothing is. A closed room keeps the sun out.
public struct Lighting
{
    /// Towards the sun (normalised).
    public float3 sun;
    /// The sun's colour; black turns it off.
    public colour sunlight;
    /// Each lamp's place (xyz) and how far it reaches before it is down to half (w).
    public float4 lampAt[4];
    public colour lampLight[4];
    public uint lamps;
    public colour sky;
    public colour ground;
    /// What a ray that meets nothing shows: transparent lets what is behind the item show.
    public colour background;
    /// How sharp shadows are: higher is harder; 0 turns them off.
    public float shadows;
    /// How dark creases and corners get (ambient occlusion), 0 for none.
    public float occlusion;
    /// Fog from `fog.x` to `fog.y` away from the camera, fading to the background; 0, 0 for none.
    public float2 fog;
    /// How far rays and shadows look.
    public float far;

    public __init()
    {
        sun = normalize(float3(-0.5, -0.8, -0.6));
        sunlight = colour(1.0, 0.95, 0.85);
        for (uint k = 0; k < 4; k++)
        {
            lampAt[k] = float4(0.0);
            lampLight[k] = colours.black;
        }
        lamps = 0;
        sky = colour(0.4, 0.5, 0.65);
        ground = colour(0.2, 0.16, 0.12);
        background = colours.transparent;
        shadows = 16.0;
        occlusion = 1.0;
        fog = float2(0.0);
        far = 8000.0;
    }

    /// Which light IShadows.shadow is asked about: the sun, or lamp k as LAMP + k.
    public static const uint SUN = 0, LAMP = 1;

    /// A copy with the sun towards `direction`.
    public Lighting sunFrom(float3 direction) { Lighting l = this; l.sun = normalize(direction); return l; }

    /// A copy without the sun.
    public Lighting sunless() { Lighting l = this; l.sunlight = colours.black; return l; }

    /// A copy with one more lamp at `position`, in `light`, reaching `reach` (past four, ignored).
    public Lighting lamp(float3 position, colour light, float reach = 1000.0)
    {
        Lighting l = this;
        if (l.lamps < 4)
        {
            l.lampAt[l.lamps] = float4(position, reach);
            l.lampLight[l.lamps] = light;
            l.lamps++;
        }
        return l;
    }

    /// A copy fogged from `near` to `far` towards `into` (which becomes the background too).
    public Lighting fogged(float near, float far, colour into) { Lighting l = this; l.fog = float2(near, far); l.background = into; return l; }
};


// ---- What a pixel shows --------------------------------------------------------------------

/// What a pixel shows before it is lit, as its maker rebuilt it: which way it faces (world
/// space, out of it) and what it is made of.
public struct Surface
{
    public float3 normal;
    public Material material;

    /// Something facing `normal`, made of `m`.
    public __init(float3 normal, Material m)
    {
        this.normal = normalize(normal);
        material = m;
    }

    public __init(float3 normal, colour c) { this = Surface(normal, Material(c)); }

    /// A copy facing the eye, whichever side it was seen from: for open sheets lit on both sides.
    public Surface facing(float3 toEye) { Surface s = this; if (dot(s.normal, toEye) < 0.0) s.normal = -s.normal; return s; }
};

/// Whose a pixel is, as an id image holds it: the maker in the top two bits, then which of
/// its things (a recorded scene's object, a pool's triangle). 0 is nothing.
/// A pool page's size, out here: inside makers, `mesh` is makers.mesh.
static const uint POOL_PAGE = mesh.PAGE;

public namespace makers
{
    public static const uint NONE = 0, SDF = 1, MESH = 2;

    /// A recorded scene's object (scene.Object, counted from 0).
    public uint sdf(uint object) { return (SDF << 30) | object; }

    /// A mesh pool's triangle: its page (a pool draw's `copy`) and its number there (`drawnTriangle`).
    public uint mesh(uint page, uint triangle) { return (MESH << 30) | (page * POOL_PAGE + triangle); }

    /// Which maker an id is: NONE, SDF or MESH.
    public uint kind(uint id) { return id >> 30; }

    /// Which of the maker's things it is.
    public uint index(uint id) { return id & 0x3FFFFFFFu; }
}

/// What a draw's shade returns to write Visibility's two images (Visibility.texels).
public struct IdTexels
{
    public float depth;
    public uint id;
};

/// One pixel of Visibility, read back: whether anything is there, how deep, whose, and where
/// in the world (on the ray through the pixel).
public struct Seen
{
    public bool found;
    public float depth;
    public uint id;
    public Ray ray;
    public float3 position;

    /// Its maker: makers.NONE, SDF or MESH.
    public property uint maker { get { return makers.kind(id); } }
};

/// Where a pool triangle was seen: its normal (world space, out of its front) and uv there.
public struct MeshPoint
{
    public float3 normal;
    public float2 uv;
};

public extension Seen
{
    /// The pool triangle this pixel shows (a makers.mesh id), placed by `placed` as its draw
    /// placed it: the ray meets the triangle, and its corners' normals and uvs are blended there.
    public MeshPoint onMesh(Placement3 placed, RWStructuredBuffer<MeshVertex> corners, RWStructuredBuffer<uint> triangles)
    {
        uint n = makers.index(id);
        uint base = n - n % mesh.PAGE;
        uint packed = triangles[n];
        MeshVertex a = corners[base + (packed & 0xFFu)];
        MeshVertex b = corners[base + ((packed >> 8) & 0xFFu)];
        MeshVertex c = corners[base + ((packed >> 16) & 0xFFu)];
        float3 pa = placed.toWorld(a.position), pb = placed.toWorld(b.position), pc = placed.toWorld(c.position);
        // Möller-Trumbore: where the ray crosses the triangle's plane, in its two edges.
        float3 e1 = pb - pa, e2 = pc - pa;
        float3 q = cross(ray.direction, e2);
        float det = dot(e1, q);
        float3 from = ray.origin - pa;
        float inv = abs(det) > 1e-12 ? 1.0 / det : 0.0;
        float u = saturate(dot(from, q) * inv);
        float v = saturate(dot(ray.direction, cross(from, e1)) * inv);
        float w = max(1.0 - u - v, 0.0);
        MeshPoint m;
        m.normal = normalize(rotate3(a.normal * w + b.normal * u + c.normal * v, placed.angles));
        m.uv = a.uv * w + b.uv * u + c.uv * v;
        return m;
    }
}

/// The shared scene's images, the graph's own: `depth` (r32f, pixels into the screen, cleared
/// to EMPTY) and `ids` (r32ui, whose). A maker's draw writes both (texels); a pixel pass puts
/// and reads them at its pixel.
public struct Visibility
{
    /// What a cleared depth holds: nothing there. Declare the depth image cleared to it: `clear: 1e30`.
    public static const float EMPTY = 1e30;

    public RWTexture2D<float> depth;
    public RWTexture2D<uint> ids;

    public __init(RWTexture2D<float> depth, RWTexture2D<uint> ids)
    {
        this.depth = depth;
        this.ids = ids;
    }

    /// How deep the nearest thing at `pixel` is: EMPTY where nothing is.
    public float depthAt(uint2 pixel) { return depth[pixel]; }

    /// `id` put at `pixel`, `depth` deep, over whatever was there: test depthAt first to keep the nearest.
    public void put(uint2 pixel, float depth, uint id)
    {
        this.depth[pixel] = depth;
        ids[pixel] = id;
    }

    /// What a draw's shade returns to write both images: `depth` (the draw's `i.depth`) and `id`.
    public static IdTexels texels(float depth, uint id)
    {
        IdTexels t;
        t.depth = depth;
        t.id = id;
        return t;
    }

    /// The pixel read back, seen through `camera` (a pixel pass's own pixel and canvas).
    public Seen at(uint2 pixel, Camera camera, Canvas canvas)
    {
        Seen s;
        s.depth = depth[pixel];
        s.id = ids[pixel];
        s.found = s.depth < EMPTY * 0.5 && s.id != makers.NONE;
        s.ray = camera.ray(canvas.centred);
        s.position = s.ray.at(camera.along(canvas.centred, s.found ? s.depth : 0.0));
        return s;
    }
};

// ---- Shadows ------------------------------------------------------------------------------

/// Where shadows and occlusion come from, for the lighting.
public interface IShadows
{
    /// How much of light `light` (Lighting.SUN, or Lighting.LAMP + k for lamp k), towards `toLight`,
    /// reaches p, 0..1, looking `far`; soft at `hardness`.
    public float shadow(uint light, float3 p, float3 toLight, float far, float hardness);

    /// How open p is, facing `normal`, 0..1: 1 in the open, less in creases. `reach` px out.
    public float open(float3 p, float3 normal, float reach);
};

/// No shadows and no occlusion: every light reaches everything.
public struct Unshadowed : IShadows
{
    public float shadow(uint light, float3 p, float3 toLight, float far, float hardness) { return 1.0; }
    public float open(float3 p, float3 normal, float reach) { return 1.0; }
};

/// Two casters at once (made by both(a, b)): a scene's soft SDF shadows and a lamp's shadow map
/// of the meshes, so every maker shadows every other. Light is what both let through; open is the less open.
public struct BothShadows<A : IShadows, B : IShadows> : IShadows
{
    public A a;
    public B b;

    public __init(A a, B b) { this.a = a; this.b = b; }

    public float shadow(uint light, float3 p, float3 toLight, float far, float hardness)
    {
        float lit = a.shadow(light, p, toLight, far, hardness);
        return lit > 0.0 ? lit * b.shadow(light, p, toLight, far, hardness) : 0.0;
    }

    public float open(float3 p, float3 normal, float reach) { return min(a.open(p, normal, reach), b.open(p, normal, reach)); }
};

/// `a` and `b` casting together (BothShadows): `both(scene.shadows(...), ShadowMap(...))`.
public BothShadows<A, B> both<A : IShadows, B : IShadows>(A a, B b) { return BothShadows<A, B>(a, b); }

/// One light's shadow map: how deep the nearest drawn thing is as `camera` (at the light,
/// looking at the origin) sees it, in an image the item's box's size, drawn by any maker's draw
/// through that camera. Darkens only light `light`; no occlusion of its own.
///
///     [Image(r32f, clear: 1e30)] RWTexture2D<float> lampDepth;
///     [Draw(64, 1, shade: fromLamp, copies: PAGES)]
///     ItemVertex seenByLamp(ItemVertex v) { return mesh.pulled(v, placed(), lampEye(v.rest.size), corners, triangles, meshlets); }
///     float fromLamp(ItemInput i) { return i.depth; }
///     // graph(): seenByLamp(lampDepth);   lighting: ShadowMap(Lighting.LAMP, lampDepth, lampEye(canvas.size), canvas.size)
public struct ShadowMap : IShadows
{
    public uint light;
    public RWTexture2D<float> depth;
    public Camera camera;
    public float2 size;

    public __init(uint light, RWTexture2D<float> depth, Camera camera, float2 size)
    {
        this.light = light;
        this.depth = depth;
        this.camera = camera;
        this.size = size;
    }

    public float shadow(uint asked, float3 p, float3 toLight, float far, float hardness)
    {
        if (asked != light)
            return 1.0;
        float3 v = camera.viewed(p);
        if (v.z < 1.0)
            return 1.0;
        float2 at = size * 0.5 + v.xy * camera.focal / v.z;
        float deep = v.z - camera.distance;
        // Two of the map's texels at this distance, so a surface never shadows itself.
        float bias = 2.0 + 2.0 * v.z / max(camera.focal, 1.0);
        // Nine taps a texel apart: a soft edge rather than the map's stairs.
        float lit = 0.0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                float2 q = at + float2(x, y);
                if (any(q < 0.0) || any(q >= size))
                    lit += 1.0;
                else
                    lit += depth[uint2(q)] < deep - bias ? 0.0 : 1.0;
            }
        return lit / 9.0;
    }

    public float open(float3 p, float3 normal, float reach) { return 1.0; }
};

// ---- Lighting a surface -------------------------------------------------------------------

public extension Lighting
{
    /// The light one source gives: diffuse and highlight, `amount` of it.
    float3 lightFrom(float3 n, float3 toLight, float3 toEye, Material m, float3 light, float amount)
    {
        float3 rgb = m.base.rgb * amount;
        if (m.shine > 0.0)
            rgb += lights.specular(n, toLight, toEye, m.shine) * amount * 0.4;
        return light * rgb;
    }

    /// `s`, at world `position`, lit: the sun and each lamp (shadowed), the sky from above and the
    /// ground from below (occluded by `caster`), its own glow, and fog; seen through `camera`.
    public colour lit<S : IShadows>(Surface s, float3 position, Camera camera, S caster)
    {
        Material m = s.material;
        float3 n = s.normal;
        float away = length(position - camera.eye);
        float3 toEye = (camera.eye - position) / max(away, 1e-3);
        float3 base = m.base.rgb;
        float3 from = position + n * max(1.5, away * 1e-3);
        float3 rgb = float3(0.0);
        if (any(sunlight.rgb > 0.0))
        {
            float sunny = lights.diffuse(n, sun);
            if (shadows > 0.0 && sunny > 0.0)
                sunny *= caster.shadow(SUN, from, sun, far, shadows);
            rgb += lightFrom(n, sun, toEye, m, sunlight.rgb, sunny);
        }
        for (uint l = 0; l < lamps; l++)
        {
            float3 to = lampAt[l].xyz - position;
            float gap = length(to);
            float3 toLamp = to / max(gap, 1e-3);
            float fall = gap / max(lampAt[l].w, 1.0);
            float lamp = lights.diffuse(n, toLamp) / (1.0 + fall * fall);
            if (shadows > 0.0 && lamp > 1e-3)
                lamp *= caster.shadow(LAMP + l, from, toLamp, gap, shadows);
            rgb += lightFrom(n, toLamp, toEye, m, lampLight[l].rgb, lamp);
        }
        float openness = occlusion > 0.0 ? lerp(1.0, caster.open(position, n, 60.0), occlusion) : 1.0;
        float above = saturate(0.5 - 0.5 * n.y);   // up is -y
        rgb += base * (sky.rgb * above + ground.rgb * (1.0 - above)) * openness * 0.6;
        rgb += base * m.glow;
        colour c = colour(rgb, 1.0);
        if (fog.y > fog.x)
            c = lerp(c, background, camera.fog(position, fog.x, fog.y));
        return c;
    }

    /// What a pixel shows: `here`, the surface its maker rebuilt at `seen`, lit through `camera`;
    /// the background where nothing is. Every thread of the tile reaches it, so `caster` may be
    /// one that lists the tile (scene.shadows).
    public colour deferred<S : IShadows>(Seen seen, Surface here, Camera camera, S caster)
    {
        if (!seen.found)
            return background;
        return lit(here, seen.position, camera, caster);
    }
}
