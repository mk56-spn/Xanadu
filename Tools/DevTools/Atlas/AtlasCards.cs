using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Tree;
using static Xanadu.Theme.UiMetrics;

namespace Xanadu.Tools.Atlas;

/// <summary>Atlas's own pieces, each a spec made of specs, and the groups of them its columns keep.</summary>
internal sealed partial class AtlasStage
{
    /// <summary>A small card: the pressable box tinted by kind, its kind's stripe, and the name. It presses <paramref name="press"/> with the type it shows.</summary>
    private sealed class TypeCard(AtlasCommand press) : UiSpec
    {
        private readonly Box _stripe = new() { Width = Space.Xs };
        private readonly Label _name = new("", UiType.Body) { Grow = 1, Fit = true };

        public UiNode Box => Node;
        public UiNode Stripe => _stripe.Node;
        public UiNode Name => _name.Node;

        protected override UiNode Create(UiNode parent) => parent.Add(new Row
        {
            Cross = StackCrossAlignment.Stretch,
            Gap = Space.Sm,
            Command = UiCommand.Of(press),
            Tooltip = "",
            Children = { _stripe, _name },
        }).Node;
    }

    /// <summary>The card a chart centres on: name, kind and project, namespace.</summary>
    private sealed class BigCard(float width) : UiSpec
    {
        private readonly Label _name = new("", UiType.Section) { Fit = true };
        private readonly Label _kind = new("", UiType.Small) { Fit = true };
        private readonly Label _space = new("", UiType.Caption) { Role = TextRole.Dim, Fit = true };

        public UiNode Box => Node;
        public UiNode Name => _name.Node;
        public UiNode Kind => _kind.Node;
        public UiNode Space => _space.Node;

        protected override UiNode Create(UiNode parent) => parent.Add(new Column
        {
            Width = width,
            Pad = UiMetrics.Space.Md,
            Gap = UiMetrics.Space.Xs,
            Cross = StackCrossAlignment.Stretch,
            Tooltip = "",
            Children = { _name, _kind, _space },
        }).Node;
    }

    /// <summary>A kind chip in the header: its swatch and name; pressing it hides or shows the kind in the chart.</summary>
    private sealed class KindChip(NodeKind kind) : UiSpec
    {
        private readonly Box _swatch = new() { Size = new Vector2(UiMetrics.Space.Md, UiMetrics.Space.Md) };
        private readonly Label _label = new(kind.ToString().ToUpperInvariant(), UiType.Small);

        public UiNode Chip => Node;
        public UiNode Swatch => _swatch.Node;
        public UiNode Label => _label.Node;

        protected override UiNode Create(UiNode parent) => parent.Add(new Row
        {
            Gap = UiMetrics.Space.Sm,
            Pad = UiMetrics.Space.Sm,
            Tone = Xanadu.Ui.Tree.Tone.Secondary,
            Command = UiCommand.Of(AtlasCommand.Kind, (int)kind),
            Children = { _swatch, _label },
        }).Node;
    }

    private sealed class FocusSide
    {
        public PooledList List = null!;
        public Label Foot = null!, Empty = null!;
        public readonly List<(Label Heading, TypeCard Card)> Rows = new();
        public readonly List<UiNode> Lines = new();
    }

    private sealed class CompareColumn
    {
        public PooledList List = null!;
        public Label Foot = null!;
        public readonly List<TypeCard> Rows = new();

        /// <summary>A line per row to the first card, and one to the second (only the shared column uses both).</summary>
        public readonly List<UiNode> ToA = new(), ToB = new();
    }

    private readonly record struct SimilarRow(TypeCard A, TypeCard B, Label Score, Label Breakdown, Label Fields);
}
