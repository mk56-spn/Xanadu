import xanadu;

[Grid(96, 54)]
ItemVertex deform(ItemVertex v)
{
    Piece piece = mesh.split(v.uv, COUNT);
    float s = v.rest.size.y / 810;
    v.place(shaped(piece.index, piece.uv) * s, camera(s));
    return v;
}
