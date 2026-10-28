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

    /// <summary>Rig-space bounds of the loaded art at the given attachment transforms; empty when nothing loaded.</summary>
    Rect2 Bounds(Transform2D[] attachments) => new();

    /// <summary>Frees the visuals' canvas items.</summary>
    void Release() { }
}
