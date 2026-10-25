using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator;

/// <summary>Draws the rigid SVG groups of a package in the preview.</summary>
public interface IAttachmentVisuals
{
    /// <summary>Rebuilds the visuals for a new or restructured package.</summary>
    void Reload(XafPackage package);

    /// <summary>Places each attachment, in preview space offset by the origin.</summary>
    void Apply(Transform2D[] attachments, Vector2 origin);
}
