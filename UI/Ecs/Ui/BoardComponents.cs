using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// A whiteboard on a UI node: the <see cref="Board"/> it edits and what its
/// systems keep between frames. It fills the node's laid-out box, so it is
/// sized like any other node in its container. <see cref="UiBoards.Board"/> makes one.
/// </summary>
/// <remarks>
/// <c>BoardInputSystem</c> turns the pointer, wheel, keys and typing into
/// edits; <c>BoardDrawSystem</c> draws it. Each finished edit sets the
/// component again, so owners hear about it through the entity's native
/// <c>OnComponentChanged</c> (<see cref="UiBoards.OnBoardChanged"/>).
/// </remarks>
public struct UiBoard(Board board) : IComponent
{
    public Board Board = board;
    public BoardState State = new();
}

/// <summary>What a press on a board is doing.</summary>
public enum BoardGestureKind
{
    None,
    Pan,
    Move,
    Resize,
    Link,
}

/// <summary>A board's view, selection, gesture and history, kept by its systems.</summary>
public sealed class BoardState
{
    public const int MaxUndo = 200;

    /// <summary>The board point at the area's top-left corner.</summary>
    public Vector2 Pan;

    public float Zoom = 1;

    /// <summary>Where the board is on screen this frame, from its node's laid-out box.</summary>
    public Rect2 Area;

    /// <summary>The screen point the canvas item's (0, 0) sits at.</summary>
    public Vector2 Origin;

    /// <summary>The canvas items it draws into, clipped to <see cref="Area"/>, made by the draw system: the backing and
    /// grid, then <see cref="Underlay"/>'s, then the lines and boxes.</summary>
    public Handle Back, Under, Item;

    /// <summary>
    /// A material drawn over the board's box between its backing and its lines and boxes (a stack's graph drawing onto
    /// the frame there); null for none. With one, the board draws only its lines' arrowheads, leaving the lines to it.
    /// </summary>
    public CanvasMaterial? Underlay;

    public bool HasItem;

    /// <summary>The backing's material (the dot grid), made by the draw system.</summary>
    public CanvasMaterial? Backing;

    /// <summary>What the board looked like when last drawn (<c>BoardLook</c>): unchanged, it isn't drawn again.</summary>
    public int Drawn;

    public readonly HashSet<int> Selected = [];
    public BoardLink? SelectedLink;

    /// <summary>The box whose text is being typed, or 0.</summary>
    public int Editing;

    /// <summary>The box under the pointer, or 0; and whether the pointer is on its link handle or resize corner.</summary>
    public int Hover;
    public bool HoverLinkHandle, HoverResize;

    /// <summary>Keys go to the board: the last press landed on it.</summary>
    public bool Focused;

    public BoardGestureKind Gesture;
    public Vector2 PressScreen, PanAtPress;

    /// <summary>The pointer in board space, this frame.</summary>
    public Vector2 Pointer;

    /// <summary>The box a resize or a line starts from.</summary>
    public int Target;

    /// <summary>Each moving box's rect when the press began.</summary>
    public readonly Dictionary<int, Rect2> Starts = [];

    /// <summary>The press moved past the drag threshold.</summary>
    public bool Dragged;

    /// <summary>For a double click: when and where the last click landed.</summary>
    public float Clock, LastClick = -1;
    public Vector2 LastClickAt;

    public float SinceCaretMove;

    /// <summary>The board before the gesture or edit under way, kept for undo if it changes anything.</summary>
    public Board? Before;

    public readonly List<Board> Undo = [], Redo = [];

    public Vector2 ToBoard(Vector2 screen) => Pan + (screen - Area.Position) / Zoom;

    public Vector2 ToScreen(Vector2 board) => Area.Position + (board - Pan) * Zoom;

    public Rect2 ToScreen(Rect2 board) => new(ToScreen(board.Position), board.Size * Zoom);

    /// <summary>A screen point in the canvas item's own space.</summary>
    public Vector2 Local(Vector2 screen) => screen - Origin;
}

/// <summary>Making boards and hearing about their edits.</summary>
public static class UiBoards
{
    private static readonly Color Clear = new(0, 0, 0, 0);

    /// <summary>
    /// A board editing <paramref name="board"/> under <paramref name="parent"/>. Size it
    /// like any node (<c>.Grow().Stretch()</c> to fill a column).
    /// </summary>
    public static UiNode Board(this UiNode parent, Board board) =>
        parent.Rect(Clear).Input().With(new UiBoard(board));

    /// <summary>
    /// Calls <paramref name="changed"/> after each finished edit (a move, a resize, a line,
    /// typed text, a delete, undo). It is the entity's native <c>OnComponentChanged</c>,
    /// narrowed to updates of <see cref="UiBoard"/>.
    /// </summary>
    public static Entity OnBoardChanged(this Entity board, Action<Board> changed)
    {
        board.OnComponentChanged += change =>
        {
            if (change.Action == ComponentChangedAction.Update && change.Type == typeof(UiBoard))
                changed(change.Component<UiBoard>().Board);
        };
        return board;
    }
}

/// <summary>A board whose lines end in arrowheads, pointing the way each goes; without it they are plain lines.</summary>
public struct Arrowheads : ITag;
