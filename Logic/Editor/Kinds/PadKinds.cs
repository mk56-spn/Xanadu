using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;

namespace Xanadu.Logic.Editor.Kinds;

/// <summary>Pads: placed and moved freely, picked on their slab; a timed pad also has a time, and pastes at the playhead.</summary>
public sealed class PadItemKind : ItemKind<Pad, PadDocument>
{
    /// <summary>How far round its slab a pad can still be picked, playfield units.</summary>
    private const float Slack = 12f;

    public override string Name => "pad";

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position)
    {
        Ref(entity).Position = position;
    }

    public override long? Time(Entity entity) => Get(entity) is { IsTimed: true } pad ? pad.TimeMs : null;

    public override bool SetTime(Entity entity, long timeMs)
    {
        if (!Get(entity).IsTimed)
            return false;
        Ref(entity).TimeMs = timeMs;
        return true;
    }

    public override long? TimeOf(object value) => ((Pad)value) is { IsTimed: true } pad ? pad.TimeMs : null;

    public override Rect2 Hull(Entity entity) => new(Get(entity).Position - Pads.Size / 2f, Pads.Size);

    public override bool Hit(Entity entity, Vector2 point, long playheadMs)
    {
        Vector2 gap = point - Get(entity).Position;
        Vector2 reach = Pads.Size / 2f + new Vector2(Slack);
        return MathF.Abs(gap.X) <= reach.X && MathF.Abs(gap.Y) <= reach.Y;
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.HasPoint(view.PlayfieldToScreen(Get(entity).Position));

    protected override int Compare(in Pad a, in Pad b)
    {
        int x = a.Position.X.CompareTo(b.Position.X);
        return x != 0 ? x : a.Position.Y.CompareTo(b.Position.Y);
    }

    protected override bool Same(in Pad a, in Pad b) => a.Position == b.Position && a.Speed == b.Speed && a.TimeMs == b.TimeMs;

    protected override Pad? Pasted(Pad value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value.IsTimed ? value with { TimeMs = timeMs } : value with { Position = value.Position + offset };

    protected override List<PadDocument> List(LevelDocument level) => level.Pads;

    protected override PadDocument ToDocument(in Pad pad) => PadDocument.From(pad);

    protected override Pad FromDocument(PadDocument pad) => pad.ToPad();
}
