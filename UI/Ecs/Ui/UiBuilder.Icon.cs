using System;
using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>
/// Loads icon textures. A missing icon is not an error: the button shows its
/// fallback letters, so a tool can ship before every icon is drawn.
/// </summary>
public static class UiIcons
{
    /// <summary>
    /// Loads nothing until a running host installs <see cref="FromResources"/>:
    /// unit tests have no texture server to load into.
    /// </summary>
    public static Func<string, Handle> Loader { get; set; } = _ => default;

    /// <summary>The icon's texture id; an invalid one when there is no icon.</summary>
    public static Handle Load(string? path) =>
        string.IsNullOrEmpty(path) ? default : Loader(path);

    /// <summary>
    /// The running host's textures (<see cref="Xanadu.Systems.Canvas.TextureServer"/>);
    /// a missing file gives no icon rather than an error.
    /// </summary>
    public static Handle FromResources(string path) =>
        System.IO.File.Exists(Xanadu.Storage.GamePaths.Globalize(path))
            ? Xanadu.Systems.Canvas.TextureServer.Current.Load(path)
            : default;
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
        Handle texture = UiIcons.Load(iconPath);

        Container(Vector2.Zero, size, color: normal, takesInput: true);
        Entity button = Current;
        if (!texture.IsValid)
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
