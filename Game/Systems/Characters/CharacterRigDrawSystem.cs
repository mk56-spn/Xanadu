using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Characters;

/// <summary>
/// Draws each player's character rig in place of the plain player box. The
/// art is scaled to the player's height and stands on the box's bottom edge;
/// visuals of players that are gone or lost their rig are released. The rig's
/// Verlet chains, such as hair, are simulated in rig space from its animated
/// bones, so they swing with the animation. With <paramref name="ghosts"/>, a
/// character that hits a note also leaves a fading copy of its art behind.
/// </summary>
public sealed class CharacterRigDrawSystem(
    Func<IAttachmentVisuals> createVisuals,
    CharacterRigGhosts? ghosts = null)
    : QuerySystem<CharacterRig, Player, Position>
{
    private readonly Dictionary<int, Instance> _instances = new();
    private readonly HashSet<int> _seen = new();

    protected override void OnUpdate()
    {
        _seen.Clear();
        ghosts?.Begin(Query.Store);
        foreach (Entity entity in Query.ToEntityList())
        {
            if (entity.Tags.Has<PlayerShattered>())
            {
                if (_instances.Remove(entity.Id, out Instance? gone))
                    gone.Visuals.Release();
                entity.RemoveTag<CharacterRigVisible>();
                continue;
            }

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
                instance = new Instance(
                    runtime, visuals, BodyCentred(visuals.Bounds(attachments), package, bones), new XafChainSimulation(), new XafClothSimulation());
                _instances[entity.Id] = instance;
            }

            if (instance.RigBounds.Size.Y <= 0)
            {
                entity.RemoveTag<CharacterRigVisible>();
                continue;
            }

            Vector2 size = entity.GetComponent<Player>().Size;
            Vector2 center = PlayerSmoothing.DrawnPosition(entity);
            if (entity.TryGetComponent(out PlayCamera camera))
                center += camera.Subpixel;
            bool faceLeft = entity.TryGetComponent(out CharacterFacing facing) && facing.Left;
            Transform2D placement = Placement(instance.RigBounds, center + new Vector2(0, size.Y / 2), Player.ArtHeight(size.Y), faceLeft);
            MeasureLift(entity, instance.RigBounds, instance.Visuals.Bounds(attachments), Player.ArtHeight(size.Y));
            instance.Visuals.Apply(attachments, placement);
            instance.Visuals.ApplyStates(package, runtime.Pose, bones, attachments, placement);
            instance.Chains.Update(package, bones, Tick.deltaTime);
            instance.Visuals.ApplyChains(package, instance.Chains, placement);
            instance.Cloths.Update(package, bones, Tick.deltaTime);
            instance.Visuals.ApplyCloths(package, instance.Cloths, placement);
            instance.Visuals.ApplyEffects(runtime.Pose, XafRig.WorldEffects(package, runtime.Pose, bones), placement);
            ghosts?.Capture(
                entity, package, runtime.Pose, bones, attachments,
                instance.Chains, instance.Cloths, instance.RigBounds, faceLeft);
            if (!entity.Tags.Has<CharacterRigVisible>())
                entity.AddTag<CharacterRigVisible>();
        }

        ReleaseUnseen();
        ghosts?.End(Query.Store);
    }

    /// <summary>
    /// The art's bounds, slid sideways so their centre is the root bone: hair,
    /// a weapon or a reaching arm make the bounds lopsided, and the art is
    /// placed and mirrored about their centre, so without this the body would
    /// sit off the hitbox and jump across it whenever the character turns.
    /// </summary>
    public static Rect2 BodyCentred(Rect2 bounds, XafPackage package, Transform2D[] bones)
    {
        int root = package.Skeleton.Bones.FindIndex(bone => bone.Parent < 0);
        if (root < 0 || root >= bones.Length)
            return bounds;

        float x = bones[root].Origin.X;
        return new Rect2(new Vector2(x - bounds.Size.X / 2, bounds.Position.Y), bounds.Size);
    }

    /// <summary>
    /// Records how far the pose has lifted the art's feet above where they stand
    /// at rest, in screen pixels, for the body to fit itself to.
    /// </summary>
    private static void MeasureLift(Entity entity, Rect2 rest, Rect2 posed, float height)
    {
        float lift = Math.Max(0f, (rest.End.Y - posed.End.Y) * height / rest.Size.Y);
        if (entity.HasComponent<PlayerTuck>())
            entity.GetComponent<PlayerTuck>().Lift = lift;
        else
            entity.AddComponent(new PlayerTuck { Lift = lift });
    }

    /// <summary>
    /// Maps rig space to canvas space so the rig is <paramref name="height"/> tall
    /// with the bottom-centre of <paramref name="rigBounds"/> on <paramref name="feet"/>,
    /// mirrored about that point when <paramref name="flip"/> is set.
    /// </summary>
    public static Transform2D Placement(Rect2 rigBounds, Vector2 feet, float height, bool flip = false)
    {
        float scale = height / rigBounds.Size.Y;
        var rigFeet = new Vector2(rigBounds.GetCenter().X, rigBounds.End.Y);
        var scaleXY = new Vector2(flip ? -scale : scale, scale);
        return new Transform2D(0f, scaleXY, 0f, feet - rigFeet * scaleXY);
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

    private sealed record Instance(
        AnimatorRuntime Runtime,
        IAttachmentVisuals Visuals,
        Rect2 RigBounds,
        XafChainSimulation Chains,
        XafClothSimulation Cloths);
}
