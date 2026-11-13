using System.Collections.Generic;
using System.Linq;
using Xanadu.Ecs;
using Xanadu.Host.Tools;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Tree;
using static Xanadu.Theme.UiMetrics;

namespace Xanadu.Tools.Moves;

/// <summary>
/// Moves: the character's move system as a chart (<see cref="MovesChart"/>). A
/// row per sector (where she is), a card per thing that can happen there: what
/// it does, and its edge cases underneath. The stripe says whether a card is
/// built, planned or still open. Written by hand, so it says so at the top.
/// </summary>
internal sealed class MovesStage : IToolStage
{
    private const float SectorWidth = 200;
    private const float CardWidth = 272;

    /// <summary>The stripe colour of each status, from the theme's categories.</summary>
    private static int CategoryOf(MoveStatus status) => status switch
    {
        MoveStatus.Built => 0,
        MoveStatus.Planned => 2,
        _ => 4,
    };

    public void Build(UiNode body) => body.Add(new Column(new Flex(Grow: 1, Cross: Cross.Stretch), gap: Space.Md)
    {
        Header(),
        MovesChart.Sectors.Select(Sector),
    });

    private static Row Header() => new(gap: Space.Md)
    {
        new Label("MOVES", UiType.Heading, ink: Ink.Accent),
        new Label("Written by hand: update Tools/DevTools/Moves/MovesChart.cs whenever movement changes.", UiType.Small, ink: Ink.Dim),
        new Spacer(),
        Legend(MoveStatus.Built, "BUILT"),
        Legend(MoveStatus.Planned, "PLANNED"),
        Legend(MoveStatus.Open, "OPEN"),
    };

    private static Row Legend(MoveStatus status, string name) => new(gap: Space.Sm)
    {
        new Box(size: new Vector2(Space.Md, Space.Md), category: CategoryOf(status)),
        new Label(name, UiType.Small),
    };

    private static Row Sector(MoveSector sector) => new(gap: Space.Sm, cross: Cross.Stretch)
    {
        new Column(width: SectorWidth, gap: Space.Xs, cross: Cross.Stretch)
        {
            new Label(sector.Name, UiType.Section, align: TextAlign.Left),
            Lines(sector.Blurb, UiType.Caption, SectorWidth, Ink.Dim),
        },
        sector.Cards.Select(Card),
    };

    private static Row Card(MoveCard card) => new(width: CardWidth, cross: Cross.Stretch, tone: Tone.Solid)
    {
        new Box(width: Space.Xs, category: CategoryOf(card.Status)),
        new Column(pad: Space.Sm, gap: Space.Xxs, grow: 1, cross: Cross.Stretch)
        {
            new Label(card.Event, UiType.Small, bold: true, align: TextAlign.Left),
            Lines(card.What, UiType.Caption, TextWidth),
            card.Edge is { } edge ? Lines(edge, UiType.Caption, TextWidth, Ink.Dim) : null,
        },
    };

    private const float TextWidth = CardWidth - Space.Xs - Space.Sm * 2;

    /// <summary>Text broken at spaces into left-aligned labels that each fit <paramref name="width"/>.</summary>
    private static Column Lines(string text, int type, float width, Ink ink = Ink.Normal)
    {
        var lines = new List<string>();
        string line = "";
        foreach (string word in text.Split(' '))
        {
            string longer = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && UiNode.Measure(longer, type).X > width)
            {
                lines.Add(line);
                longer = word;
            }

            line = longer;
        }

        lines.Add(line);
        return new Column(cross: Cross.Stretch)
        {
            lines.Select(text => new Label(text, type, ink: ink, align: TextAlign.Left)),
        };
    }

    public void Update(in ToolFrame frame) { }

    public void Dispose() { }
}
