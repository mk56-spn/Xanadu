import xanadu;

[Grid(192, 108)]
ItemVertex deform(ItemVertex v)
{
    float s = v.rest.size.y / 810;
    v.place(TorusKnot(240, 96, 2, 3).tube(36).turned(float3(0.25, 0, 0)).at(v.uv) * s, camera(s));
    return v;
}
