using System.Runtime.InteropServices;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Labs.Experiments.Chess;

/// <summary>
/// What the chess stack last reported (<c>[Reported] game</c> and <c>legal</c> in game.slang), a few frames late: the
/// position, whose turn it is, the AI's last search and the legal moves. Read only; nothing here knows the rules.
/// </summary>
internal readonly struct ChessReport
{
    public const uint Player = 0, Thinking = 1, Over = 2, Counting = 3;
    private const uint Counted = 4;

    // Game's layout: the position (eight uints of squares, side, castling, en passant, kings), then the game's own.
    private const int SideAt = 8, PhaseAt = 12, StartedAt = 13, HeardAt = 14, LastAt = 16, ResultAt = 17, RootsAt = 18,
        PlaysAt = 19, ScoreAt = 20, NodesAt = 21, FramesAt = 22, DepthAt = 23, Length = 25;

    /// <summary>The test positions perft counts from, and their published counts by depth (1 on).</summary>
    public static readonly string[] TestNames = ["THE START", "KIWIPETE", "POSITION 3", "POSITION 4"];

    private static readonly long[][] Published =
    [
        [20, 400, 8902, 197281, 4865609, 119060324],
        [48, 2039, 97862, 4085603, 193690690],
        [14, 191, 2812, 43238, 674624, 11030083],
        [6, 264, 9467, 422333, 15833292],
    ];

    private static readonly string[] Glyphs = ["", "♙", "♘", "♗", "♖", "♕", "♔", "", "", "♟", "♞", "♝", "♜", "♛", "♚"];

    private readonly uint[] _game;
    private readonly uint[] _legal;

    private ChessReport(uint[] game, uint[] legal)
    {
        _game = game;
        _legal = legal;
    }

    public static ChessReport Read(CanvasMaterial material)
    {
        uint[] game = material.Reports.TryGetValue("game", out byte[]? g) ? MemoryMarshal.Cast<byte, uint>(g).ToArray() : [];
        uint[] legal = material.Reports.TryGetValue("legal", out byte[]? l) ? MemoryMarshal.Cast<byte, uint>(l).ToArray() : [];
        return new ChessReport(game, legal);
    }

    public bool Valid => _game.Length >= Length && _legal.Length > 0;
    public int Started => (int)_game[StartedAt];
    public int Heard => (int)_game[HeardAt];
    public uint Phase => _game[PhaseAt];

    /// <summary>The piece on <paramref name="square"/>: its kind 1 pawn .. 6 king, plus 8 when black; 0 for none.</summary>
    public uint PieceAt(int square) => (_game[square >> 3] >> ((square & 7) * 4)) & 15;

    public static string Glyph(uint piece) => Glyphs[piece];

    private ReadOnlySpan<uint> Moves => _legal.AsSpan(1, (int)Math.Min(_legal[0], (uint)_legal.Length - 1));

    public bool HasMove(int from, int to)
    {
        foreach (uint m in Moves)
            if ((m & 63) == from && ((m >> 6) & 63) == to)
                return true;
        return false;
    }

    public bool HasMoveFrom(int from)
    {
        foreach (uint m in Moves)
            if ((m & 63) == from)
                return true;
        return false;
    }

    private static string Square(uint s) => $"{(char)('a' + s % 8)}{s / 8 + 1}";

    private static string Move(uint m) => m == 0 ? "" : Square(m & 63) + Square((m >> 6) & 63);

    public string Status(int aiPlays, int tested)
    {
        string you = aiPlays == 1 ? "white" : "black";
        return Phase switch
        {
            Thinking => "The GPU is thinking...",
            Counting => $"Perft: counting {TestNames[tested].ToLowerInvariant()} on the GPU...",
            Over when _game[ResultAt] == Counted => Checked(tested),
            Player => _game[PlaysAt] == 0 ? $"Your move ({you})." : $"AI played {Move(_game[LastAt])}. Your move.",
            _ => _game[ResultAt] switch
            {
                3 => "Stalemate: a draw.",
                1 => aiPlays == 1 ? "Checkmate: you win." : "Checkmate: the GPU wins.",
                _ => aiPlays == 0 ? "Checkmate: you win." : "Checkmate: the GPU wins.",
            },
        };
    }

    /// <summary>A finished perft against the published count.</summary>
    private string Checked(int tested)
    {
        int depth = (int)_game[DepthAt];
        long counted = _game[NodesAt];
        long[] published = Published[tested];
        string expected = depth <= published.Length ? $"{published[depth - 1]:N0}" : "unpublished here";
        string verdict = depth > published.Length ? "" : counted == published[depth - 1] ? " MATCH." : " WRONG.";
        return $"Perft {depth}, {TestNames[tested].ToLowerInvariant()}: {counted:N0} lines, published {expected}.{verdict}";
    }

    public string Detail()
    {
        if (Phase == Counting || (Phase == Over && _game[ResultAt] == Counted))
            return $"{_game[RootsAt]} root moves, a workgroup each: {_game[NodesAt]:N0} lines in {_game[FramesAt]} frames.";
        string side = _game[SideAt] == 0 ? "White" : "Black";
        float score = (int)_game[ScoreAt] / 100f;
        return Phase == Thinking
            ? $"{_game[RootsAt]} root moves, a workgroup each, {_game[DepthAt]} plies deep: {_game[NodesAt]:N0} positions so far, frame {_game[FramesAt]}."
            : $"{side} to move, move {_game[PlaysAt] / 2 + 1}. Last search: {_game[NodesAt]:N0} positions in {_game[FramesAt]} frames, "
              + $"the GPU rated its move {score:+0.00;-0.00}.";
    }
}
