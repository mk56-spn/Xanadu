// xanadu.light: what every 3D producer and the lighting agree on. A producer (a draw, a march)
// writes Surfaces, one Surface a pixel: how deep, facing which way, made of what. The lighting
// reads them back and lights each once, whoever wrote it, so a mesh drawn into a marched room
// gets the room's lamp, shadows and occlusion.
//
//     // build.slang
//     [Image(r32f, clear: 1e30)] RWTexture2D<float>  depth;     // pixels into the screen; empty is Surfaces.EMPTY
//     [Image(rgba16f)]           RWTexture2D<float4> normals;   // xyz which way it faces, w its shine
//     [Image(rgba16f)]           RWTexture2D<float4> bases;     // rgb its colour, a its glow
//     [Image(rgba16f)]           RWTexture2D<float4> shown;
//
//     Surfaces surfaces() { return Surfaces(depth, normals, bases); }
//
//     [Draw(96, 32, shade: ringLook)]                  // a draw whose shade gives a Surface's texels
//     ItemVertex ringDrawn(ItemVertex v) { v.place(ring().at(v.uv), eye(v.rest.size)); return v; }
//     SurfaceTexels ringLook(ItemInput i) { return Surface(ring().normalAt(i.uv), colours.orange).texels(i.depth); }
//
//     [Pixels(8, 8)]
//     void illuminate(ItemInput i, Canvas canvas)
//     {
//         shown[group.pixel] = Lighting().deferred(surfaces(), eye(canvas.size), canvas, Unshadowed());
//     }
//
//     void graph()
//     {
//         ringDrawn(depth, normals, bases);              // an image for each field of SurfaceTexels
//         illuminate();
//         present(shown, depth);
//     }
//
// Shadows and occlusion come from an IShadows: Unshadowed for none, scene.shadows(...) for a
// recorded scene's soft SDF shadows, cast on everything in Surfaces.
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


// ---- Surfaces ----------------------------------------------------------------------------

/// What one pixel shows before it is lit: whether anything is there, how far into the screen
/// (pixels, as ItemVertex.depth), which way it faces (world space, out of it), and its material.
public struct Surface
{
    public bool found;
    public float depth;
    public float3 normal;
    public Material material;

    /// Nothing: the pixel stays empty.
    public __init()
    {
        found = false;
        depth = Surfaces.EMPTY;
        normal = float3(0.0, 0.0, -1.0);
        material = Material(colours.black, 0.0);
    }

    /// Something facing `normal`, made of `m`. A draw's depth is filled in by the draw.
    public __init(float3 normal, Material m)
    {
        found = true;
        depth = 0.0;
        this.normal = normalize(normal);
        material = m;
    }

    public __init(float3 normal, colour c) { this = Surface(normal, Material(c)); }

    /// A copy facing the eye, whichever side it was seen from: for open sheets lit on both sides.
    public Surface facing(float3 toEye) { Surface s = this; if (dot(s.normal, toEye) < 0.0) s.normal = -s.normal; return s; }

    /// What a draw's shade returns to write it into Surfaces' three images, `depth` (the draw's
    /// `i.depth`) in front. Nothing found leaves the pixel alone: a draw's shade only, since it
    /// discards.
    public SurfaceTexels texels(float depth)
    {
        if (!found)
            discard;
        SurfaceTexels t;
        t.depth = depth;
        t.normal = Surfaces.normalTexel(this);
        t.base = Surfaces.baseTexel(this);
        return t;
    }
};

/// A Surface as Surfaces holds it, one field an image: what a draw writes (`Surface.texels`).
public struct SurfaceTexels
{
    public float depth;
    public float4 normal;
    public float4 base;
};

/// The images Surfaces live in, the graph's own: `depth` (r32f), `normals` (rgba16f: xyz the
/// normal, w the shine) and `bases` (rgba16f or rgba8: rgb the colour, a the glow). A Surface
/// draw writes all three; a pixel pass puts and reads them at its pixel.
public struct Surfaces
{
    /// What a cleared depth holds: nothing there. Declare the depth image cleared to it: `clear: 1e30`.
    public static const float EMPTY = 1e30;

    public RWTexture2D<float> depth;
    public RWTexture2D<float4> normals;
    public RWTexture2D<float4> bases;

    public __init(RWTexture2D<float> depth, RWTexture2D<float4> normals, RWTexture2D<float4> bases)
    {
        this.depth = depth;
        this.normals = normals;
        this.bases = bases;
    }

    /// How deep the surface at `pixel` is: EMPTY where nothing is.
    public float depthAt(uint2 pixel) { return depth[pixel]; }

    /// The surface at `pixel`.
    public Surface at(uint2 pixel)
    {
        Surface s = Surface();
        s.depth = depth[pixel];
        if (s.depth >= EMPTY * 0.5)
            return s;
        float4 n = normals[pixel];
        float4 b = bases[pixel];
        s.found = true;
        s.normal = n.xyz;
        s.material = Material(colour(b.rgb, 1.0), n.w, b.a);
        return s;
    }

    /// `s` put at `pixel`, over whatever was there: test depthAt first to keep the nearest.
    public void put(uint2 pixel, Surface s)
    {
        depth[pixel] = s.depth;
        normals[pixel] = normalTexel(s);
        bases[pixel] = baseTexel(s);
    }

    /// What a surface's normals texel holds.
    public static float4 normalTexel(Surface s) { return float4(s.normal, s.material.shine); }

    /// What a surface's bases texel holds.
    public static float4 baseTexel(Surface s) { return float4(s.material.base.rgb, s.material.glow); }
};

// ---- Shadows ------------------------------------------------------------------------------

/// Where shadows and occlusion come from, for the lighting.
public interface IShadows
{
    /// How much of the light towards `toLight` reaches p, 0..1, looking `far`; soft at `hardness`.
    public float shadow(float3 p, float3 toLight, float far, float hardness);

    /// How open p is, facing `normal`, 0..1: 1 in the open, less in creases. `reach` px out.
    public float open(float3 p, float3 normal, float reach);
};

/// No shadows and no occlusion: every light reaches everything.
public struct Unshadowed : IShadows
{
    public float shadow(float3 p, float3 toLight, float far, float hardness) { return 1.0; }
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
                sunny *= caster.shadow(from, sun, far, shadows);
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
                lamp *= caster.shadow(from, toLamp, gap, shadows);
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

    /// This pixel's surface (a pixel pass's, at group.pixel) lit, seen through `camera`; the
    /// background where nothing is. Every thread of the tile reaches it, so `shadows` may be
    /// a caster that lists the tile (scene.shadows).
    public colour deferred<S : IShadows>(Surfaces from, Camera camera, Canvas canvas, S caster)
    {
        Surface s = from.at(group.pixel);
        if (!s.found)
            return background;
        Ray ray = camera.ray(canvas.centred);
        return lit(s, ray.at(camera.along(canvas.centred, s.depth)), camera, caster);
    }
}
