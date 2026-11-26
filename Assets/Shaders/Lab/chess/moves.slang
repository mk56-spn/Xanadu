// CHESS: listing a position's moves. Pseudo-legal: a move that leaves its own king attacked is listed and turned away
// when played (`legalIn`). `tactical` lists only captures and queenings, for the search past its depth.

/// Moves a position can list: no real position has more than 218.
static const uint MOST_MOVES = 224;

void add(uint at0, inout uint n, uint m)
{
    if (n < MOST_MOVES)
        listed[at0 + n] = m;
    n++;
}

/// A pawn reaching the last rank: always a queen; the others too unless only tactics are listed.
void promotions(uint at0, inout uint n, uint from, uint to, uint taken, bool tactical)
{
    add(at0, n, moveOf(from, to, QUEEN, 0, taken, PAWN));
    if (tactical)
        return;
    add(at0, n, moveOf(from, to, KNIGHT, 0, taken, PAWN));
    add(at0, n, moveOf(from, to, ROOK, 0, taken, PAWN));
    add(at0, n, moveOf(from, to, BISHOP, 0, taken, PAWN));
}

void pawnMoves(Pos p, uint s, uint at0, inout uint n, bool tactical)
{
    uint colour = p.side * BLACK;
    int forward = p.side == 0 ? 1 : -1;
    uint lastRank = p.side == 0 ? 7 : 0, startRank = p.side == 0 ? 1 : 6;
    uint one = stepFrom(s, 0, forward);
    if (one != NO_SQUARE && at(p, one) == 0)
    {
        if ((one >> 3) == lastRank)
            promotions(at0, n, s, one, 0, tactical);
        else if (!tactical)
        {
            add(at0, n, moveOf(s, one, 0, 0, 0, PAWN));
            uint two = stepFrom(s, 0, forward * 2);
            if ((s >> 3) == startRank && at(p, two) == 0)
                add(at0, n, moveOf(s, two, 0, DOUBLE, 0, PAWN));
        }
    }
    for (int df = -1; df <= 1; df += 2)
    {
        uint to = stepFrom(s, df, forward);
        if (to == NO_SQUARE)
            continue;
        uint target = at(p, to);
        if (target != 0 && (target & BLACK) != colour)
        {
            if ((to >> 3) == lastRank)
                promotions(at0, n, s, to, target & 7, tactical);
            else
                add(at0, n, moveOf(s, to, 0, 0, target & 7, PAWN));
        }
        else if (to == p.ep)
            add(at0, n, moveOf(s, to, 0, EN_PASSANT, PAWN, PAWN));
    }
}

/// Castling for the side to move: rights kept, the squares between empty, the king never crossing an attacked square.
void castlings(Pos p, uint at0, inout uint n)
{
    uint home = p.side == 0 ? 0 : 56, them = p.side ^ 1;
    uint shortRight = p.side == 0 ? 1 : 4, longRight = p.side == 0 ? 2 : 8;
    uint king = home + 4;
    if ((p.castle & (shortRight | longRight)) == 0 || attacked(p, king, them))
        return;
    if ((p.castle & shortRight) != 0 && at(p, home + 5) == 0 && at(p, home + 6) == 0
        && !attacked(p, home + 5, them) && !attacked(p, home + 6, them))
        add(at0, n, moveOf(king, home + 6, 0, CASTLE, 0, KING));
    if ((p.castle & longRight) != 0 && at(p, home + 1) == 0 && at(p, home + 2) == 0 && at(p, home + 3) == 0
        && !attacked(p, home + 3, them) && !attacked(p, home + 2, them))
        add(at0, n, moveOf(king, home + 2, 0, CASTLE, 0, KING));
}

/// Lists `p`'s moves into listed[at0 ..]; how many (at most MOST_MOVES).
uint generate(Pos p, uint at0, bool tactical)
{
    uint n = 0;
    uint colour = p.side * BLACK;
    for (uint s = 0; s < 64; s++)
    {
        uint piece = at(p, s);
        if (piece == 0 || (piece & BLACK) != colour)
            continue;
        uint kind = piece & 7;
        if (kind == PAWN)
        {
            pawnMoves(p, s, at0, n, tactical);
            continue;
        }
        if (kind == KNIGHT || kind == KING)
        {
            for (uint k = 0; k < 8; k++)
            {
                int2 d = kind == KNIGHT ? KNIGHT_STEPS[k] : WAYS[k];
                uint to = stepFrom(s, d.x, d.y);
                if (to == NO_SQUARE)
                    continue;
                uint target = at(p, to);
                if ((target != 0 && (target & BLACK) == colour) || (tactical && target == 0))
                    continue;
                add(at0, n, moveOf(s, to, 0, 0, target & 7, kind));
            }
            continue;
        }
        uint first = kind == BISHOP ? 4 : 0, last = kind == ROOK ? 4 : 8;
        for (uint w = first; w < last; w++)
        {
            uint to = stepFrom(s, WAYS[w].x, WAYS[w].y);
            while (to != NO_SQUARE)
            {
                uint target = at(p, to);
                if (target != 0)
                {
                    if ((target & BLACK) != colour)
                        add(at0, n, moveOf(s, to, 0, 0, target & 7, kind));
                    break;
                }
                if (!tactical)
                    add(at0, n, moveOf(s, to, 0, 0, 0, kind));
                to = stepFrom(to, WAYS[w].x, WAYS[w].y);
            }
        }
    }
    if (!tactical)
        castlings(p, at0, n);
    return min(n, MOST_MOVES);
}
