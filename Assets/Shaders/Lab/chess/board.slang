// Chess on the GPU (Labs' CHESS): the rules, the search and the game live in this stack; the host only shows the
// board it reports and sends the player's clicks. This file: a position, its squares, what attacks a square and how a
// move changes it. Squares run a1 = 0 to h8 = 63, rank by rank; a piece is its kind (1 pawn .. 6 king) plus 8 when black.
import xanadu;

static const uint PAWN = 1, KNIGHT = 2, BISHOP = 3, ROOK = 4, QUEEN = 5, KING = 6, BLACK = 8;
static const uint NO_SQUARE = 64;

/// A move: from, to, the kind a pawn becomes, how it is special, what it takes and what moves (for ordering).
static const uint DOUBLE = 1, EN_PASSANT = 2, CASTLE = 3;
uint moveFrom(uint m) { return m & 63; }
uint moveTo(uint m) { return (m >> 6) & 63; }
uint movePromo(uint m) { return (m >> 12) & 7; }
uint moveFlag(uint m) { return (m >> 16) & 3; }
uint moveTaken(uint m) { return (m >> 20) & 7; }
uint moveMover(uint m) { return (m >> 24) & 7; }
uint moveOf(uint from, uint to, uint promo, uint flag, uint taken, uint mover)
{
    return from | (to << 6) | (promo << 12) | (flag << 16) | (taken << 20) | (mover << 24);
}

struct Pos
{
    uint squares[8];    // 4 bits a square, 8 squares a uint
    uint side;          // 0 white to move, 1 black
    uint castle;        // 1 white short, 2 white long, 4 black short, 8 black long
    uint ep;            // the square a pawn just passed over, or NO_SQUARE
    uint kings;         // the white king's square, the black king's << 8
};

uint at(Pos p, uint s) { return (p.squares[s >> 3] >> ((s & 7) * 4)) & 15; }

void put(inout Pos p, uint s, uint piece)
{
    uint shift = (s & 7) * 4;
    p.squares[s >> 3] = (p.squares[s >> 3] & ~(15u << shift)) | (piece << shift);
}

uint kingOf(Pos p, uint side) { return (p.kings >> (side * 8)) & 255; }

/// The square `df` files and `dr` ranks from `s`, or NO_SQUARE off the board.
uint stepFrom(uint s, int df, int dr)
{
    int f = int(s & 7) + df, r = int(s >> 3) + dr;
    return f < 0 || f > 7 || r < 0 || r > 7 ? NO_SQUARE : uint(r * 8 + f);
}

static const int2 KNIGHT_STEPS[8] = { int2(1, 2), int2(2, 1), int2(2, -1), int2(1, -2), int2(-1, -2), int2(-2, -1), int2(-2, 1), int2(-1, 2) };
/// The rook's four ways first, then the bishop's: a king and queen take all eight.
static const int2 WAYS[8] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1), int2(1, 1), int2(1, -1), int2(-1, 1), int2(-1, -1) };

/// Whether side `by` (0 white, 1 black) attacks square `s`.
bool attacked(Pos p, uint s, uint by)
{
    uint colour = by * BLACK;
    // A pawn attacks forwards: a white one from the rank below.
    int back = by == 0 ? -1 : 1;
    for (int df = -1; df <= 1; df += 2)
    {
        uint from = stepFrom(s, df, back);
        if (from != NO_SQUARE && at(p, from) == (colour | PAWN))
            return true;
    }
    for (uint k = 0; k < 8; k++)
    {
        uint from = stepFrom(s, KNIGHT_STEPS[k].x, KNIGHT_STEPS[k].y);
        if (from != NO_SQUARE && at(p, from) == (colour | KNIGHT))
            return true;
        from = stepFrom(s, WAYS[k].x, WAYS[k].y);
        if (from != NO_SQUARE && at(p, from) == (colour | KING))
            return true;
    }
    for (uint w = 0; w < 8; w++)
    {
        uint slider = w < 4 ? ROOK : BISHOP;
        uint from = stepFrom(s, WAYS[w].x, WAYS[w].y);
        while (from != NO_SQUARE)
        {
            uint piece = at(p, from);
            if (piece != 0)
            {
                if ((piece & BLACK) == colour && ((piece & 7) == slider || (piece & 7) == QUEEN))
                    return true;
                break;
            }
            from = stepFrom(from, WAYS[w].x, WAYS[w].y);
        }
    }
    return false;
}

bool inCheck(Pos p) { return attacked(p, kingOf(p, p.side), p.side ^ 1); }

/// The castling rights a move off or onto `s` leaves: a king or rook leaving home, or a rook taken there.
uint rightsKept(uint s)
{
    switch (s)
    {
    case 0: return ~2u;
    case 4: return ~3u;
    case 7: return ~1u;
    case 56: return ~8u;
    case 60: return ~12u;
    case 63: return ~4u;
    default: return ~0u;
    }
}

/// Plays `m` on `p`, legal or not: the caller checks the mover's king afterwards.
void make(inout Pos p, uint m)
{
    uint from = moveFrom(m), to = moveTo(m), flag = moveFlag(m);
    uint piece = at(p, from);
    uint colour = p.side * BLACK;
    put(p, to, movePromo(m) != 0 ? (movePromo(m) | colour) : piece);
    put(p, from, 0);
    if (flag == EN_PASSANT)
        put(p, p.side == 0 ? to - 8 : to + 8, 0);
    if (flag == CASTLE)
    {
        bool shortSide = to > from;
        put(p, shortSide ? from + 1 : from - 1, colour | ROOK);
        put(p, shortSide ? from + 3 : from - 4, 0);
    }
    if ((piece & 7) == KING)
        p.kings = p.side == 0 ? (p.kings & 0xFF00) | to : (p.kings & 0xFF) | (to << 8);
    p.ep = flag == DOUBLE ? (from + to) / 2 : NO_SQUARE;
    p.castle &= rightsKept(from) & rightsKept(to);
    p.side ^= 1;
}

/// Whether `m`, pseudo-legal in `p`, leaves the mover's own king safe.
bool legalIn(Pos p, uint m)
{
    Pos after = p;
    make(after, m);
    return !attacked(after, kingOf(after, p.side), after.side);
}

static const uint START_ROW[8] = { ROOK, KNIGHT, BISHOP, QUEEN, KING, BISHOP, KNIGHT, ROOK };

Pos startPosition()
{
    Pos p;
    for (uint k = 0; k < 8; k++)
        p.squares[k] = 0;
    for (uint f = 0; f < 8; f++)
    {
        put(p, f, START_ROW[f]);
        put(p, 8 + f, PAWN);
        put(p, 48 + f, BLACK | PAWN);
        put(p, 56 + f, BLACK | START_ROW[f]);
    }
    p.side = 0;
    p.castle = 15;
    p.ep = NO_SQUARE;
    p.kings = 4 | (60 << 8);
    return p;
}
