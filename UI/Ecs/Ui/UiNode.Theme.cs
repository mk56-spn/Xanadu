
namespace Xanadu.Ecs;

/// <summary>
/// Themed variants: a control asks for a role and the theme in force gives the
/// colours, so a screen is restyled by its theme, not by editing each control.
/// </summary>
public readonly partial struct UiNode
{
    /// <summary>Gives this node and everything built under it <paramref name="roles"/>.</summary>
    public UiNode Theme(UiThemeRoles roles) => IsNull ? this : With(new UiThemeScope(roles));

    /// <summary>The theme in force here: this node's, else its nearest themed ancestor's.</summary>
    public UiThemeRoles Roles => IsNull ? UiThemes.Default : UiThemes.Of(_entity);

    /// <summary>The main action of a screen or group.</summary>
    public UiNode Primary() => Tone(Roles.Primary);

    /// <summary>Any other action.</summary>
    public UiNode Secondary() => Tone(Roles.Secondary);

    /// <summary>An action that should not draw the eye.</summary>
    public UiNode Quiet() => Tone(Roles.Quiet);

    /// <summary>An action that destroys something.</summary>
    public UiNode Danger() => Tone(Roles.Danger);

    /// <summary>One choice of a group: the chosen tone when <paramref name="on"/>, the secondary one otherwise.</summary>
    public UiNode Chosen(bool on) => Tone(on ? Roles.Chosen : Roles.Secondary);

    /// <summary>A surface tinted with category <paramref name="index"/> (<see cref="UiThemeRoles.Categories"/>), such as a card of one kind.</summary>
    public UiNode CategoryTone(int index) => Tone(Roles.CategoryTone(index));

    /// <summary>Filled with category <paramref name="index"/>'s own colour, such as a swatch or a stripe.</summary>
    public UiNode CategoryFill(int index) => Fill(Roles.Category(index));

    /// <summary>Text in category <paramref name="index"/>'s colour, such as a heading over that kind.</summary>
    public UiNode CategoryText(int index) => TextColor(Roles.Category(index));

    /// <summary>An opaque panel fill.</summary>
    public UiNode Solid() => Fill(Roles.Surface);

    /// <summary>The see-through backing behind controls over a busy view.</summary>
    public UiNode Backing() => Fill(Roles.Backing);

    /// <summary>Text in the theme's accent, for titles and highlights.</summary>
    public UiNode AccentText() => TextColor(Roles.Accent);

    /// <summary>Text in the theme's dim colour, for captions.</summary>
    public UiNode DimText() => TextColor(Roles.TextDim);

    /// <summary>Fills with a tone and colours the text on it; a button's hover follows the new fill.</summary>
    private UiNode Tone(UiTone tone)
    {
        if (IsNull)
            return this;

        Fill(tone.Fill);
        TextColor(tone.On);
        if (_entity.HasComponent<ButtonVisual>())
            _entity.AddComponent(ButtonVisual.Create(tone.Fill));
        return this;
    }
}
