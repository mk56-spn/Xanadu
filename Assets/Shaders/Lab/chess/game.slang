// CHESS: the game. Each frame `play` takes a new game or the player's move (checked against the legal moves, all
// worked out here) or a perft request, `think` steps the AI's searches (`count` a perft's) and `choose` deepens them
// a ply at a time while time is left, then plays the best root move of the deepest search it finished. The host reads `game` and `legal` back a few frames late to show the board; it sends only values.

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
    uint depth;         // plies being searched before captures only, or counted
    uint tested;        // the perftCount it last took
    uint completed;     // the deepest search finished this turn
    uint bestMove;      // its best root move
    uint spent;         // positions the finished depths searched
    float began;        // when the turn's thinking started, s
};

[Reported] Stored<Game> game;
/// How many, then each move the side to move may make: the player's while it is the player's turn.
[Reported] [Size(MOST_MOVES + 1)] List<uint> legal;

/// A new game whenever this changes, the AI playing `aiPlays` (0 white, 1 black).
[Default(1)] uniform uint newGame;
[Default(1)] uniform uint aiPlays;
/// The player's move, from | to << 6 | the kind a pawn becomes << 12 (none: a queen), taken when `moveCount` goes up.
[Default(0)] uniform uint move;
[Default(0)] uniform uint moveCount;
/// Plies perft counts.
[Default(4)] uniform uint depth;
/// Seconds the AI thinks a move: it starts no new depth past half of it, and stops a depth that runs past all of it.
[Default(0.5)] uniform float thinkTime;

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

Search idleSearch(uint move)
{
    Search s;
    s.state = IDLE;
    s.ply = 0;
    s.returning = 0;
    s.value = 0;
    s.score = 0;
    s.nodes = 0;
    s.move = move;
    s.pad = 0;
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
    // Cuts from last turn still say something, but less.
    for (uint h = 0; h < 2 * 64 * 64; h++)
        history[h] /= 4;
    game.roots = roots;
    game.phase = THINKING;
    game.nodes = 0;
    game.spent = 0;
    game.frames = 0;
    game.began = time;
    game.completed = 0;
    game.bestMove = legal[1];
    for (uint k = 0; k < roots; k++)
        searches[k] = idleSearch(legal[1 + k]);
    deepen(1);
}

/// Starts every root move's search again, `deep` plies deep.
void deepen(uint deep)
{
    game.depth = deep;
    for (uint k = 0; k < game.roots; k++)
        searches[k] = idleSearch(searches[k].move);
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
        // No piece named: a pawn reaching the end becomes a queen.
        uint promo = movePromo(move) != 0 ? movePromo(move) : QUEEN;
        if (moveFrom(m) != moveFrom(move) || moveTo(m) != moveTo(move) || (movePromo(m) != 0 && movePromo(m) != promo))
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

/// Plays the AI's move and hands the turn on.
void playBest()
{
    uint m = game.bestMove;
    Pos p = game.pos;
    make(p, m);
    game.pos = p;
    game.last = m;
    game.plays++;
    turn();
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
    game.nodes = game.spent + nodes;
    if (game.phase == PERFT)
    {
        if (done)
        {
            game.phase = OVER;
            game.result = COUNTED;
        }
        return;
    }

    float spentTime = time - game.began;
    if (!done)
    {
        // Out of time mid-depth: the deepest finished depth's move stands.
        if (spentTime >= thinkTime && game.completed > 0)
            playBest();
        return;
    }

    game.completed = game.depth;
    game.bestMove = searches[best].move;
    game.score = most;
    game.spent += nodes;
    if (game.depth >= MOST_DEPTH || spentTime >= thinkTime * 0.5 || abs(most) > MATE_BOUND || game.roots == 1)
        playBest();
    else
        deepen(game.depth + 1);
}

void build()
{
    play();
    think();
    count();
    choose();
}
