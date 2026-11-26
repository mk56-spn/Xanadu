using System.Numerics;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Labs.Experiments.Chess;

/// <summary>
/// Chess against an AI that runs inside the render stack (Assets/Shaders/Lab/chess): the rules, the legal moves, the
/// player's move checked and played, the search and the game's end all happen in the stack's passes on the GPU. The
/// board is the stack's shade on a rect; this side only shows the pieces the stack reports back and sends clicks as
/// values (<see cref="ChessReport"/>).
/// </summary>
internal sealed class ChessLab : ILabExperiment
{
    public string Name => "CHESS";

    public bool Keep => true;

    public string[] Notes =>
    [
        "Chess against an AI that runs in a",
        "shader stack: rules, legal moves, the",
        "search and checkmate are GPU passes.",
        "The CPU sends clicks, reads the board.",
        "Click a piece, then where it goes.",
    ];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage();

    private enum ChessCommand { Square, NewGame, Depth, Time, Perft }

    internal sealed class Stage : IToolStage
    {
        private const string Shader = "Assets/Shaders/Lab/chess";
        private const float BoardSize = 840;
        private const float SideWidth = 340;
        private const int NoSquare = 64;

        private readonly CanvasMaterial _material = new(Shader);
        private readonly Label[] _pieces = new Label[64];
        private Label _status = null!, _detail = null!, _timeLabel = null!, _depthLabel = null!;
        private static readonly float[] Times = [0.25f, 0.5f, 1, 2, 4];
        private IDisposable? _presses;
        private int _newGame = 1, _aiPlays = 1, _depth = 4, _time = 1, _moveCount, _selected = NoSquare, _tested, _perftCount;
        private bool _flipped;

        public void Build(UiNode body)
        {
            var board = new Column(size: new Vector2(BoardSize, BoardSize), tone: Tone.Solid);
            body.Add(new Row(gap: Space.Xl, cross: Cross.Start)
            {
                board,
                new Column(width: SideWidth, gap: Space.Md)
                {
                    new Label("CHESS ON THE GPU", UiType.Heading, ink: Ink.Accent),
                    (_status = new Label("", UiType.Section, wrap: true, stretch: true)),
                    (_detail = new Label("", UiType.Body, ink: Ink.Dim, wrap: true, stretch: true)),
                    new Button("NEW GAME AS WHITE", cmd: Cmd(ChessCommand.NewGame, 1), stretch: true),
                    new Button("NEW GAME AS BLACK", cmd: Cmd(ChessCommand.NewGame, 0), stretch: true),
                    (_timeLabel = new Label("", UiType.Body, ink: Ink.Dim, wrap: true, stretch: true)),
                    new Row(gap: Space.Sm)
                    {
                        new Button("LESS TIME", cmd: Cmd(ChessCommand.Time, -1), grow: 1),
                        new Button("MORE TIME", cmd: Cmd(ChessCommand.Time, 1), grow: 1),
                    },
                    new Label("PERFT: CHECK THE RULES", UiType.Label, ink: Ink.Accent),
                    (_depthLabel = new Label("", UiType.Body, ink: Ink.Dim, wrap: true, stretch: true)),
                    new Row(gap: Space.Sm)
                    {
                        new Button("FEWER PLIES", cmd: Cmd(ChessCommand.Depth, -1), grow: 1),
                        new Button("MORE PLIES", cmd: Cmd(ChessCommand.Depth, 1), grow: 1),
                    },
                    new Row(gap: Space.Sm)
                    {
                        new Button(ChessReport.TestNames[0], cmd: Cmd(ChessCommand.Perft, 0), grow: 1),
                        new Button(ChessReport.TestNames[1], cmd: Cmd(ChessCommand.Perft, 1), grow: 1),
                    },
                    new Row(gap: Space.Sm)
                    {
                        new Button(ChessReport.TestNames[2], cmd: Cmd(ChessCommand.Perft, 2), grow: 1),
                        new Button(ChessReport.TestNames[3], cmd: Cmd(ChessCommand.Perft, 3), grow: 1),
                    },
                },
            });

            board.Node.With(new UiMaterial(_material));
            UiNode grid = board.Node.Grid(8);
            float cell = BoardSize / 8;
            for (int c = 0; c < 64; c++)
            {
                var piece = new Label("", UiType.Display, align: Align.Center, width: cell);
                grid.Add(new Column(size: new Vector2(cell, cell), main: Main.Center, cross: Cross.Center, cmd: Cmd(ChessCommand.Square, c)) { piece });
                piece.Node.TextColor(new Color(0.08f, 0.08f, 0.08f));
                _pieces[c] = piece;
            }

            Send();
            _presses = UiCommandEvents.OnPressed<ChessCommand>(body.Entity.Store, Pressed);
        }

        /// <summary>The board square under grid cell <paramref name="cell"/>, rank 8 at the top unless flipped.</summary>
        private int SquareOf(int cell)
        {
            int file = cell % 8, rank = 7 - cell / 8;
            return _flipped ? (7 - rank) * 8 + 7 - file : rank * 8 + file;
        }

        private void Pressed(ChessCommand command, UiCommand press, Entity _)
        {
            switch (command)
            {
                case ChessCommand.NewGame:
                    _aiPlays = press.Arg;
                    _flipped = press.Arg == 0;
                    _newGame++;
                    _selected = NoSquare;
                    break;
                case ChessCommand.Depth:
                    _depth = Math.Clamp(_depth + press.Arg, 1, 6);
                    break;
                case ChessCommand.Time:
                    _time = Math.Clamp(_time + press.Arg, 0, Times.Length - 1);
                    break;
                case ChessCommand.Perft:
                    _tested = press.Arg;
                    _material.Set("perftPosition", _tested);
                    _material.Set("perftCount", ++_perftCount);
                    _selected = NoSquare;
                    _flipped = false;
                    break;
                case ChessCommand.Square:
                    Click(SquareOf(press.Arg));
                    break;
            }

            Send();
        }

        /// <summary>A click on <paramref name="square"/>: pick a piece that can move, or move the picked one there.</summary>
        private void Click(int square)
        {
            ChessReport report = ChessReport.Read(_material);
            // Nothing to pick until the GPU has heard the last request and it is the player's turn.
            if (!report.Valid || report.Started != _newGame || report.Heard != _moveCount || report.Phase != ChessReport.Player)
                return;
            if (_selected != NoSquare && report.HasMove(_selected, square))
            {
                _material.Set("move", _selected | square << 6);
                _material.Set("moveCount", ++_moveCount);
                _selected = NoSquare;
                return;
            }

            _selected = report.HasMoveFrom(square) && square != _selected ? square : NoSquare;
        }

        private void Send()
        {
            _material.Set("newGame", _newGame);
            _material.Set("aiPlays", _aiPlays);
            _material.Set("depth", _depth);
            _material.Set("thinkTime", Times[_time]);
            _material.Set("selected", _selected);
            _material.Set("flipped", _flipped ? 1 : 0);
        }

        private ChessMatch? _match = ChessMatch.FromEnvironment();

        public int NewGameId => _newGame;
        public int MovesSent => _moveCount;

        public void StartGame(int aiPlays)
        {
            _aiPlays = aiPlays;
            _flipped = aiPlays == 0;
            _newGame++;
            Send();
        }

        public void SendMove(int move)
        {
            _material.Set("move", move);
            _material.Set("moveCount", ++_moveCount);
        }

        public void Update(in ToolFrame frame)
        {
            ChessReport report = ChessReport.Read(_material);
            _match?.Step(report, this);
            _timeLabel.Node.SetText($"The AI thinks {Times[_time]:0.##} s a move, a ply deeper at a time.");
            _depthLabel.Node.SetText($"Perft counts {_depth} plies.");
            if (!report.Valid || report.Started != _newGame)
            {
                _status.Node.SetText("Setting up the board on the GPU...");
                return;
            }

            for (int c = 0; c < 64; c++)
                _pieces[c].Node.SetText(ChessReport.Glyph(report.PieceAt(SquareOf(c))));
            _status.Node.SetText(report.Status(_aiPlays, _tested));
            _detail.Node.SetText(report.Detail());
        }

        public void Dispose()
        {
            _presses?.Dispose();
            _match?.Dispose();
        }
    }
}
