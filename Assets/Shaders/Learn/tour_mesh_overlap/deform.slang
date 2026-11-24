import xanadu;

[Grid(192, 108)]
ItemVertex deform(ItemVertex v)
{
    Piece piece = mesh.split(v.uv, COUNT, CELLS);
    float scale = v.rest.size.y / 810;
    v.place(shaped(piece.index, piece.uv) * scale, camera(scale));
    return v;
}
