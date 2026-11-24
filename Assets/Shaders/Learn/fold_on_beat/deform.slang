import xanadu;

[Grid(96, 54)]
ItemVertex deform(ItemVertex v)
{
    float s = v.rest.size.y / 810;
    v.place(shaped(v.uv, s), camera(s));
    return v;
}
