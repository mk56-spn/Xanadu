// Library tour: colour spaces and maps, row by row.
import xanadu;

// Rows: hsv vs oklch rainbow, rgb lerp vs oklab mix, colour maps, blackbody, tonemaps on an HDR ramp.
float4 shade(ItemInput i, Canvas canvas)
{
    float x = i.uv.x;
    int row = int(i.uv.y * 10);
    switch (row)
    {
        case 0: return colours.hsv(x, 1, 1);
        case 1: return colours.oklch(0.75, 0.15, x);
        case 2: return lerp(colours.blue, colours.yellow, x);
        case 3: return colours.blue.mixed(colours.yellow, x);
        case 4: return x < 0.25 ? colours.viridis(x * 4) : x < 0.5 ? colours.inferno(x * 4 - 1) : x < 0.75 ? colours.magma(x * 4 - 2) : colours.plasma(x * 4 - 3);
        case 5: return colours.turbo(x);
        case 6: return colours.blackbody(lerp(1000, 12000, x));
        case 7: { colour hdr = colour(float3(1, 0.45, 0.15) * exp2(lerp(-4, 6, frac(x * 3)))); int k = int(x * 3); return k == 0 ? tonemap.reinhard(hdr) : k == 1 ? tonemap.aces(hdr) : tonemap.agx(hdr); }
        case 8: { colour hdr = colour(float3(0.2, 0.5, 1) * exp2(lerp(-4, 6, frac(x * 3)))); int k = int(x * 3); return k == 0 ? tonemap.filmic(hdr) : k == 1 ? tonemap.acesFitted(hdr) : tonemap.reinhard(hdr, 8); }
        default: { colour c = colours.hsl(x, 0.8, 0.5).posterized(4).exposed(sin(time)).warmed(0.5); float3 back = c.hsv(); return colours.hsv(back.x, back.y, back.z) + c.hsl().x * 0 + c.oklch().x * 0 + c.linearised().encoded() * 0; }
    }
}
