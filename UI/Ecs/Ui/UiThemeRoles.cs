using Friflo.Engine.ECS;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>A fill and the colour of whatever sits on it, such as a button's label.</summary>
public readonly record struct UiTone(Color Fill, Color On);

/// <summary>
/// What colours mean on a screen, by job rather than by hue: the main action,
/// a secondary one, a panel, the backing behind controls over a busy view, and
/// the text colours. Controls ask for a role through a variant such as
/// <see cref="UiNode.Primary"/>, never for a colour, so restyling a screen is
/// changing its theme rather than every control on it.
/// </summary>
public sealed record UiThemeRoles
{
    public required string Name { get; init; }

    /// <summary>The main action of a screen or group.</summary>
    public required UiTone Primary { get; init; }

    /// <summary>Every other action.</summary>
    public required UiTone Secondary { get; init; }

    /// <summary>An action that should not draw the eye: no fill, dim text.</summary>
    public required UiTone Quiet { get; init; }

    /// <summary>An action that destroys something.</summary>
    public required UiTone Danger { get; init; }

    /// <summary>The one choice of a group that is on: a toggled view, a chosen sort.</summary>
    public required UiTone Chosen { get; init; }

    /// <summary>An opaque panel.</summary>
    public required Color Surface { get; init; }

    /// <summary>A see-through panel behind controls that sit over a busy view, for contrast.</summary>
    public required Color Backing { get; init; }

    public required Color Text { get; init; }
    public required Color TextDim { get; init; }

    /// <summary>Titles and highlighted text.</summary>
    public required Color Accent { get; init; }

    /// <summary>A recessed well, such as a meter's empty track.</summary>
    public required Color Inset { get; init; }

    /// <summary>A reading that is fine, such as a meter well under its limit.</summary>
    public required Color Good { get; init; }

    /// <summary>A reading getting close to a limit.</summary>
    public required Color Warn { get; init; }

    /// <summary>A reading at or past a limit.</summary>
    public required Color Bad { get; init; }

    /// <summary>
    /// Colours that only tell things apart, such as kinds of item in a chart.
    /// They mean nothing on their own: whoever shows the categories decides
    /// which is which, and asks for them by index (<see cref="Category"/>).
    /// </summary>
    public required Color[] Categories { get; init; }

    /// <summary>How far a category tints a surface: enough to tell kinds apart, not so much the text on it stops reading.</summary>
    public float CategoryTint { get; init; } = 0.24f;

    /// <summary>Category <paramref name="index"/>'s colour; indices past the end wrap round.</summary>
    public Color Category(int index) => Categories[((index % Categories.Length) + Categories.Length) % Categories.Length];

    /// <summary>A surface tinted with category <paramref name="index"/>, and the text that reads on it.</summary>
    public UiTone CategoryTone(int index) => new(Surface.Lerp(Category(index), CategoryTint), Text);
}

/// <summary>
/// The built-in themes. A subtree takes one with <see cref="UiNode.Theme"/>;
/// everything built inside it resolves its roles from the nearest themed
/// ancestor, and anything with none uses <see cref="Default"/>.
/// </summary>
public static class UiThemes
{
    private static readonly Color Clear = new(0f, 0f, 0f, 0f);

    /// <summary>The tools' look: muted blue actions on slate panels; bright blue is kept for what is toggled on.</summary>
    public static readonly UiThemeRoles Tools = new()
    {
        Name = "Tools",
        Primary = new UiTone(Palette.PrimaryMuted, Palette.Text),
        Secondary = new UiTone(Palette.Muted, Palette.Text),
        Quiet = new UiTone(Clear, Palette.TextDim),
        Danger = new UiTone(Palette.Danger, Palette.TextBright),
        Chosen = new UiTone(Palette.Primary, Palette.TextBright),
        Surface = Palette.Surface,
        Backing = new Color(0f, 0f, 0f, 0.3f),
        Text = Palette.Text,
        TextDim = Palette.TextDim,
        Accent = Palette.Accent,
        Inset = Palette.SurfaceDeep,
        Good = Palette.Success,
        Warn = Palette.Warning,
        Bad = Palette.DangerBright,
        Categories =
        [
            Palette.Gold, Palette.GoldLight, Palette.InfoSoft, Palette.Cyan,
            Palette.Violet, Palette.Lime, Palette.Pink, Palette.TextDim, Palette.BorderSubtle,
        ],
    };

    /// <summary>The menus, after song select: gold for the main action and titles, dark text on the gold.</summary>
    public static readonly UiThemeRoles Menu = Tools with
    {
        Name = "Menu",
        Primary = new UiTone(Palette.Gold, new Color("#141414")),
        Accent = Palette.Gold,
    };

    /// <summary>The play HUD: the menus' gold, with everything else on see-through black over the field.</summary>
    public static readonly UiThemeRoles Hud = Menu with
    {
        Name = "Hud",
        Secondary = new UiTone(Tools.Backing, Palette.Gold),
        Surface = Tools.Backing,
    };

    public static UiThemeRoles Default => Tools;

    /// <summary>The theme in force at <paramref name="entity"/>: its own, else its nearest themed ancestor's.</summary>
    public static UiThemeRoles Of(Entity entity)
    {
        for (Entity node = entity; !node.IsNull; node = node.Parent)
        {
            if (node.TryGetComponent(out UiThemeScope scope) && scope.Roles is not null)
                return scope.Roles;
        }

        return Default;
    }
}

/// <summary>Gives a node and everything under it a theme; see <see cref="UiThemes"/>.</summary>
public struct UiThemeScope(UiThemeRoles roles) : IComponent
{
    public UiThemeRoles Roles = roles;
}
