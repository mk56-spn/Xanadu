using Xanadu.Systems.Canvas;

namespace Xanadu.Ecs;

/// <summary>
/// A material that tints an entity's white text along a line, in the entity's
/// local space. Attach it with <see cref="UiMaterial"/> to an entity with a
/// transparent fill, so only the glyphs are recoloured. The text colour still
/// supplies the alpha, so fades keep working.
/// </summary>
public static class UiTextGradient
{
    public const string Shader = "res://Assets/Shaders/Ui/text_gradient.slang";

    /// <summary>
    /// Blends from <paramref name="from"/> at <paramref name="start"/> to
    /// <paramref name="to"/> at <paramref name="end"/>, clamped beyond either end.
    /// </summary>
    public static CanvasMaterial Create(Color from, Color to, Vector2 start, Vector2 end)
    {
        var material = new CanvasMaterial(Shader);
        material.Set("color_from", from);
        material.Set("color_to", to);
        material.Set("point_from", start);
        material.Set("point_to", end);
        return material;
    }
}
