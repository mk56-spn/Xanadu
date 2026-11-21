// A tunnel you fly down, in triangles (xanadu.mesh): a long cylinder seen from inside, its
// wall painted with rings that rush past and flash on the beat. Seen from inside a tube
// never overlaps itself, so nothing needs sorting. In a level, swap time for beat.
import xanadu;

[Default(colours.cyan)] uniform colour glow;
[Default(1)] uniform float speed;

static const float LENGTH = 4000;

Camera camera(float scale) { return Camera(10 * scale, 700 * scale); }

/// The tube: along the view (+z) from just ahead of the eye, swaying as it goes.
float3 shaped(float2 uv, float scale)
{
    // The cylinder stands upright; turned a quarter round x it runs into the screen.
    float3 p = Cylinder(260, LENGTH).turned(float3(-PI / 2, 0, 0)).at(uv);
    float z = p.z + LENGTH * 0.5 + 20;                    // 20 px ahead of the eye to LENGTH beyond
    float bend = z / LENGTH;
    p.xy += float2(sin(time * 0.7) * 600, cos(time * 0.5) * 300) * bend * bend;
    return float3(p.xy, z) * scale;
}

ItemVertex deform(ItemVertex v)
{
    float scale = v.rest.size.y / 810;
    v.place(shaped(v.uv, scale), camera(scale));
    return v;
}

float4 shade(ItemInput i)
{
    float along = i.uv.y * LENGTH;                         // px into the tunnel
    float travel = time * 900 * speed;
    float pulse = shaping.impulse(frac(time * 2), 8);      // two hits a second: beats.pulse() in a level

    // Rings round the wall rushing towards you, and stripes along it.
    float ring = frac((along + travel) / 260);
    float rings = 1 - smoothstep(0.02, 0.06, 0.5 - abs(ring - 0.5));
    float stripes = 1 - smoothstep(0.01, 0.03, 0.5 - abs(frac(i.uv.x * 12) - 0.5));
    colour c = colours.black.brightened(0.05);
    c.paint(max(rings, stripes * 0.5), glow.brightened(0.6 + 1.4 * pulse), blend.add);
    // Far away fades into the dark; the near rim fades so the cut doesn't show.
    float fade = (1 - linearstep(LENGTH * 0.2, LENGTH, along)) * linearstep(0, 200, along);
    c.rgb *= fade;
    c.rgb += colours.white.rgb * pulse * 0.08 * fade;
    return c;
}
