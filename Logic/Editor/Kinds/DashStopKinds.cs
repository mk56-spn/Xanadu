using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;

namespace Xanadu.Logic.Editor.Kinds;

/// <summary>Dash enders: untimed, placed, moved and picked by their square, pasted by offset.</summary>
public sealed class DashStopItemKind : ItemKind<DashStop, DashStopDocument>
{
    private const float Slack = 10f;

    public override string Name => "dash ender";

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position)
    {
        Ref(entity).Position = position;
    }

    public override Rect2 Hull(Entity entity) => new(Get(entity).Position - DashStops.Size / 2f, DashStops.Size);

    public override bool Hit(Entity entity, Vector2 point, long playheadMs)
    {
        Vector2 gap = point - Get(entity).Position;
        return MathF.Abs(gap.X) <= DashStops.Size.X / 2f + Slack && MathF.Abs(gap.Y) <= DashStops.Size.Y / 2f + Slack;
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.HasPoint(view.PlayfieldToScreen(Get(entity).Position));

    protected override int Compare(in DashStop a, in DashStop b)
    {
        int x = a.Position.X.CompareTo(b.Position.X);
        return x != 0 ? x : a.Position.Y.CompareTo(b.Position.Y);
    }

    protected override DashStop? Pasted(DashStop value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { Position = value.Position + offset };

    protected override List<DashStopDocument> List(LevelDocument level) => level.DashStops;

    protected override DashStopDocument ToDocument(in DashStop stop) => DashStopDocument.From(stop);

    protected override DashStop FromDocument(DashStopDocument stop) => stop.ToDashStop();
}
