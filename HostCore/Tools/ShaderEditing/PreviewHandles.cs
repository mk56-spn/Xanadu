using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The code's shapes as handles over the preview (<see cref="SlangHandles"/>): the shape the
/// caret is in shows its own (size, radius, corner, points, and a move square, a turn arm and a
/// scale square) with its outline; every shape the CPU can place is somewhere to press, which puts
/// the caret on it and drags it. A drag writes into the code (a literal, or a step or argument
/// it adds) or into a tuned value, whole pixels, the drag one undo step. The handles are UI grips
/// on a layer of their own over the preview, on a frame that follows the preview's box.
/// </summary>
internal sealed class PreviewHandles
{
    private const float DotReach = 10, LeastBody = 14;
    private static readonly Color Guide = Palette.PrimarySoft with { A = 0.7f };

    private readonly UiNode _layer, _frame, _outline, _arm;
    private readonly List<UiNode> _grips = [];

    /// <summary>What each shown grip is: its shape, and its handle, or null for the shape's body.</summary>
    private readonly List<(ChainHandles Shape, ChainHandle? Handle)> _slots = [];

    private Rect2 _framed;
    private TextBuffer? _buffer;
    private TextHistory? _history;
    private SlangHandleDrag? _drag;
    private Action _done = () => { };

    private readonly Func<string, int, IReadOnlyList<string>> _parameterNames;
    private readonly Func<string, float[]?> _values;
    private readonly Action<string, float[]> _setValue;

    /// <param name="parameterNames">The library's parameter names by call and argument.</param>
    /// <param name="values">A tuned value by name; <paramref name="setValue"/> sets one a drag writes.</param>
    public PreviewHandles(EntityStore entities, Func<string, int, IReadOnlyList<string>> parameterNames,
        Func<string, float[]?> values, Action<string, float[]> setValue)
    {
        (_parameterNames, _values, _setValue) = (parameterNames, values, setValue);
        _layer = UiNode.Layer(entities, default, UiLayers.ShaderPreviewHandles, null, UiStage.Full).Theme(UiThemes.Tools);
        _frame = _layer.Column().IgnoreLayout();
        _outline = _frame.Shape([Vector2.Zero, Vector2.One], default, Guide, 1.5f).IgnoreLayout();
        _arm = _frame.Shape([Vector2.Zero, Vector2.One], default, Guide, 1.5f, closed: false).IgnoreLayout();
        _switches = new PreviewSwitches(_frame);
    }

    public bool Dragging => _drag is not null;

    /// <summary>Where the caret starts in the shader's text (the shader tool's <c>--caret</c>), for a shot of a shape's handles.</summary>
    public static (int Line, int Column)? StartCaret { get; set; }

    /// <summary>The library's handle tags, read again with the library; the switches' buttons are made from them.</summary>
    public SlangHandleTags Tags
    {
        get => _tags;
        set
        {
            _tags = value;
            _switches.Use(value);
        }
    }

    private SlangHandleTags _tags = SlangHandleTags.FromSources([]);
    private readonly PreviewSwitches _switches;

    private Vector2 Canvas => _framed.Size;

    /// <summary>
    /// Each frame: the shapes in <paramref name="buffer"/> (the shader's own text; null hides them)
    /// and their grips on <paramref name="preview"/>. <paramref name="done"/> runs when a drag ends.
    /// </summary>
    public void Follow(Rect2 preview, TextBuffer? buffer, TextHistory history, Action done)
    {
        if (preview != _framed && preview.Size.X > 0)
        {
            _framed = preview;
            _frame.Box(preview);
        }

        (_buffer, _history, _done) = (buffer, history, done);
        _slots.Clear();
        ChainHandles? selected = null;
        if (buffer is not null && StartCaret is var (startLine, startColumn))
        {
            StartCaret = null;
            buffer.Place(Math.Min(startLine, buffer.Lines.Count - 1), startColumn);
        }

        if (buffer is not null)
        {
            int caret = TextPlaces.Offset(buffer.Lines, buffer.Line, buffer.Column);
            foreach (ShapeChain chain in SlangShapeChains.Find(buffer.Text, Tags, _values))
            {
                if (SlangHandles.Of(chain, Tags, _parameterNames, Canvas) is not { } shape)
                    continue;
                if (chain.Holds(caret) || (_drag is { } drag && drag.ChainStart == chain.Start))
                    selected = shape;
                if (shape.HasExtent && shape.Handles.Exists(handle => handle.Kind == HandleKind.Move))
                    _slots.Add((shape, null));
            }
        }

        // The caret's shape's own handles last: they are drawn over the bodies and, being smaller, taken first.
        foreach (ChainHandle handle in selected?.Handles ?? [])
            _slots.Add((selected!, handle));
        Show(selected);
        _switches.Show(selected?.Chain, buffer, history, done);
    }

    public void Delete()
    {
        _switches.Dispose();
        UiLayout.DeleteSubtree(_layer.Entity);
    }

    private void Show(ChainHandles? selected)
    {
        while (_grips.Count < _slots.Count)
            _grips.Add(MakeGrip(_grips.Count));
        for (int i = 0; i < _grips.Count; i++)
        {
            UiNode grip = _grips[i];
            bool shown = i < _slots.Count;
            UiLayout.SetHidden(grip.Entity, !shown);
            if (!shown)
                continue;
            (ChainHandles shape, ChainHandle? handle) = _slots[i];
            grip.At(handle?.At ?? shape.Origin);
            grip.Entity.GetComponent<UiGrip>().Reach = handle is null ? Body(shape) : DotReach;
            Look(grip.Entity, handle?.Kind);
        }

        UiLayout.SetHidden(_outline.Entity, selected is null);
        UiLayout.SetHidden(_arm.Entity, selected is null);
        if (selected is null)
            return;
        _outline.Reshape(Outline(selected));
        Vector2 top = selected.Placement.Apply(new Vector2(0, -selected.Extent.Y));
        int turn = selected.Handles.FindIndex(handle => handle.Kind == HandleKind.Turn);
        Vector2 end = turn < 0 ? top : selected.Handles[turn].At;
        _arm.Reshape([top, end]);
    }

    /// <summary>How near a press has to be to a shape's centre to take its body: its size, as placed.</summary>
    private static float Body(ChainHandles shape) =>
        MathF.Max(MathF.Max(shape.Extent.X, shape.Extent.Y) * shape.Placement.Scale, LeastBody);

    /// <summary>The placement handles are squares, the shape's own dots; a body has no look.</summary>
    private static void Look(Entity grip, HandleKind? kind)
    {
        bool square = kind is HandleKind.Move or HandleKind.Scale, dot = kind is not null && !square;
        if (grip.Tags.Has<GripSquare>() != square)
        {
            if (square) grip.AddTag<GripSquare>(); else grip.RemoveTag<GripSquare>();
        }

        if (grip.Tags.Has<GripDot>() != dot)
        {
            if (dot) grip.AddTag<GripDot>(); else grip.RemoveTag<GripDot>();
        }
    }

    /// <summary>The shape's edge as placed: a ring for one sized by a radius, else its box.</summary>
    private static Vector2[] Outline(ChainHandles shape)
    {
        Vector2 e = shape.Extent;
        bool round = shape.Handles.Exists(handle => handle.Kind == HandleKind.Radius);
        const int Segments = 48;
        Vector2[] local = round
            ? [.. Enumerable.Range(0, Segments).Select(i => e.X * new Vector2(MathF.Cos(i * MathF.Tau / Segments), MathF.Sin(i * MathF.Tau / Segments)))]
            : [new(-e.X, -e.Y), new(e.X, -e.Y), new(e.X, e.Y), new(-e.X, e.Y)];
        return [.. local.Select(shape.Placement.Apply)];
    }

    private UiNode MakeGrip(int slot)
    {
        UiNode grip = _frame.Grip(DotReach);
        grip.Entity.AddSignalHandler<GripTaken>(signal => Take(slot, signal.Event.Pointer));
        grip.Entity.AddSignalHandler<GripPulled>(signal => Pull(signal.Event.Pointer));
        grip.Entity.AddSignalHandler<GripLetGo>(_ => LetGo());
        return grip;
    }

    /// <summary>A press on a grip: a handle starts its drag; a shape's body puts the caret on it and moves it.</summary>
    private void Take(int slot, Vector2 pointer)
    {
        if (slot >= _slots.Count || _buffer is null)
            return;
        (ChainHandles shape, ChainHandle? handle) = _slots[slot];
        if (handle is null)
        {
            (int line, int column) = TextPlaces.Place(_buffer.Lines, shape.Chain.Start);
            _buffer.Place(line, column);
            int move = shape.Handles.FindIndex(own => own.Kind == HandleKind.Move);
            handle = move < 0 ? null : shape.Handles[move];
        }

        if (handle is not { } taken)
            return;
        _drag = SlangHandleDrag.Take(shape, taken, pointer - _framed.GetCenter(), _parameterNames);
        _history?.Hold();
    }

    /// <summary>The held handle's new value, written into the code as it is now, or into its tuned value.</summary>
    private void Pull(Vector2 pointer)
    {
        if (_drag is not { } drag || _buffer is not { } buffer)
            return;
        ShapeChain? chain = SlangShapeChains.Find(buffer.Text, Tags, _values).Find(found => found.Start == drag.ChainStart);
        if (chain is null || drag.Edit(chain, drag.Pull(pointer - _framed.GetCenter(), Canvas), _parameterNames) is not { } edit)
            return;
        if (edit.Uniform is { } uniform)
        {
            _setValue(uniform, edit.Value);
            return;
        }

        (int line, int from) = TextPlaces.Place(buffer.Lines, edit.From);
        (int endLine, int to) = TextPlaces.Place(buffer.Lines, edit.To);
        if (endLine == line && buffer.Lines[line][from..to] != edit.Text)
            buffer.Edit(line, from, to, edit.Text);
    }

    private void LetGo()
    {
        if (_drag is null)
            return;
        _drag = null;
        _history?.LetGo();
        _done();
    }
}
