// CHESS: the game. Each frame `play` takes a new game or the player's move (checked against the legal moves, all
// worked out here) or a perft request, `think` steps the AI's searches (`count` a perft's) and `choose` plays the best root move once every search is done.
// The host reads `game` and `legal` back a few frames late to show the board; it sends only values.

static const uint PLAYER = 0, THINKING = 1, OVER = 2;
/// How a finished game ended.
static const uint WHITE_WON = 1, BLACK_WON = 2, STALEMATE = 3;

struct Game
{
    Pos pos;
    uint phase;
    uint started;       // the newGame it last took
    uint heard;         // the moveCount it last took
    uint aiSide;        // 0 white, 1 black
    uint last;          // the last move played, or 0
    uint result;
    uint roots;         // root moves being searched
    uint plays;         // moves played
    int score;          // the AI's last move's worth to it, in centipawns
    uint nodes;         // positions searched for it
    uint frames;        // frames it took
    uint depth;         // plies searched before captures only, or counted
    uint tested;        // the perftCount it last took
};

[Reported] Stored<Game> game;
/// How many, then each move the side to move may make: the player's while it is the player's turn.
[Reported] [Size(MOST_MOVES + 1)] List<uint> legal;

/// A new game whenever this changes, the AI playing `aiPlays` (0 white, 1 black).
[Default(1)] uniform uint newGame;
[Default(1)] uniform uint aiPlays;
/// The player's move, from | to << 6, taken when `moveCount` goes up; a pawn reaching the end becomes a queen.
[Default(0)] uniform uint move;
[Default(0)] uniform uint moveCount;
/// Plies the AI looks ahead before it follows captures only.
[Default(4)] uniform uint depth;

static const uint GAME_LIST = ROOTS * PLIES * MOST_MOVES;

/// Lists the legal moves of the side to move into `legal`; how many.
uint listLegal(Pos p)
{
    uint n = generate(p, GAME_LIST, false);
    uint kept = 0;
    for (uint k = 0; k < n; k++)
    {
        uint m = listed[GAME_LIST + k];
        if (legalIn(p, m))
        {
            legal[1 + kept] = m;
            kept++;
        }
    }
    legal[0] = kept;
    return kept;
}

Search idleSearch()
{
    Search s;
    s.state = IDLE;
    s.ply = 0;
    s.returning = 0;
    s.value = 0;
    s.score = 0;
    s.nodes = 0;
    s.pad0 = 0;
    s.pad1 = 0;
    return s;
}

/// After a move, or at the start: the game ends, waits for the player, or sets every root move searching.
void turn()
{
    Pos p = game.pos;
    uint n = listLegal(p);
    if (n == 0)
    {
        game.phase = OVER;
        game.result = !inCheck(p) ? STALEMATE : p.side == 0 ? BLACK_WON : WHITE_WON;
        return;
    }
    if (p.side != game.aiSide)
    {
        game.phase = PLAYER;
        return;
    }

    uint roots = min(n, ROOTS);
    for (uint k = 0; k < roots; k++)
    {
        rootMoves[k] = legal[1 + k];
        searches[k] = idleSearch();
    }
    game.roots = roots;
    game.phase = THINKING;
    game.nodes = 0;
    game.frames = 0;
    game.depth = clamp(depth, 1, PLIES / 2);
}

[Pass(1, over: 1)]
void play(uint i)
{
    if (game.started != newGame)
    {
        game.pos = startPosition();
        game.started = newGame;
        game.aiSide = aiPlays;
        game.heard = moveCount;
        game.last = 0;
        game.plays = 0;
        game.result = 0;
        game.score = 0;
        turn();
        return;
    }
    if (game.tested != perftCount)
    {
        startCount();
        return;
    }
    if (game.phase != PLAYER || moveCount == game.heard)
        return;
    game.heard = moveCount;
    for (uint k = 0; k < legal[0]; k++)
    {
        uint m = legal[1 + k];
        if (moveFrom(m) != moveFrom(move) || moveTo(m) != moveTo(move) || (movePromo(m) != 0 && movePromo(m) != QUEEN))
            continue;
        Pos p = game.pos;
        make(p, m);
        game.pos = p;
        game.last = m;
        game.plays++;
        turn();
        return;
    }
}

[Pass(1, over: 1)]
void choose(uint i)
{
    if (game.phase != THINKING && game.phase != PERFT)
        return;
    game.frames++;
    uint best = 0, nodes = 0;
    int most = -INF * 2;
    bool done = true;
    for (uint k = 0; k < game.roots; k++)
    {
        Search s = searches[k];
        nodes += s.nodes;
        done = done && s.state == DONE;
        if (s.state == DONE && s.score > most)
        {
            most = s.score;
            best = k;
        }
    }
    game.nodes = nodes;
    if (!done)
        return;
    if (game.phase == PERFT)
    {
        game.phase = OVER;
        game.result = COUNTED;
        return;
    }

    uint m = rootMoves[best];
    Pos p = game.pos;
    make(p, m);
    game.pos = p;
    game.last = m;
    game.score = most;
    game.plays++;
    turn();
}

void build()
{
    play();
    think();
    count();
    choose();
}
