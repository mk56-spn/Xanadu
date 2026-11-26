// CHESS: the AI's search. Every legal root move gets a workgroup of its own (one thread, so nothing diverges) that
// searches the position after it with alpha-beta to the game's depth, then captures only until the position is quiet.
// Shaders can't recurse, so each search keeps its line as a stack of plies in `plies`, their moves in `listed`, and
// steps it at most `budget` times a frame: a search that takes longer goes on next frame where it stopped, so no
// dispatch ever runs long. The game deepens it a ply at a time while time is left (game.slang).
//
// What the searches share: a transposition table every workgroup reads and writes (a position's worth, how deep it was
// searched and its best move, found again by a Zobrist hash), and a history of quiet moves that cut; each keeps its own
// killers (two quiet moves a ply that cut). Moves are tried table move first, then captures by most valuable victim and
// least valuable attacker, then killers, then by history.

static const uint ROOTS = 128;      // root moves searched side by side; more are never searched
static const uint PLIES = 24;       // deepest a line goes, the captures past the depth included
static const uint MOST_DEPTH = PLIES - 8;
static const int INF = 1000000, MATE = 100000, MATE_BOUND = MATE - 256;
static const uint IDLE = 0, RUNNING = 1, DONE = 2;

/// One position on a search's line: its window, the best found, the next of its listed moves to try.
struct Ply
{
    Pos pos;
    uint2 hash;
    int alpha;
    int beta;
    int best;
    int alpha0;         // alpha as it came in: a best at or under it is only an upper bound
    uint next;
    uint count;
    uint legal;         // legal moves tried so far: none, at the end, is mate or stalemate
    uint tactical;      // past the depth: captures only, standing pat allowed
    int left;           // plies short of the depth
    uint bestMove;
    uint hashMove;
    uint pad;
};

/// One root move's search, kept from frame to frame.
struct Search
{
    uint state;
    uint ply;           // the ply being worked on: 0 is the position after the root move
    uint returning;     // a value is on its way up from `ply`
    int value;
    int score;          // done: the root move's worth to the side that plays it
    uint nodes;
    uint move;          // the root move it searches
    uint pad;
};

[Size(ROOTS * PLIES)] List<Ply> plies;
/// Each ply's listed moves and how promising each looks, then one more run for the game's own listing (`listLegal`).
[Size((ROOTS * PLIES + 1) * MOST_MOVES)] List<uint> listed;
[Size((ROOTS * PLIES + 1) * MOST_MOVES)] List<int> scored;
[Size(ROOTS)] List<Search> searches;
[Size(ROOTS * PLIES * 2)] List<uint> killers;
/// Quiet moves that cut, by side, from and to, weighted by the depth left.
[Size(2 * 64 * 64)] List<uint> history;

/// Steps each search takes a frame at most: each is a move tried or a value passed up.
[Default(1500)] uniform uint budget;

uint plyAt(uint g, uint ply) { return g * PLIES + ply; }
uint listAt(uint g, uint ply) { return plyAt(g, ply) * MOST_MOVES; }

// ---- The transposition table ----

static const uint TABLE_SIZE = 1 << 20;
static const uint EXACT = 1, LOWER = 2, UPPER = 3;
/// Each entry: its check (the hash's other half, mixed with the rest so a torn write never passes), the best move, the
/// worth (mates counted from the position), and depth | bound << 8.
[Size(TABLE_SIZE)] List<uint4> table;

uint mixed(uint v)
{
    uint state = v * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

uint2 keyOf(uint k) { return uint2(mixed(k * 2 + 1), mixed(k * 2 + 2)); }

/// The position's Zobrist hash: a key for each piece on each square, the side to move, the castling rights, the en
/// passant file. Pieces run to 14, so 14 × 64 + 63 stays under the other keys.
uint2 hashOf(Pos p)
{
    uint2 h = keyOf(1001 + p.castle);
    if (p.side == 1)
        h ^= keyOf(1000);
    if (p.ep != NO_SQUARE)
        h ^= keyOf(1100 + (p.ep & 7));
    for (uint s = 0; s < 64; s++)
    {
        uint piece = at(p, s);
        if (piece != 0)
            h ^= keyOf(piece * 64 + s);
    }
    return h;
}

int toTable(int v, uint ply) { return v > MATE_BOUND ? v + int(ply + 1) : v < -MATE_BOUND ? v - int(ply + 1) : v; }
int fromTable(int v, uint ply) { return v > MATE_BOUND ? v - int(ply + 1) : v < -MATE_BOUND ? v + int(ply + 1) : v; }

bool probe(uint2 h, out uint move, out int score, out int depth, out uint bound)
{
    uint4 e = table[h.x & (TABLE_SIZE - 1)];
    move = e.y;
    score = int(e.z);
    depth = int(e.w & 255);
    bound = e.w >> 8;
    return (e.x ^ e.y ^ e.z ^ e.w) == h.y && bound != 0;
}

/// Keeps what a finished ply found, unless it only followed captures.
void remember(Ply f, uint ply, int value)
{
    if (f.tactical != 0)
        return;
    uint bound = value >= f.beta ? LOWER : value <= f.alpha0 ? UPPER : EXACT;
    uint4 e = uint4(0, f.bestMove, uint(toTable(value, ply)), uint(max(f.left, 0)) | (bound << 8));
    e.x = f.hash.y ^ e.y ^ e.z ^ e.w;
    table[f.hash.x & (TABLE_SIZE - 1)] = e;
}

// ---- Ordering ----

int orderOf(uint m, uint hashMove, uint killer0, uint killer1, uint side)
{
    if (m == hashMove)
        return 1 << 30;
    uint taken = moveTaken(m);
    if (taken != 0)
        return (1 << 29) + int(taken) * 8 - int(moveMover(m));
    if (movePromo(m) == QUEEN)
        return (1 << 29) - 1;
    if (m == killer0)
        return (1 << 28) + 1;
    if (m == killer1)
        return 1 << 28;
    return int(min(history[side * 4096 + moveFrom(m) * 64 + moveTo(m)], (1u << 27)));
}

/// Swaps the most promising untried move to `next` and hands it out.
uint nextMove(uint base, uint next, uint count)
{
    uint pick = next;
    int most = -1;
    for (uint k = next; k < count; k++)
    {
        if (scored[base + k] > most)
        {
            most = scored[base + k];
            pick = k;
        }
    }
    uint m = listed[base + pick];
    listed[base + pick] = listed[base + next];
    listed[base + next] = m;
    scored[base + pick] = scored[base + next];
    return m;
}

/// A quiet move that cut: a killer at its ply, and history for its side, weighted by the depth left.
void cutBy(uint g, uint ply, Ply f, uint m)
{
    if (moveTaken(m) != 0 || movePromo(m) != 0)
        return;
    uint k = plyAt(g, ply) * 2;
    if (killers[k] != m)
    {
        killers[k + 1] = killers[k];
        killers[k] = m;
    }
    uint weight = uint(max(f.left, 1));
    InterlockedAdd(history[f.pos.side * 4096 + moveFrom(m) * 64 + moveTo(m)], weight * weight);
}

// ---- The search ----

/// Starts ply `ply` of search `g` at `pos` in the window (alpha, beta), `left` plies short of the depth (none left:
/// captures only). True when it is over before it starts, its worth in `value`: the line is as deep as it goes, the
/// table already knows enough, or standing pat beats beta.
bool enter(uint g, uint ply, Pos pos, int alpha, int beta, int left, out int value)
{
    value = 0;
    if (ply + 1 >= PLIES)
    {
        value = evaluate(pos);
        return true;
    }

    Ply f;
    f.pos = pos;
    f.hash = uint2(0);
    f.alpha = alpha;
    f.beta = beta;
    f.alpha0 = alpha;
    f.best = -INF;
    f.next = 0;
    f.legal = 0;
    f.tactical = left <= 0 ? 1 : 0;
    f.left = left;
    f.bestMove = 0;
    f.hashMove = 0;
    f.pad = 0;
    if (f.tactical != 0)
    {
        int stand = evaluate(pos);
        if (stand >= beta)
        {
            value = stand;
            return true;
        }
        f.best = stand;
        f.alpha = max(alpha, stand);
    }
    else
    {
        f.hash = hashOf(pos);
        uint move, bound;
        int score, depth;
        if (probe(f.hash, move, score, depth, bound))
        {
            f.hashMove = move;
            score = fromTable(score, ply);
            if (depth >= left && (bound == EXACT || (bound == LOWER && score >= beta) || (bound == UPPER && score <= alpha)))
            {
                value = score;
                return true;
            }
        }
    }

    uint base = listAt(g, ply);
    f.count = generate(pos, base, f.tactical != 0);
    uint k = plyAt(g, ply) * 2;
    uint killer0 = f.tactical != 0 ? 0 : killers[k], killer1 = f.tactical != 0 ? 0 : killers[k + 1];
    for (uint m = 0; m < f.count; m++)
        scored[base + m] = orderOf(listed[base + m], f.hashMove, killer0, killer1, pos.side);
    plies[plyAt(g, ply)] = f;
    return false;
}

/// What a ply is worth with its moves all tried: its best, or mate or stalemate when it had no legal move.
int exhausted(Ply f, uint ply)
{
    if (f.tactical != 0 || f.legal > 0)
        return f.best;
    return inCheck(f.pos) ? -(MATE - int(ply + 1)) : 0;
}

[Pass(1, groups: ROOTS)]
void think(uint g)
{
    if (game.phase != THINKING || g >= game.roots)
        return;
    Search s = searches[g];
    if (s.state == DONE)
        return;
    int depthLeft = int(game.depth) - 1;
    bool returning = s.returning != 0;
    int value = s.value;
    if (s.state == IDLE)
    {
        Pos child = game.pos;
        make(child, s.move);
        s.state = RUNNING;
        s.ply = 0;
        s.nodes = 1;
        returning = enter(g, 0, child, -INF, INF, depthLeft, value);
    }

    for (uint step = 0; step < budget && s.state == RUNNING; step++)
    {
        if (returning)
        {
            if (s.ply == 0)
            {
                s.score = -value;
                s.state = DONE;
                break;
            }
            s.ply--;
            Ply up = plies[plyAt(g, s.ply)];
            int worth = -value;
            uint played = listed[listAt(g, s.ply) + up.next - 1];
            if (worth > up.best)
            {
                up.best = worth;
                up.bestMove = played;
            }
            up.alpha = max(up.alpha, worth);
            // A cut: this ply is done too, worth its best so far.
            returning = up.alpha >= up.beta;
            if (returning && up.tactical == 0)
                cutBy(g, s.ply, up, played);
            if (returning)
                remember(up, s.ply, up.best);
            plies[plyAt(g, s.ply)] = up;
            value = up.best;
            continue;
        }

        Ply f = plies[plyAt(g, s.ply)];
        if (f.next >= f.count)
        {
            value = exhausted(f, s.ply);
            remember(f, s.ply, value);
            returning = true;
            continue;
        }

        uint m = nextMove(listAt(g, s.ply), f.next, f.count);
        f.next++;
        Pos child = f.pos;
        make(child, m);
        bool safe = !attacked(child, kingOf(child, f.pos.side), child.side);
        if (safe)
            f.legal++;
        plies[plyAt(g, s.ply)] = f;
        if (!safe)
            continue;
        s.ply++;
        s.nodes++;
        returning = enter(g, s.ply, child, -f.beta, -f.alpha, depthLeft - int(s.ply), value);
    }

    s.returning = returning ? 1 : 0;
    s.value = value;
    searches[g] = s;
}
