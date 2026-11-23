using Xanadu.Core.Items;
using Xanadu.Model.Animation;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Animator;

/// <summary>
/// The rig's art in the animator: drawn by <see cref="XifDrawer"/> into the
/// art canvas each frame, the same way play draws it, with the parts'
/// particle streams mounted over it (a held part's over the held canvas).
/// </summary>
public sealed class AnimatorArt(Handle canvas, Handle held = default)
{
    private readonly XifDrawer _drawer = new();
    private readonly RigParticleStreams _particles = new(canvas, held);

    /// <summary>After the package or its art changed: art is edited in place, so nothing kept from before holds.</summary>
    public void Reload(RigPackage package)
    {
        _drawer.Forget();
        _held.Forget();
        _particles.Reload(package);
    }

    public void Draw(IRectCanvas into, RigPackage package, RigPose pose, Transform2D[] bones, Transform2D[] attachments,
        RigChainSimulation chains, RigClothSimulation cloths, Transform2D placement, int skip = -1)
    {
        into.Clear(canvas);
        _drawer.DrawRig(into, canvas, package, pose, bones, attachments, chains, cloths, placement, placement.X.Length(), XifTint.Plain,
            new XifDrawer.PartFilter(Skip: skip));
        _particles.Apply(pose, attachments, placement, skip);
    }

    private readonly XifDrawer _held = new();

    /// <summary>
    /// Draws one part alone on <paramref name="target"/>, as the XIF editor
    /// holding it shows it: above the editor's dimming, through its shader.
    /// An index below zero only clears it.
    /// </summary>
    public void DrawHeld(IRectCanvas into, Handle target, RigPackage package, RigPose pose, Transform2D[] bones,
        Transform2D[] attachments, Transform2D placement, int part)
    {
        into.Clear(target);
        if (part < 0)
            return;
        _held.DrawRig(into, target, package, pose, bones, attachments, null, null, placement, placement.X.Length(), XifTint.Plain,
            new XifDrawer.PartFilter(Only: part));
    }

    /// <summary>
    /// Draws the node the XIF editor holds in its shape box, alone on
    /// <paramref name="target"/> at <paramref name="toScreen"/> (where the box
    /// shows it), through its shader: chain and cloth art laid flat, a skinned
    /// part unbent.
    /// </summary>
    public void DrawBoxed(IRectCanvas into, Handle target, Xif item, Transform2D toScreen)
    {
        into.Clear(target);
        _held.DrawNode(into, target, item, toScreen, toScreen.X.Length(), XifTint.Plain);
    }

    /// <summary>The target-space points and triangles a skinned part was last drawn with.</summary>
    public bool TryMesh(int attachment, out Vector2[] points, out int[] triangles) =>
        _drawer.TryMesh(attachment, out points, out triangles);

    public void Release() => _particles.Release();
}
