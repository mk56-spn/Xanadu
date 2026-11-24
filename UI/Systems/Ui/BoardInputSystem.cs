using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Edits every <see cref="UiBoard"/>. Double-click empty board for a new box and
/// type its text (Shift+Enter for a new line, Enter or Esc or a click elsewhere
/// to finish); double-click a box, or Enter or F2 with one selected, to retype it.
/// A press on a box selects it (Shift adds or takes out) and a drag moves the
/// selection, frames carrying what they hold. The bottom-right corner resizes; the
/// dot on the right edge drags a line to another box. A click on a line selects it.
/// Delete removes what is selected, 1 to 9 colour the selection, Ctrl+Z and Ctrl+Y
/// (or Ctrl+Shift+Z) undo and redo. Dragging empty board, or with the middle or
/// right button anywhere, pans; the wheel zooms about the pointer. Keys go to the
/// board the last press landed on. A <see cref="ReadOnly"/> board only pans, zooms
/// and selects.
/// </summary>
public sealed partial class BoardInputSystem(IPointerInputSource pointer, IKeyboardState keyboard, IWheelSource? wheel = null)
    : QuerySystem<UiBoard>
{
    public const float DragSlop = 4, DoubleClick = 0.4f;

    /// <summary>On screen, whatever the zoom: the link dot's reach, the resize corner, how near a line takes a click.</summary>
    public const float HandleRadius = 8, CornerSize = 14, LinkReach = 6;

    public const float MinZoom = 0.2f, MaxZoom = 3f, ZoomStep = 1.12f;

    /// <summary>A new box's size, in board units.</summary>
    public static readonly Vector2 NewBoxSize = new(160, 60);

    private bool _wasHeld, _wasPanHeld, _claimed;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        ReadKeys();

        PointerInput input = pointer.Read();
        bool press = input.Pressed && !_wasHeld;
        bool panHeld = input.Middle || input.SecondaryPressed;
        bool panPress = panHeld && !_wasPanHeld;
        _wasHeld = input.Pressed;
        _wasPanHeld = panHeld;

        bool anyEditing = false;
        foreach (Entity entity in Query.ToEntityList())
        {
            ref UiBoard ui = ref entity.GetComponent<UiBoard>();
            BoardState state = ui.State;
            state.Clock += delta;
            state.SinceCaretMove += delta;
            if (Hidden(entity))
            {
                Finish(entity, ref ui);
                state.Gesture = BoardGestureKind.None;
                state.Focused = false;
                continue;
            }

            Place(entity, state);
            Point(entity, ref ui, input, press, panPress, panHeld);
            if (state.Focused && !entity.Tags.Has<ReadOnly>())
                Keys(entity, ref ui, input, delta);
            anyEditing |= state.Editing != 0;
        }

        // Typing goes to a box only while one is being typed into; text fields get it back after.
        if (anyEditing)
            HostPlatform.TextEntry.Claimed = _claimed = true;
        else if (_claimed)
            HostPlatform.TextEntry.Claimed = _claimed = false;
    }

    /// <summary>Where the board is this frame: its node's laid-out box.</summary>
    private static void Place(Entity entity, BoardState state)
    {
        if (!entity.HasComponent<Rectangle>())
            return;

        Vector2 at = entity.TryGetComponent(out Position position) ? position.Value : Vector2.Zero;
        state.Origin = UiTransform.WorldPosition(entity, at);
        state.Area = UiLayout.Box(entity);
    }

    private static bool Hidden(Entity entity)
    {
        for (Entity at = entity; !at.IsNull; at = at.Parent)
        {
            if (at.Tags.Has<UiHidden>())
                return true;
        }

        return false;
    }

    private void Point(Entity entity, ref UiBoard ui, PointerInput input, bool press, bool panPress, bool panHeld)
    {
        BoardState state = ui.State;
        Board board = ui.Board;
        bool inside = state.Area.HasPoint(input.Position);
        // Something drawn over the board takes the press first.
        bool mine = inside && (!entity.Tags.Has<TakesInput>() || entity.Tags.Has<Pressed>());

        if (inside && wheel is { Notches: not 0 } && state.Gesture == BoardGestureKind.None)
            Zoom(state, input.Position, wheel.TakeWheel());

        if (press || panPress)
        {
            if (!mine && !(panPress && inside))
            {
                Finish(entity, ref ui);
                state.Focused = false;
                return;
            }

            state.Focused = true;
            if (panPress && !press)
                Begin(state, BoardGestureKind.Pan, input.Position);
            else
                Press(entity, ref ui, input);
        }
        else if (state.Gesture != BoardGestureKind.None)
        {
            bool held = state.Gesture == BoardGestureKind.Pan ? input.Pressed || panHeld : input.Pressed;
            if (held)
                Drag(state, board, input.Position);
            else
                Release(entity, ref ui, input.Position);
        }

        state.Pointer = state.ToBoard(input.Position);
        Hover(state, board, inside ? state.Pointer : null);
        if (entity.Tags.Has<ReadOnly>())
            state.HoverLinkHandle = state.HoverResize = false;
    }

    private static void Zoom(BoardState state, Vector2 screen, int notches)
    {
        Vector2 under = state.ToBoard(screen);
        state.Zoom = Math.Clamp(state.Zoom * MathF.Pow(ZoomStep, notches), MinZoom, MaxZoom);
        state.Pan = under - (screen - state.Area.Position) / state.Zoom;
    }

    private static void Begin(BoardState state, BoardGestureKind kind, Vector2 screen)
    {
        state.Gesture = kind;
        state.PressScreen = screen;
        state.PanAtPress = state.Pan;
        state.Dragged = false;
        state.Starts.Clear();
    }

    private void Press(Entity entity, ref UiBoard ui, PointerInput input)
    {
        BoardState state = ui.State;
        Board board = ui.Board;
        Vector2 point = state.ToBoard(input.Position);
        bool twice = state.Clock - state.LastClick < DoubleClick &&
                     Vector2.Distance(input.Position, state.LastClickAt) < DragSlop * 2;
        state.LastClick = twice ? -1 : state.Clock;
        state.LastClickAt = input.Position;

        BoardBox? under = board.At(point);
        // Read-only: a press selects what it lands on, or pans the empty board.
        if (entity.Tags.Has<ReadOnly>())
        {
            state.SelectedLink = null;
            if (under is not null)
                Select(state, under.Id);
            else
            {
                state.Selected.Clear();
                Begin(state, BoardGestureKind.Pan, input.Position);
            }

            return;
        }

        if (state.Editing != 0 && under?.Id != state.Editing)
            Finish(entity, ref ui);
        if (state.Editing != 0)
            return;

        if (CornerAt(state, board, point) is { } corner)
        {
            Begin(state, BoardGestureKind.Resize, input.Position);
            state.Target = corner.Id;
            state.Starts[corner.Id] = corner.Rect;
            state.Before = board.Clone();
            Select(state, corner.Id);
            return;
        }

        if (HandleAt(state, board, point) is { } from)
        {
            Begin(state, BoardGestureKind.Link, input.Position);
            state.Target = from.Id;
            state.Before = board.Clone();
            return;
        }

        if (under is not null)
        {
            if (twice)
            {
                Select(state, under.Id);
                Edit(state, board, under.Id);
                state.Before = board.Clone();
                return;
            }

            if (input.Shift)
            {
                if (!state.Selected.Remove(under.Id))
                    state.Selected.Add(under.Id);
            }
            else if (!state.Selected.Contains(under.Id))
                Select(state, under.Id);
            state.SelectedLink = null;

            Begin(state, BoardGestureKind.Move, input.Position);
            foreach (BoardBox box in Carried(board, state.Selected))
                state.Starts[box.Id] = box.Rect;
            state.Before = board.Clone();
            return;
        }

        if (board.LinkAt(point, LinkReach / state.Zoom) is { } link)
        {
            state.Selected.Clear();
            state.SelectedLink = link;
            return;
        }

        if (twice)
        {
            Board before = board.Clone();
            Vector2 at = Board.Snap(point - NewBoxSize / 2);
            BoardBox made = board.Add(new Rect2(at, NewBoxSize), category: LastCategory(board));
            Select(state, made.Id);
            Edit(state, board, made.Id);
            state.Before = before;
            return;
        }

        state.Selected.Clear();
        state.SelectedLink = null;
        Begin(state, BoardGestureKind.Pan, input.Position);
    }

    /// <summary>The selected boxes and everything their frames hold, each once.</summary>
    internal static IEnumerable<BoardBox> Carried(Board board, IEnumerable<int> selected)
    {
        var seen = new HashSet<int>();
        foreach (int id in selected)
        {
            if (board.Find(id) is not { } box)
                continue;

            foreach (BoardBox carried in board.Inside(box).Prepend(box))
            {
                if (seen.Add(carried.Id))
                    yield return carried;
            }
        }
    }

    private static int LastCategory(Board board) => board.Boxes.Count > 0 ? board.Boxes[^1].Category : 0;

    private static void Select(BoardState state, int id)
    {
        state.Selected.Clear();
        state.Selected.Add(id);
        state.SelectedLink = null;
    }

    private static void Drag(BoardState state, Board board, Vector2 screen)
    {
        if (!state.Dragged && Vector2.Distance(screen, state.PressScreen) >= DragSlop)
            state.Dragged = true;
        if (!state.Dragged)
            return;

        Vector2 moved = (screen - state.PressScreen) / state.Zoom;
        switch (state.Gesture)
        {
            case BoardGestureKind.Pan:
                state.Pan = state.PanAtPress - moved;
                break;

            case BoardGestureKind.Move:
                Vector2 step = Board.Snap(moved);
                foreach ((int id, Rect2 start) in state.Starts)
                {
                    if (board.Find(id) is { } box)
                        box.Rect = new Rect2(start.Position + step, start.Size);
                }
                break;

            case BoardGestureKind.Resize:
                if (board.Find(state.Target) is { } sized && state.Starts.TryGetValue(state.Target, out Rect2 from))
                {
                    Vector2 end = Board.Snap(state.ToBoard(screen));
                    Vector2 size = new(MathF.Max(end.X - from.Position.X, Board.MinWidth), MathF.Max(end.Y - from.Position.Y, Board.MinHeight));
                    sized.Rect = new Rect2(from.Position, size);
                }
                break;
        }
    }

    private void Release(Entity entity, ref UiBoard ui, Vector2 screen)
    {
        BoardState state = ui.State;
        Board board = ui.Board;
        BoardGestureKind kind = state.Gesture;
        state.Gesture = BoardGestureKind.None;

        bool changed = kind switch
        {
            BoardGestureKind.Move or BoardGestureKind.Resize => state.Dragged &&
                state.Starts.Any(start => board.Find(start.Key)?.Rect != start.Value),
            BoardGestureKind.Link => board.At(state.ToBoard(screen)) is { } to && board.Link(state.Target, to.Id),
            _ => false,
        };

        if (changed)
            Commit(entity, ref ui);
        else if (state.Editing == 0)
            state.Before = null;
        state.Starts.Clear();
    }

    private static void Hover(BoardState state, Board board, Vector2? point)
    {
        state.Hover = 0;
        state.HoverLinkHandle = state.HoverResize = false;
        if (point is not { } at || state.Gesture != BoardGestureKind.None)
            return;

        state.HoverResize = CornerAt(state, board, at) is not null;
        BoardBox? handle = HandleAt(state, board, at);
        state.HoverLinkHandle = handle is not null;
        state.Hover = handle?.Id ?? board.At(at)?.Id ?? 0;
    }

    /// <summary>The box whose resize corner is under a board point: only the box the point is on.</summary>
    internal static BoardBox? CornerAt(BoardState state, Board board, Vector2 point)
    {
        if (board.At(point) is not { } box)
            return null;

        float size = CornerSize / state.Zoom;
        Vector2 end = box.Rect.End;
        return point.X >= end.X - size && point.Y >= end.Y - size ? box : null;
    }

    /// <summary>Where a box's link dot sits: the middle of its right edge.</summary>
    public static Vector2 HandleOf(Rect2 rect) => new(rect.End.X, rect.Position.Y + rect.Size.Y / 2);

    /// <summary>The box whose link dot is under a board point, smallest box first.</summary>
    internal static BoardBox? HandleAt(BoardState state, Board board, Vector2 point)
    {
        float reach = HandleRadius / state.Zoom;
        return board.PaintOrder().Reverse()
            .FirstOrDefault(box => Vector2.Distance(point, HandleOf(box.Rect)) <= reach);
    }

    /// <summary>
    /// Keeps the board as it was before this edit for undo, and sets the component again
    /// so the owner's <c>OnComponentChanged</c> hears of it.
    /// </summary>
    private void Commit(Entity entity, ref UiBoard ui)
    {
        BoardState state = ui.State;
        if (state.Before is { } before)
        {
            state.Undo.Add(before);
            if (state.Undo.Count > BoardState.MaxUndo)
                state.Undo.RemoveAt(0);
            state.Redo.Clear();
            state.Before = null;
        }

        Changed(entity, ui);
    }

    private void Changed(Entity entity, UiBoard ui) => CommandBuffer.AddComponent(entity.Id, ui);
}
