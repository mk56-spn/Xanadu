using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Systems.Canvas;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Systems.Characters;

/// <summary>
/// The see-through copies of a rigged character that <see cref="HitGhost"/>s
/// ask for. Each copy is the character's art posed once, on the frame its ghost
/// appears, under a canvas group of its own so it fades as one picture rather
/// than its overlapping parts showing through each other. Copies are pooled and
/// keep their rasterised art, so a hit does not load the rig again.
/// </summary>
/// <param name="layer">The canvas item the copies hang from, drawn under the live characters.</param>
/// <param name="createVisuals">Makes the art for one copy under the given canvas item.</param>
public sealed class CharacterRigGhosts(Handle layer, Func<Handle, IAttachmentVisuals> createVisuals)
{
    /// <summary>The most copies on screen at once; past it the oldest is reused.</summary>
    public const int MaxCopies = 8;

    /// <summary>How far round the art the canvas group reaches, in pixels, so nothing at its edge is clipped.</summary>
    private const float GroupMargin = 10f;

    private readonly Dictionary<int, Copy> _live = new();
    private readonly List<Copy> _free = new();
    private readonly List<Entity> _fresh = new();
    private long _captures;

    /// <summary>Collects the ghosts that appeared since the last frame, before the characters are drawn.</summary>
    public void Begin(EntityStore entities)
    {
        _fresh.Clear();
        foreach (Entity ghost in entities.Query<HitGhost>().ToEntityList())
        {
            if (!_live.ContainsKey(ghost.Id))
                _fresh.Add(ghost);
        }
    }

    /// <summary>
    /// Poses a copy for each new ghost of <paramref name="character"/>, as the
    /// character is drawn this frame, at the spot the ghost was left.
    /// </summary>
    public void Capture(
        Entity character,
        XafPackage package,
        XafPose pose,
        Transform2D[] bones,
        Transform2D[] attachments,
        XafChainSimulation chains,
        XafClothSimulation cloths,
        Rect2 rigBounds,
        bool faceLeft)
    {
        foreach (Entity entity in _fresh)
        {
            HitGhost ghost = entity.GetComponent<HitGhost>();
            if (ghost.Player.Id != character.Id)
                continue;

            Copy copy = Acquire(package);
            Transform2D placement = CharacterRigDrawSystem.Placement(
                rigBounds,
                ghost.Center + new Vector2(0, ghost.Size.Y / 2),
                Player.ArtHeight(ghost.Size.Y),
                faceLeft);
            copy.Visuals.Apply(attachments, placement);
            copy.Visuals.ApplyStates(package, pose, bones, attachments, placement);
            copy.Visuals.ApplyChains(package, chains, placement);
            copy.Visuals.ApplyCloths(package, cloths, placement);
            copy.Visuals.ApplyEffects(pose, XafRig.WorldEffects(package, pose, bones), placement);
            copy.Order = _captures++;
            copy.Left = ghost.Center;
            CanvasServer.Current.SetTransform(copy.Root, Transform2D.Identity);
            SetAlpha(copy, ghost.Alpha);
            CanvasServer.Current.SetVisible(copy.Root, true);
            _live[entity.Id] = copy;
            entity.AddTag<HitGhostRigged>();
        }
    }

    /// <summary>Fades every copy to its ghost's opacity and puts away those whose ghost is gone.</summary>
    public void End(EntityStore entities)
    {
        List<int>? gone = null;
        foreach ((int id, Copy copy) in _live)
        {
            Entity ghost = entities.GetEntityById(id);
            if (ghost.IsNull || !ghost.TryGetComponent(out HitGhost state))
            {
                (gone ??= new List<int>()).Add(id);
                continue;
            }

            SetAlpha(copy, state.Alpha);
            // A launched copy flies off from where it was posed.
            if (state.Center != copy.Left)
                CanvasServer.Current.SetTransform(copy.Root, new Transform2D(new Vector2(1, 0), new Vector2(0, 1), state.Center - copy.Left));
        }

        if (gone is null)
            return;

        foreach (int id in gone)
        {
            Copy copy = _live[id];
            _live.Remove(id);
            CanvasServer.Current.SetVisible(copy.Root, false);
            _free.Add(copy);
        }
    }

    private Copy Acquire(XafPackage package)
    {
        Copy? copy = TakeFree(package);
        if (copy is null && _live.Count >= MaxCopies)
            copy = TakeOldest();

        if (copy is null)
        {
            Handle root = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(root, layer);
            CanvasServer.Current.SetCanvasGroup(root, GroupMargin, fitEmpty: false, GroupMargin);
            copy = new Copy(root, createVisuals(root));
        }

        if (copy.Package != package)
        {
            copy.Visuals.Reload(package);
            copy.Package = package;
        }

        return copy;
    }

    private Copy? TakeFree(XafPackage package)
    {
        if (_free.Count == 0)
            return null;

        int index = _free.FindIndex(copy => copy.Package == package);
        if (index < 0)
            index = _free.Count - 1;

        Copy copy = _free[index];
        _free.RemoveAt(index);
        return copy;
    }

    private Copy? TakeOldest()
    {
        int oldestId = -1;
        long oldest = long.MaxValue;
        foreach ((int id, Copy copy) in _live)
        {
            if (copy.Order < oldest)
            {
                oldest = copy.Order;
                oldestId = id;
            }
        }

        if (oldestId < 0)
            return null;

        Copy taken = _live[oldestId];
        _live.Remove(oldestId);
        return taken;
    }

    private static void SetAlpha(Copy copy, float alpha) =>
        CanvasServer.Current.SetModulate(copy.Root, new Color(1f, 1f, 1f, alpha));

    private sealed class Copy(Handle root, IAttachmentVisuals visuals)
    {
        public Handle Root { get; } = root;
        public IAttachmentVisuals Visuals { get; } = visuals;
        public XafPackage? Package { get; set; }
        public long Order { get; set; }

        /// <summary>Where its ghost was when it was posed.</summary>
        public Vector2 Left { get; set; }
    }
}
