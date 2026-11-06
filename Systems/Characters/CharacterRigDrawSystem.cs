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
/// visuals of players that are gone or lost their rig are released. The rig's
/// Verlet chains, such as hair, are simulated in rig space from its animated
/// bones, so they swing with the animation. With <paramref name="ghosts"/>, a
/// character that hits a note also leaves a fading copy of its art behind.
/// In play, a character that has waited a beat since its last hit fades to
/// black but for its hands, which flash white on the beat (<see cref="CharacterPresenceRules"/>).
/// </summary>
public sealed class CharacterRigDrawSystem(
    Func<IAttachmentVisuals> createVisuals,
    CharacterRigGhosts? ghosts = null)
    : QuerySystem<CharacterRig, Player, Position>
{
    private readonly Dictionary<int, Instance> _instances = new();
    private readonly HashSet<int> _seen = new();
    private readonly (int Attachment, float Light)[] _kept = new (int, float)[2];

    protected override void OnUpdate()
    {
        _seen.Clear();
        ghosts?.Begin(Query.Store);
        Playhead? playhead = PlayPlayhead(Query.Store);
        foreach (Entity entity in Query.ToEntityList())
        {
            if (entity.Tags.Has<SplitlineAbsent>() ||
                entity.Tags.Has<PlayerShattered>())
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
                    runtime, visuals, visuals.Bounds(attachments), new XafChainSimulation(), new XafClothSimulation(),
                    CharacterPresenceRules.AttachmentOn(package, CharacterPresenceRules.LeftHand),
                    CharacterPresenceRules.AttachmentOn(package, CharacterPresenceRules.RightHand));
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
            instance.Visuals.Apply(attachments, placement);
            instance.Visuals.ApplyStates(package, runtime.Pose, bones, attachments, placement);
            instance.Chains.Update(package, bones, Tick.deltaTime);
            instance.Visuals.ApplyChains(package, instance.Chains, placement);
            instance.Cloths.Update(package, bones, Tick.deltaTime);
            instance.Visuals.ApplyCloths(package, instance.Cloths, placement);
            instance.Visuals.ApplyEffects(runtime.Pose, XafRig.WorldEffects(package, runtime.Pose, bones), placement);
            ApplyPresence(entity, instance, playhead);
            ghosts?.Capture(
                entity, package, runtime.Pose, bones, attachments,
                instance.Chains, instance.Cloths, instance.RigBounds, faceLeft);
            if (!entity.Tags.Has<CharacterRigVisible>())
                entity.AddTag<CharacterRigVisible>();
        }

        ReleaseUnseen();
        ghosts?.End(Query.Store);
    }

    /// <summary>The playhead while a song is being played; null in the editor and anywhere else, which keeps characters whole.</summary>
    private static Playhead? PlayPlayhead(EntityStore store)
    {
        if (store.Query<PlaySession>().Count == 0)
            return null;
        foreach (Entity entity in store.Query<Playhead>().ToEntityList())
            return entity.GetComponent<Playhead>();
        return null;
    }

    private void ApplyPresence(Entity entity, Instance instance, Playhead? playhead)
    {
        CharacterPresenceRules.Look look = CharacterPresenceRules.Look.Present;
        if (playhead is { } head)
        {
            if (!entity.HasComponent<CharacterPresence>())
                entity.AddComponent(new CharacterPresence(head.TimeMs));
            ref CharacterPresence presence = ref entity.GetComponent<CharacterPresence>();
            // A seek back past the last hit starts the wait again from there.
            if (head.TimeMs < presence.LastHitMs)
                presence.LastHitMs = head.TimeMs;
            look = CharacterPresenceRules.At(head.TimeMs - presence.LastHitMs, head.TimeMs, head.Bpm, head.OffsetMs);
        }

        int count = 0;
        if (instance.LeftHand >= 0)
            _kept[count++] = (instance.LeftHand, look.LeftLight);
        if (instance.RightHand >= 0)
            _kept[count++] = (instance.RightHand, look.RightLight);
        instance.Visuals.ApplyPresence(look.BodyShade, _kept.AsSpan(0, count));
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
        XafClothSimulation Cloths,
        int LeftHand,
        int RightHand);
}
