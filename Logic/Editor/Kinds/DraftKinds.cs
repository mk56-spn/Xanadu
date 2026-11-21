using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;

namespace Xanadu.Logic.Editor.Kinds;

/// <summary>Updrafts: untimed, placed and moved by their emitter, picked on the emitter or at their hover, pasted by offset.</summary>
public sealed class DraftItemKind : ItemKind<Draft, DraftDocument>
{
    private const float Slack = 14f;

    public override string Name => "draft";

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position)
    {
        Ref(entity).Position = position;
    }

    public override Rect2 Hull(Entity entity)
    {
        Draft draft = Get(entity);
        return new Rect2(new Vector2(draft.Position.X - Drafts.Diameter / 2f, draft.Position.Y - draft.Height), new Vector2(Drafts.Diameter, draft.Height));
    }

    public override bool Hit(Entity entity, Vector2 point, long playheadMs)
    {
        Draft draft = Get(entity);
        float dx = MathF.Abs(point.X - draft.Position.X);
        if (dx > Drafts.Diameter / 2f + Slack)
            return false;
        return MathF.Abs(point.Y - draft.Position.Y) <= Drafts.EmitterHeight / 2f + Slack ||
               MathF.Abs(point.Y - (draft.Position.Y - draft.Height)) <= Slack;
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.HasPoint(view.PlayfieldToScreen(Get(entity).Position));

    protected override int Compare(in Draft a, in Draft b)
    {
        int x = a.Position.X.CompareTo(b.Position.X);
        return x != 0 ? x : a.Position.Y.CompareTo(b.Position.Y);
    }

    protected override Draft? Pasted(Draft value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { Position = value.Position + offset };

    protected override List<DraftDocument> List(LevelDocument level) => level.Drafts;

    protected override DraftDocument ToDocument(in Draft draft) => DraftDocument.From(draft);

    protected override Draft FromDocument(DraftDocument draft) => draft.ToDraft();
}
