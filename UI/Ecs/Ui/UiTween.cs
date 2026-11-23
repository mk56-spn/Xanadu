using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Tweens;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// An entity's tweens: its target in <see cref="TweenPool.Shared"/>. Movement
/// applies after layout: <see cref="Xanadu.Core.Position"/> stays where the
/// container put it, and the tweens move what is drawn and hit from there, so
/// a move never fights a container. Children ride along with their parent.
/// </summary>
public struct UiTweens(TweenTarget target) : IComponent
{
    public TweenTarget Target = target;
}

/// <summary>The UI's own named tween layers (<see cref="Tween.Key"/>); a screen picks its own from <see cref="FirstFree"/> up.</summary>
public static class UiTweenKeys
{
    public const ushort Hover = 1;
    public const ushort Flash = 2;
    public const ushort FirstFree = 100;
}

/// <summary>
/// Tweens on UI entities: a <see cref="Clip"/> (the shader library's, in C#) playing
/// on an entity. Movement stacks: a slide started while another runs adds to it.
/// </summary>
public static class UiTween
{
    private static readonly ConditionalWeakTable<EntityStore, object> Watched = [];

    public static TweenPool Pool => TweenPool.Shared;

    /// <summary>The entity's target, made on first use. Adds a component, so not from inside a query over the entity.</summary>
    public static TweenTarget Target(Entity entity)
    {
        if (entity.TryGetComponent(out UiTweens tweens))
            return tweens.Target;
        TweenTarget target = Make(entity.Store);
        entity.AddComponent(new UiTweens(target));
        return target;
    }

    /// <summary>
    /// The entity's target, made on first use, with the component added through
    /// <paramref name="buffer"/>, for systems inside a query.
    /// </summary>
    public static TweenTarget Target(Entity entity, CommandBuffer buffer)
    {
        if (entity.TryGetComponent(out UiTweens tweens))
            return tweens.Target;
        TweenTarget target = Make(entity.Store);
        buffer.AddComponent(entity.Id, new UiTweens(target));
        return target;
    }

    /// <summary>The entity's target, or <see cref="TweenTarget.None"/> when nothing has tweened it.</summary>
    public static TweenTarget Of(Entity entity) =>
        entity.TryGetComponent(out UiTweens tweens) ? tweens.Target : TweenTarget.None;

    public static void Play(Entity entity, in Tween tween) => Pool.Play(Target(entity), tween);

    /// <summary>
    /// Slides in from <paramref name="from"/> (relative to where it rests) to where
    /// it rests over <paramref name="clip"/>; until the clip starts it waits at
    /// <paramref name="from"/>. <c>UiTween.MoveIn(row, new(-260, 0), Clip.FromNow(0.6f).Eased(EasePreset.CubicOut).Staggered(i, 0.1f))</c>.
    /// </summary>
    public static void MoveIn(Entity entity, Vector2 from, Clip clip) => Play(entity, Tween.MoveIn(from, clip));

    /// <summary>Slides by <paramref name="by"/> over <paramref name="clip"/> and stays there (its layout place doesn't move).</summary>
    public static void MoveBy(Entity entity, Vector2 by, Clip clip) => Play(entity, Tween.MoveBy(by, clip));

    /// <summary>Drops every tween on the entity: it shows where it rests, in its own colour.</summary>
    public static void Clear(Entity entity) => Pool.Clear(Of(entity));

    /// <summary>The weight the named layer applies now; 0 when it isn't playing.</summary>
    public static float Weight(Entity entity, ushort key) => Pool.Weight(Of(entity), key);

    /// <summary>How the entity's tweens move it now, in its own space.</summary>
    public static TweenPose Pose(Entity entity)
    {
        TweenTarget target = Of(entity);
        return target.IsNone ? TweenPose.Identity : Pool.Pose(target);
    }

    private static TweenTarget Make(EntityStore store)
    {
        if (!Watched.TryGetValue(store, out _))
        {
            Watched.Add(store, new object());
            // A deleted entity hands its range back; its id may be reused.
            store.OnEntityDelete += deleted =>
            {
                if (deleted.Entity.TryGetComponent(out UiTweens gone))
                    Pool.Release(gone.Target);
            };
        }

        return Pool.Create();
    }
}
