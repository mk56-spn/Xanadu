using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Characters;

/// <summary>
/// Draws each player's character rig in place of the plain player box. The
/// art is scaled to the player's height and stands on the box's bottom edge;
/// visuals of players that are gone or lost their rig are released.
/// </summary>
public sealed class CharacterRigDrawSystem(Func<IAttachmentVisuals> createVisuals)
    : QuerySystem<CharacterRig, Player, Position>
{
    private readonly Dictionary<int, Instance> _instances = new();
    private readonly HashSet<int> _seen = new();

    protected override void OnUpdate()
    {
        _seen.Clear();
        foreach (Entity entity in Query.ToEntityList())
        {
            _seen.Add(entity.Id);
            AnimatorRuntime runtime = entity.GetComponent<CharacterRig>().Runtime;
            XafPackage package = runtime.Package;
            Transform2D[] bones = XafRig.WorldBones(package, runtime.Pose);
            Transform2D[] attachments = XafRig.WorldAttachments(package, runtime.Pose, bones);

            if (!_instances.TryGetValue(entity.Id, out Instance? instance) ||
                instance.Runtime != runtime)
            {
                instance?.Visuals.Release();
                IAttachmentVisuals visuals = createVisuals();
                visuals.Reload(package);
                instance = new Instance(runtime, visuals, visuals.Bounds(attachments));
                _instances[entity.Id] = instance;
            }

            if (instance.RigBounds.Size.Y <= 0)
            {
                entity.RemoveTag<CharacterRigVisible>();
                continue;
            }

            Vector2 size = entity.GetComponent<Player>().Size;
            Vector2 center = entity.GetComponent<Position>().Value;
            instance.Visuals.Apply(
                attachments,
                Placement(instance.RigBounds, center + new Vector2(0, size.Y / 2), size.Y));
            if (!entity.Tags.Has<CharacterRigVisible>())
                entity.AddTag<CharacterRigVisible>();
        }

        ReleaseUnseen();
    }

    /// <summary>
    /// Maps rig space to canvas space so the rig is <paramref name="height"/> tall
    /// with the bottom-centre of <paramref name="rigBounds"/> on <paramref name="feet"/>.
    /// </summary>
    public static Transform2D Placement(Rect2 rigBounds, Vector2 feet, float height)
    {
        float scale = height / rigBounds.Size.Y;
        var rigFeet = new Vector2(rigBounds.GetCenter().X, rigBounds.End.Y);
        return new Transform2D(0f, new Vector2(scale, scale), 0f, feet - rigFeet * scale);
    }

    private void ReleaseUnseen()
    {
        if (_instances.Count == _seen.Count)
            return;

        var gone = new List<int>();
        foreach (int id in _instances.Keys)
        {
            if (!_seen.Contains(id))
                gone.Add(id);
        }

        foreach (int id in gone)
        {
            _instances[id].Visuals.Release();
            _instances.Remove(id);
        }
    }

    private sealed record Instance(AnimatorRuntime Runtime, IAttachmentVisuals Visuals, Rect2 RigBounds);
}
