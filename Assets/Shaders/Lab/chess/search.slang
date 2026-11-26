// CHESS: the AI's search. Every legal root move gets a workgroup of its own (one thread, so nothing diverges) that
// searches the position after it with alpha-beta to the game's depth, then captures only until the position is quiet.
// Shaders can't recurse, so each search keeps its line as a stack of plies in `plies`, their moves in `listed`, and
// steps it at most `budget` times a frame: a search that takes longer goes on next frame where it stopped, so no
// dispatch ever runs long.

static const uint ROOTS = 128;      // root moves searched side by side; more are never searched
static const uint PLIES = 16;       // deepest a line goes, the captures past the depth included
static const int INF = 1000000, MATE = 100000;
static const uint IDLE = 0, RUNNING = 1, DONE = 2;

/// One position on a search's line: its window, the best found, the next of its listed moves to try.
struct Ply
{
    Pos pos;
    int alpha;
    int beta;
    int best;
    uint next;
    uint count;
    uint legal;         // legal moves tried so far: none, at the end, is mate or stalemate
    uint tactical;      // past the depth: captures only, standing pat allowed
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
    uint pad0, pad1;
};

[Size(ROOTS * PLIES)] List<Ply> plies;
/// Each ply's listed moves, then one more run of them for the game's own listing (`listLegal`).
[Size((ROOTS * PLIES + 1) * MOST_MOVES)] List<uint> listed;
[Size(ROOTS)] List<Search> searches;
[Size(ROOTS)] List<uint> rootMoves;

/// Steps each search takes a frame at most: each is a move tried or a value passed up.
[Default(1500)] uniform uint budget;

uint plyAt(uint g, uint ply) { return g * PLIES + ply; }
uint listAt(uint g, uint ply) { return plyAt(g, ply) * MOST_MOVES; }

/// Starts ply `ply` of search `g` at `pos` in the window (alpha, beta), `left` plies short of the depth (none left:
/// captures only). True when it is over before it starts, its worth in `value`: the line is as deep as it goes, or
/// standing pat already beats beta.
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
    f.alpha = alpha;
    f.beta = beta;
    f.best = -INF;
    f.next = 0;
    f.legal = 0;
    f.tactical = left <= 0 ? 1 : 0;
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

    f.count = generate(pos, listAt(g, ply), f.tactical != 0);
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

/// Swaps the most promising untried move to `next` and hands it out.
uint nextMove(uint base, uint next, uint count)
{
    uint pick = next;
    int most = -1;
    for (uint k = next; k < count; k++)
    {
        int order = orderOf(listed[base + k]);
        if (order > most)
        {
            most = order;
            pick = k;
        }
    }
    uint m = listed[base + pick];
    listed[base + pick] = listed[base + next];
    listed[base + next] = m;
    return m;
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
        make(child, rootMoves[g]);
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
            up.best = max(up.best, worth);
            up.alpha = max(up.alpha, worth);
            plies[plyAt(g, s.ply)] = up;
            // A cut: this ply is done too, worth its best so far.
            returning = up.alpha >= up.beta;
            value = up.best;
            continue;
        }

        Ply f = plies[plyAt(g, s.ply)];
        if (f.next >= f.count)
        {
            value = exhausted(f, s.ply);
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
