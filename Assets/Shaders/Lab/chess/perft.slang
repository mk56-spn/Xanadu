// CHESS: perft, the standard check that the rules are right. From a test position it counts every line of legal moves
// `depth` plies long, each root move a workgroup counting its own on the same ply stack the search uses, `budget` steps
// a frame. Published counts to match: the start 20, 400, 8902, 197281, 4865609; Kiwipete 48, 2039, 97862, 4085603;
// position 3 14, 191, 2812, 43238, 674624; position 4 6, 264, 9467, 422333. A wrong castling, en passant, promotion or
// check rule shows as a wrong count.

static const uint PERFT = 3;
/// A finished game's result when it was a count, not a game.
static const uint COUNTED = 4;

/// The test position to count from (0 the start, 1 Kiwipete, 2 and 3 the published positions 3 and 4), taken whenever
/// `perftCount` changes, `depth` plies deep.
[Default(0)] uniform uint perftPosition;
[Default(0)] uniform uint perftCount;

static const uint TEST_POSITIONS[4 * 12] = {
    // the start
    0x42365324u, 0x11111111u, 0x0u, 0x0u, 0x0u, 0x0u, 0x99999999u, 0xCABEDBACu, 0, 15, 64, 15364,
    // Kiwipete: castling, pins, en passant
    0x40060004u, 0x11133111u, 0x90500200u, 0x10090u, 0x21000u, 0x9A900ABu, 0xB9D9909u, 0xC00E000Cu, 0, 15, 64, 15364,
    // position 3: en passant and checks in an endgame
    0x0u, 0x1010000u, 0x0u, 0xE0900040u, 0xC0000016u, 0x9000u, 0x900u, 0x0u, 0, 0, 64, 7968,
    // position 4: promotions and castling under fire
    0x6405004u, 0x11001091u, 0x20000Du, 0x10133u, 0x1Au, 0x2BA000B0u, 0x99909991u, 0xC00E000Cu, 0, 12, 64, 15366,
};

Pos testPosition(uint k)
{
    uint at0 = min(k, 3) * 12;
    Pos p;
    for (uint w = 0; w < 8; w++)
        p.squares[w] = TEST_POSITIONS[at0 + w];
    p.side = TEST_POSITIONS[at0 + 8];
    p.castle = TEST_POSITIONS[at0 + 9];
    p.ep = TEST_POSITIONS[at0 + 10];
    p.kings = TEST_POSITIONS[at0 + 11];
    return p;
}

/// Sets every legal root move of the test position counting.
void startCount()
{
    game.pos = testPosition(perftPosition);
    game.tested = perftCount;
    game.last = 0;
    game.plays = 0;
    game.result = 0;
    uint roots = min(listLegal(game.pos), ROOTS);
    for (uint k = 0; k < roots; k++)
    {
        rootMoves[k] = legal[1 + k];
        searches[k] = idleSearch();
    }
    game.roots = roots;
    game.phase = PERFT;
    game.nodes = 0;
    game.frames = 0;
    game.depth = clamp(depth, 1, PLIES / 2);
}

void startCounting(uint g, uint ply, Pos pos)
{
    Ply f;
    f.pos = pos;
    f.alpha = 0;
    f.beta = 0;
    f.best = 0;
    f.next = 0;
    f.legal = 0;
    f.tactical = 0;
    f.pad = 0;
    f.count = generate(pos, listAt(g, ply), false);
    plies[plyAt(g, ply)] = f;
}

[Pass(1, groups: ROOTS)]
void count(uint g)
{
    if (game.phase != PERFT || g >= game.roots)
        return;
    Search s = searches[g];
    if (s.state == DONE)
        return;
    uint deep = game.depth;
    if (s.state == IDLE)
    {
        s.state = RUNNING;
        s.ply = 0;
        s.nodes = 0;
        if (deep <= 1)
        {
            s.nodes = 1;
            s.state = DONE;
        }
        else
        {
            Pos child = game.pos;
            make(child, rootMoves[g]);
            startCounting(g, 0, child);
        }
    }

    for (uint step = 0; step < budget && s.state == RUNNING; step++)
    {
        Ply f = plies[plyAt(g, s.ply)];
        if (f.next >= f.count)
        {
            if (s.ply == 0)
                s.state = DONE;
            else
                s.ply--;
            continue;
        }
        uint m = listed[listAt(g, s.ply) + f.next];
        f.next++;
        plies[plyAt(g, s.ply)] = f;
        Pos child = f.pos;
        make(child, m);
        if (attacked(child, kingOf(child, f.pos.side), child.side))
            continue;
        // A move from the last ply but one lands on a leaf: count it, no need to go there.
        if (s.ply + 2 >= deep)
        {
            s.nodes++;
            continue;
        }
        s.ply++;
        startCounting(g, s.ply, child);
    }
    searches[g] = s;
}
