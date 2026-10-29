using Godot;

namespace Xanadu.Animation;

/// <summary>Draws the rigid SVG groups of a package.</summary>
public interface IAttachmentVisuals
{
    /// <summary>Rebuilds the visuals for a new or restructured package.</summary>
    void Reload(XafPackage package);

    /// <summary>
    /// Places each attachment: rig-space transforms mapped through
    /// <paramref name="placement"/> (rig space to canvas space).
    /// </summary>
    void Apply(Transform2D[] attachments, Transform2D placement);

    /// <summary>
    /// Places the art of each chain that has any, one piece per segment, along
    /// the simulated points. Chains are drawn over the attachments.
    /// </summary>
    void ApplyChains(XafPackage package, XafChainSimulation simulation, Transform2D placement) { }

    /// <summary>
    /// Draws each cloth as a filled sheet through its simulated points, on the
    /// cloth's own layer among the attachments.
    /// </summary>
    void ApplyCloths(XafPackage package, XafClothSimulation simulation, Transform2D placement) { }

    /// <summary>
    /// Places each effect at its rig-space transform and shows it at its
    /// pose's alpha; an effect the pose does not show is hidden.
    /// </summary>
    void ApplyEffects(XafPose pose, Transform2D[] effects, Transform2D placement) { }

    /// <summary>Rebuilds only the effects, after their items were edited; the default rebuilds everything.</summary>
    void ReloadEffects(XafPackage package) => Reload(package);

    /// <summary>Rig-space bounds of the loaded art at the given attachment transforms; empty when nothing loaded.</summary>
    Rect2 Bounds(Transform2D[] attachments) => new();

    /// <summary>Frees the visuals' canvas items.</summary>
    void Release() { }
}
