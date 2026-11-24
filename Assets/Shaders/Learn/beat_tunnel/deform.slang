import xanadu;

[Grid(96, 54)]
ItemVertex deform(ItemVertex v)
{
    float scale = v.rest.size.y / 810;
    v.place(shaped(v.uv, scale), camera(scale));
    return v;
}
