using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Xanadu.Labs.Experiments.Chess;

/// <summary>
/// Measures the GPU bot: plays it against a UCI engine (Stockfish, kept outside the repo) at a set Elo, as the player,
/// through the same values a click sends, 100 ms a move for the engine. Off unless
/// XANADU_CHESS_MATCH="engine path|elo|games|results file" is set; run it with the window shown to watch. Colours
/// alternate; a threefold repetition (which the bot can't see) or 300 plies is called a draw. Quits when done.
/// </summary>
internal sealed class ChessMatch : IDisposable
{
    private readonly Process _fish;
    private readonly int _games;
    private readonly string _results;
    private readonly List<string> _moves = [];
    private readonly Dictionary<string, int> _seen = [];
    private readonly List<string> _lines = [];
    private Task<string>? _asking;
    private int _game, _seenPlays = -1, _botWins, _fishWins, _draws;
    private bool _started;

    private ChessMatch(string path, int elo, int games, string results)
    {
        _games = games;
        _results = results;
        _fish = Process.Start(new ProcessStartInfo(path) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false })!;
        Say("uci");
        Say("setoption name UCI_LimitStrength value true");
        Say($"setoption name UCI_Elo value {elo}");
        Say("isready");
        while (_fish.StandardOutput.ReadLine() is { } line && line != "readyok") { }
        _lines.Add($"stockfish elo {elo}");
    }

    public static ChessMatch? FromEnvironment()
    {
        if (Environment.GetEnvironmentVariable("XANADU_CHESS_MATCH") is not { } spec)
            return null;
        string[] parts = spec.Split('|');
        return new ChessMatch(parts[0], int.Parse(parts[1]), int.Parse(parts[2]), parts[3]);
    }

    private void Say(string line)
    {
        _fish.StandardInput.WriteLine(line);
        _fish.StandardInput.Flush();
    }

    private string Ask(string moves)
    {
        Say("position startpos" + (moves.Length > 0 ? " moves " + moves : ""));
        Say("go movetime 100");
        while (_fish.StandardOutput.ReadLine() is { } line)
            if (line.StartsWith("bestmove", StringComparison.Ordinal))
                return line.Split(' ')[1];
        return "(none)";
    }

    private static string Square(uint s) => $"{(char)('a' + s % 8)}{s / 8 + 1}";

    private static string Uci(uint m) => Square(m & 63) + Square((m >> 6) & 63) + ((m >> 12 & 7) switch { 2 => "n", 3 => "b", 4 => "r", 5 => "q", _ => "" });

    private static int Encode(string uci)
    {
        int Sq(int at) => (uci[at] - 'a') + (uci[at + 1] - '1') * 8;
        int promo = uci.Length > 4 ? uci[4] switch { 'n' => 2, 'b' => 3, 'r' => 4, _ => 5 } : 0;
        return Sq(0) | Sq(2) << 6 | promo << 12;
    }

    private int BotSide => _game % 2;   // even games the bot plays white

    public void Step(ChessReport report, ChessLab.Stage stage)
    {
        if (!_started)
        {
            _started = true;
            stage.StartGame(BotSide);
            return;
        }
        if (!report.Valid || report.Started != stage.NewGameId)
            return;

        if (report.Plays != _seenPlays)
        {
            if (report.Plays == _moves.Count + 1)
                _moves.Add(Uci(report.Last));
            if (report.Plays == _moves.Count)
            {
                _seenPlays = report.Plays;
                _seen[report.PositionKey] = _seen.GetValueOrDefault(report.PositionKey) + 1;
            }
        }

        if (report.Phase == ChessReport.Over)
        {
            // 1 white won, 2 black won, 3 stalemate.
            Finish(report.Result == 3 ? 0 : (report.Result == 1) == (BotSide == 0) ? 1 : -1, report.Result == 3 ? "stalemate" : "mate", stage);
            return;
        }
        if (_seen.Values.Any(n => n >= 3) || _moves.Count >= 300)
        {
            Finish(0, _moves.Count >= 300 ? "300 plies" : "repetition", stage);
            return;
        }
        if (report.Phase != ChessReport.Player || report.Heard != stage.MovesSent || report.Plays != _moves.Count)
            return;
        _asking ??= Task.Run(() => Ask(string.Join(' ', _moves)));
        if (!_asking.IsCompleted)
            return;
        string best = _asking.Result;
        _asking = null;
        _moves.Add(best);
        stage.SendMove(Encode(best));
    }

    private void Finish(int forBot, string how, ChessLab.Stage stage)
    {
        if (forBot > 0)
            _botWins++;
        else if (forBot < 0)
            _fishWins++;
        else
            _draws++;
        _lines.Add($"game {_game + 1}: bot {(BotSide == 0 ? "white" : "black")}, {(forBot > 0 ? "bot wins" : forBot < 0 ? "stockfish wins" : "draw")} by {how}, {_moves.Count} plies: {string.Join(' ', _moves)}");
        _lines.Add($"score so far: bot {_botWins}, stockfish {_fishWins}, draws {_draws}");
        File.WriteAllLines(_results, _lines);
        _game++;
        _moves.Clear();
        _seen.Clear();
        _seenPlays = -1;
        if (_game >= _games)
        {
            Dispose();
            Environment.Exit(0);
        }
        stage.StartGame(BotSide);
    }

    public void Dispose()
    {
        if (!_fish.HasExited)
            _fish.Kill();
    }
}
