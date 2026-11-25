using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Ui.Systems.Canvas;
using Handle = Xanadu.Core.Maths.Handle;

namespace Xanadu.Tools.Shaders;

/// <summary>
/// The shader tool's picker: every stack as a card (a live thumbnail over its name)
/// in a scrolling grid, on a scrim over the whole editor, hidden until the bar's
/// SHADERS button opens it. A press on a card, on CLOSE or on the scrim around the
/// panel hides it again; the app reads the presses from <see cref="Cards"/>,
/// <see cref="Thumbs"/>, <see cref="NewButton"/> and <see cref="Scrim"/>.
/// </summary>
internal sealed class ShaderGrid
{
    private const int Columns = 5;
    private const float ThumbWidth = 288, ThumbHeight = 162;
    private static readonly Color ScrimColour = new(0f, 0f, 0f, 0.6f);

    private readonly UiNode _scrim;
    private readonly UiNode _grid;
    private readonly List<Entity> _cards = [];
    private readonly List<Entity> _thumbs = [];

    public ShaderGrid(UiNode layer)
    {
        _scrim = layer.Column().Cover().Fill(ScrimColour).Input()
            .Pad(UiMetrics.Space.Xl).Cross(StackCrossAlignment.Center).Main(StackMainAlignment.Center);
        UiNode panel = _scrim.Panel().Input().Gap(UiMetrics.Space.Md).Cross(StackCrossAlignment.Stretch).Grow();
        UiNode head = panel.Row().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Center);
        head.Text("SHADERS", UiType.Section).AccentText().Grow();
        NewButton = head.Button("NEW", UiType.Small).Secondary().Tooltip("Make a new effect from the template and open it");
        CloseButton = head.Button("CLOSE", UiType.Small).Secondary().Tooltip("Back to the open shader (Escape)");
        UiNode grid = default;
        panel.Add(new Scroll(grow: 1, stretch: true) { new Custom(p => grid = p.Grid(Columns, UiMetrics.Space.Md, UiMetrics.Space.Md)) });
        _grid = grid;
        _scrim.Hidden();
    }

    public Entity Scrim => _scrim;
    public Entity NewButton { get; }
    public Entity CloseButton { get; }

    /// <summary>Each stack's name button, by file index.</summary>
    public IReadOnlyList<Entity> Cards => _cards;

    /// <summary>Each stack's thumbnail, by file index; a press on it picks too.</summary>
    public IReadOnlyList<Entity> Thumbs => _thumbs;

    public bool Open { get; private set; }

    public void Show(bool open)
    {
        Open = open;
        UiLayout.SetHidden(_scrim, !open);
    }

    /// <summary>
    /// A stack's card. The effect as shipped, drawn live with its own defaults, or a still
    /// (<paramref name="still"/>) for one too heavy to draw live; a box the thumbnail's size
    /// clips it, as an effect that moves its own vertices (a deform) would draw past it.
    /// </summary>
    public void Add(ShaderFile file, Handle? still)
    {
        UiNode card = _grid.Column().Gap(UiMetrics.Space.Xs).Cross(StackCrossAlignment.Stretch).Width(ThumbWidth);
        UiNode frame = card.Column().Size(ThumbWidth, ThumbHeight).Clip();
        var size = new Vector2(ThumbWidth, ThumbHeight);
        _thumbs.Add((still is { } texture
                ? frame.Image(texture, size)
                : frame.Rect(Colors.White, size: size).With(new UiMaterial(new CanvasMaterial(file.Path))))
            .Input()
            .Tooltip(file.Path));
        _cards.Add(card.Button(file.Name, UiType.Small).Quiet());
    }

    /// <summary>Bright blue only for the one open; the rest at rest.</summary>
    public void Mark(int picked)
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            UiNode card = UiNode.Wrap(_cards[i]);
            if (i == picked)
                card.Primary();
            else
                card.Quiet();
            _cards[i].AddTag<Dirty>();
        }
    }

    public void Name(int index, string name) => UiText.Set(_cards[index], name);
}
