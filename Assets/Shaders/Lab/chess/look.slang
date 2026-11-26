// CHESS: the board, painted straight from the game the GPU keeps: the squares, the last move, the square the player
// picked and where its piece may go, and a king in check. The pieces are the host's text over it.

/// The square the player picked (64 for none), and whether the board shows black at the bottom.
[Default(64)] uniform uint selected;
[Default(0)] uniform uint flipped;

float4 shade(ItemInput i)
{
    float2 cells = i.uv * 8.0;
    uint file = min(uint(cells.x), 7), rank = 7 - min(uint(cells.y), 7);
    if (flipped != 0)
    {
        file = 7 - file;
        rank = 7 - rank;
    }
    uint s = rank * 8 + file;
    float2 inCell = frac(cells) - 0.5;

    bool light = ((file + rank) & 1) != 0;
    float3 c = light ? float3(0.86, 0.80, 0.69) : float3(0.47, 0.58, 0.34);
    Game g = game;
    if (g.plays > 0 && (s == moveFrom(g.last) || s == moveTo(g.last)))
        c = lerp(c, float3(0.95, 0.85, 0.25), 0.45);
    if (s == selected)
        c = lerp(c, float3(0.30, 0.55, 0.95), 0.55);

    // A king in check glows red.
    uint piece = at(g.pos, s);
    if (g.phase != OVER || g.result != STALEMATE)
        if ((piece & 7) == KING && ((piece & BLACK) != 0) == (g.pos.side == 1) && inCheck(g.pos))
            c = lerp(c, float3(0.9, 0.15, 0.1), saturate(1.0 - length(inCell) * 1.6));

    // Where the picked piece may go: a dot on an empty square, a ring round a capture.
    if (g.phase == PLAYER && selected < 64)
    {
        for (uint k = 0; k < legal[0]; k++)
        {
            uint m = legal[1 + k];
            if (moveFrom(m) != selected || moveTo(m) != s)
                continue;
            float d = length(inCell);
            float mark = piece == 0 ? 1.0 - smoothstep(0.12, 0.14, d) : smoothstep(0.40, 0.42, d) * (1.0 - smoothstep(0.48, 0.5, d));
            c = lerp(c, float3(0.12, 0.16, 0.12), mark * 0.45);
            break;
        }
    }
    return float4(c, 1.0);
}
