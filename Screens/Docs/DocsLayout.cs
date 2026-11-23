using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Core.Timeline;
using Xanadu.Core.Tweens;
using Xanadu.Logic.Docs;
using Xanadu.Model.Docs;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Screens.Docs;

/// <summary>
/// The docs as a panel hanging from the top of the screen: the sections down a
/// side column, the open page beside them, its title in bold over its body.
/// </summary>
public sealed class DocsLayout : IScreenLayout<DocsLogic>
{
    private const float PanelHeight = 760;
    private const float SideWidth = 320;
    private const float DropSeconds = 0.22f;

    private static readonly Flex Fill = new(Grow: 1, Cross: Cross.Stretch);

    private readonly List<Button> _sections = [];
    private Column _panel = null!;
    private Label _title = null!;
    private Label _body = null!;
    private int _shown = -1;

    public string Name => "DROP";

    /// <summary>Plays the opening: the panel drops from above the screen as the backing fades in.</summary>
    public void DropIn(Entity layer)
    {
        Clip drop = Clip.FromNow(DropSeconds).Eased(EasePreset.CubicOut);
        UiTween.Clear(layer);
        UiTween.Clear(_panel.Node);
        UiTween.Play(layer, Tween.FadeOpacityIn(0f, drop));
        UiTween.MoveIn(_panel.Node, new Vector2(0, -PanelHeight), drop);
    }

    public void Build(UiNode layer, DocsLogic logic)
    {
        layer.Backing().Input().Add(
            (_panel = new Column(height: PanelHeight, stretch: true, cross: Cross.Stretch, tone: Tone.Solid)
            {
                new Row(Fill, pad: Space.Xl, gap: Space.Xl)
                {
                    new Column(width: SideWidth, gap: Space.Xs, cross: Cross.Stretch)
                    {
                        new Label(DocsStrings.Title, UiType.Heading, ink: Ink.Accent),
                        logic.Pages.Select(Section),
                        new Spacer(),
                        new Button(DocsStrings.Close, tone: Tone.Quiet, cmd: Cmd(DocsEntrance.Close), tip: DocsStrings.CloseTip),
                    },
                    new Column(Fill, pad: Space.Lg, gap: Space.Lg, tone: Tone.Backing)
                    {
                        (_title = new Label("", UiType.Title, bold: true)),
                        (_body = new Label(wrap: true, stretch: true)),
                    },
                },
            }));
    }

    public void Refresh(DocsLogic logic)
    {
        if (_shown == logic.Current)
            return;

        _shown = logic.Current;
        for (int i = 0; i < _sections.Count; i++)
            _sections[i].Node.Chosen(i == logic.Current);

        DocPage page = logic.Page;
        _title.Node.SetText(page.Title);
        _body.Node.SetText(page.Body);
    }

    private Button Section(DocPage page, int index)
    {
        var button = new Button(page.Name, tone: Tone.Secondary, cmd: Cmd(DocsEntrance.Open, index), stretch: true);
        _sections.Add(button);
        return button;
    }
}
