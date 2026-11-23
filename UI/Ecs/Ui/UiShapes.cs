using Xanadu.Core.Maths;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>The shapes <see cref="UiShapes"/> draws, numbered as sdf.slang's <c>sdf_shape</c> list.</summary>
public enum UiSdfShape
{
    Circle = 0,
    Box = 1,
    Ellipse = 2,
    Triangle = 3,
    Polygon = 4,
    Star = 5,
    Rhombus = 6,
    Cross = 7,
    Heart = 8,
    Ring = 9,
    Moon = 10,
    Squircle = 11,
    Egg = 12,
    Pie = 13,
    Trapezoid = 14,
    Vesica = 15,
}

/// <summary>
/// Small shapes drawn by the general purpose shape shader (sdf.slang) instead of
/// a glyph or an icon file: a mark beside a label that reads at a glance. Attach
/// the material with <see cref="UiMaterial"/> to a rect the size of the shape's box;
/// the rect's colour tints it.
/// </summary>
public static class UiShapes
{
    public const string Shader = "Assets/Shaders/Items/sdf.slang";

    /// <summary>
    /// A shape <paramref name="size"/> pixels across, filled with <paramref name="fill"/>,
    /// or only outlined <paramref name="outline"/> pixels wide when <paramref name="filled"/> is false.
    /// </summary>
    public static CanvasMaterial Material(UiSdfShape shape, Vector2 size, Color fill, bool filled = true, float outline = 1.5f)
    {
        var material = new CanvasMaterial(Shader);
        material.Set("xif_unit", 1f);
        material.Set("sdf_shape", (int)shape);
        material.Set("sdf_size", size);
        material.Set("sdf_shape_params", new Vector4(size.Y * 0.2f, 0.6f, 0, 0));
        material.Set("sdf_fill", filled ? fill : new Color(fill, 0f));
        material.Set("sdf_outline", fill);
        material.Set("sdf_outline_width", new Vector2(filled ? 0f : outline, 0f));
        material.Set("sdf_shadow", new Color(0, 0, 0, 0));
        material.Set("sdf_shadow_offset", Vector2.Zero);
        material.Set("sdf_shadow_blur", Vector2.Zero);
        return material;
    }
}
