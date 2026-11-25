// Once a frame, one thread bounds-checks every sphere against the tiles: `tiles[0]` is what
// shade reads, kept on the GPU.
import xanadu;

struct Tiles
{
    float4 spheres[SPHERES];
    uint sets[TILES * WORDS];   // each tile's spheres, a bit apiece: tile t's in sets[t * WORDS ..]
};

[Size(1)] List<Tiles> tiles;

[Pass(1, over: 1)]
void bin(uint unused)
{
    Tiles s;
    for (int t = 0; t < TILES; t++)
    {
        for (int w = 0; w < WORDS; w++)
            s.sets[t * WORDS + w] = 0;
    }

    for (int i = 0; i < SPHERES; i++)
    {
        float4 b = sphereAt(i, time);
        s.spheres[i] = b;

        // Every tile edge is a plane through the eye, so a tile is the space between four
        // planes: a sphere can show in it if it reaches over each of them. Grown by BLEND,
        // since a melted neighbour bends the surface that far out.
        float reach = b.w + BLEND;
        for (int row = 0; row < GRID; row++)
        {
            // The outer tiles have no outer edge: they run on to the stage's edge, whatever its shape.
            if (row > 0 && past(b.xyz, edge(row, 1.0), 1) < -reach)
                continue;
            if (row < GRID - 1 && past(b.xyz, edge(row + 1, 1.0), 1) > reach)
                continue;
            for (int col = 0; col < GRID; col++)
            {
                if (col > 0 && past(b.xyz, edge(col, ASPECT), 0) < -reach)
                    continue;
                if (col < GRID - 1 && past(b.xyz, edge(col + 1, ASPECT), 0) > reach)
                    continue;

                int tile = row * GRID + col;
                s.sets[tile * WORDS + i / 32] |= 1u << (i % 32);
            }
        }
    }
    tiles[0] = s;
}

void graph()
{
    bin();
}
