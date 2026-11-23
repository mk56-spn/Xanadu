using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;

namespace Xanadu.Logic.Editor.Kinds;

/// <summary>
/// Decos, placed Xifs that only look: untimed, picked by the box their Xif
/// covers, moved by their origin, pasted by offset. Copies own their Xif
/// trees, so undo and the clipboard never share one with the field.
/// </summary>
public sealed class DecoItemKind : ItemKind<Deco, DecoDocument>
{
    private const float Slack = 8f;

    public override string Name => "deco";

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position)
    {
        Ref(entity).Position = position;
    }

    public override Rect2 Hull(Entity entity) => Decos.Bounds(Get(entity));

    public override bool Hit(Entity entity, Vector2 point, long playheadMs) =>
        Decos.Bounds(Get(entity)).Grow(Slack).HasPoint(point);

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.HasPoint(view.PlayfieldToScreen(Get(entity).Position));

    protected override Deco Detach(Deco value) => value with { Xif = XifCopy.Clone(value.Xif) };

    protected override bool Same(in Deco a, in Deco b) => a.Position == b.Position && XifCopy.AreEqual(a.Xif, b.Xif);

    protected override int Compare(in Deco a, in Deco b)
    {
        int place = ByPosition(a.Position, b.Position);
        return place != 0 ? place : string.CompareOrdinal(a.Xif.Name, b.Xif.Name);
    }

    /// <summary>A pasted deco is a new item: its own id, so groups never mistake it for the one it was copied from.</summary>
    protected override Deco? Pasted(Deco value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities)
    {
        Xif xif = XifCopy.Clone(value.Xif);
        var taken = new HashSet<string>();
        foreach (Entity deco in entities.Query<Deco>().Entities)
            taken.Add(deco.GetComponent<Deco>().Xif.Id);
        xif.Id = LevelItemIds.Fresh(taken, xif.Name);
        return value with { Position = value.Position + offset, Xif = xif };
    }

    protected override List<DecoDocument> List(LevelDocument level) => level.Decos;

    protected override DecoDocument ToDocument(in Deco deco) => DecoDocument.From(deco);

    protected override Deco FromDocument(DecoDocument deco) => deco.ToDeco();

    protected override int CompareDocuments(DecoDocument a, DecoDocument b) => ByXThenY(a.X, a.Y, b.X, b.Y);
}
