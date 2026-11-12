using Xanadu.Systems.Canvas;

namespace Xanadu.Ecs;

/// <summary>
/// A material that blurs whatever is drawn behind a translucent panel, so
/// text on it stays readable. It samples the screen at a high mip level, the
/// usual cheap blur, and lays the panel's own colour over it. Attach it with
/// <see cref="UiMaterial"/> to a shape or rect with a translucent fill.
/// </summary>
/// <remarks>Needs a screen read; the host draws it plain until step 6.</remarks>
public static class UiFrostedGlass
{
    public const string Shader = "Assets/Shaders/Ui/frosted_glass.slang";

    /// <summary>Higher <paramref name="lod"/> blurs more: each step halves the resolution.</summary>
    public static CanvasMaterial Create(float lod = 3f)
    {
        var material = new CanvasMaterial(Shader);
        material.Set("lod", lod);
        return material;
    }
}
