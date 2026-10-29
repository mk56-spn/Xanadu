using System;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// Loads icon textures. A missing icon is not an error: the button shows its
/// fallback letters, so a tool can ship before every icon is drawn.
/// </summary>
public static class UiIcons
{
    /// <summary>
    /// Loads nothing until a running tool installs <see cref="FromResources"/>:
    /// the engine's resource loader takes the process down when there is no engine,
    /// which is the case in unit tests.
    /// </summary>
    public static Func<string, Texture2D?> Loader { get; set; } = _ => null;

    public static Texture2D? Load(string? path) =>
        string.IsNullOrEmpty(path) ? null : Loader(path);

    /// <summary>The engine's loader; a missing file gives no icon rather than an error.</summary>
    public static Texture2D? FromResources(string path) =>
        ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
}

public sealed partial class UiBuilder
{
    /// <summary>Icon size as a share of the button's shorter side.</summary>
    public const float IconScale = 0.6f;

    /// <summary>
    /// A button showing an icon, or <paramref name="fallback"/> (one or two
    /// letters) when the icon is missing. The button is a container so layout
    /// centres the image inside it; the image sits on its own entity because
    /// an image and a fill cannot share one canvas item.
    /// </summary>
    public Entity IconButton(
        string fallback,
        string? iconPath,
        Vector2 size,
        Color? color = null,
        int fontSize = UiTheme.FontSize)
    {
        Color normal = color ?? UiTheme.Button;
        Texture2D? texture = UiIcons.Load(iconPath);

        Container(Vector2.Zero, size, color: normal, takesInput: true);
        Entity button = Current;
        if (texture is null)
        {
            button.AddComponent(new Text(fallback));
            button.AddComponent(new TextStyle(fontSize, UiTheme.Text));
        }
        else
        {
            float side = Math.Min(size.X, size.Y) * IconScale;
            ImageEntity(Vector2.Zero, new Vector2(side, side), texture);
        }

        button.AddComponent(ButtonVisual.Create(normal));
        End();
        return button;
    }
}
