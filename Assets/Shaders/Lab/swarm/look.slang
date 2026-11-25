// Each dot a copy of a quad round it, a soft disc in its own hue.
import xanadu;

[Draw(1, 1, shade: dotLook, copies: DOTS)]
ItemVertex dot(ItemVertex v)
{
    Dot d = dots[copy];
    float2 offset = (v.uv - 0.5) * d.size;
    v.position = v.rest.position + d.position + offset;
    v.local = offset;
    v.colour = colours.hsv(d.hue, 0.6, 1);
    return v;
}

float4 dotLook(ItemInput i)
{
    float cover = Circle(dots[copy].size / 2).filled(i.local, 1);
    return float4(i.colour.rgb, 0.85 * cover);
}
