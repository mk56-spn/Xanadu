using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Tree;
using static Xanadu.Ui.Tree.UiSpecs;

namespace Xanadu.Tools.Atlas;

/// <summary>Atlas's own pieces, each a spec made of specs, and the groups of them its columns keep.</summary>
internal sealed partial class AtlasStage
{
    /// <summary>A small card: the pressable box tinted by kind, its kind's stripe, and the name. It presses <paramref name="press"/> with the type it shows.</summary>
    private sealed class TypeCard(AtlasCommand press, float grow = 0, float? width = null) : UiSpec(width, grow: grow)
    {
        private readonly Box _stripe = new(width: UiMetrics.Space.Xs);
        private readonly Label _name = new("", UiType.Body, grow: 1, fit: true);

        public UiNode Box => Node;
        public UiNode Stripe => _stripe.Node;
        public UiNode Name => _name.Node;

        protected override UiNode Create(UiNode parent) =>
            parent.Add(new Row(cross: Cross.Stretch, gap: UiMetrics.Space.Sm, cmd: Cmd(press), tip: "") { _stripe, _name }).Node;
    }

    /// <summary>The card a chart centres on: name, kind and project, namespace.</summary>
    private sealed class BigCard(float width) : UiSpec
    {
        private readonly Label _name = new("", UiType.Section, fit: true);
        private readonly Label _kind = new("", UiType.Small, fit: true);
        private readonly Label _space = new("", UiType.Caption, ink: Ink.Dim, fit: true);

        public UiNode Box => Node;
        public UiNode Name => _name.Node;
        public UiNode Kind => _kind.Node;
        public UiNode Space => _space.Node;

        protected override UiNode Create(UiNode parent) => parent.Add(
            new Column(width: width, pad: UiMetrics.Space.Md, gap: UiMetrics.Space.Xs, cross: Cross.Stretch, tip: "")
            {
                _name, _kind, _space,
            }).Node;
    }

    /// <summary>A kind chip in the header: its swatch and name; pressing it hides or shows the kind in the chart.</summary>
    private sealed class KindChip(NodeKind kind) : UiSpec
    {
        private readonly Box _swatch = new(size: new Vector2(UiMetrics.Space.Md, UiMetrics.Space.Md));
        private readonly Label _label = new(kind.ToString().ToUpperInvariant(), UiType.Small);

        public UiNode Chip => Node;
        public UiNode Swatch => _swatch.Node;
        public UiNode Label => _label.Node;

        protected override UiNode Create(UiNode parent) => parent.Add(
            new Row(gap: UiMetrics.Space.Sm, pad: UiMetrics.Space.Sm, tone: Tone.Secondary, cmd: Cmd(AtlasCommand.Kind, (int)kind))
            {
                _swatch, _label,
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
