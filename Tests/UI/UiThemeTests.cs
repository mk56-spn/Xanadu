using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiThemeTests
{
    private EntityStore _store = null!;
    private UiNode _layer;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _layer = UiNode.Layer(_store, default, 0, new FakeVisualRidFactory());
    }

    private static Color FillOf(UiNode node) => node.Entity.GetComponent<FillColor>().Value;

    private static Color TextOf(UiNode node) => node.Entity.GetComponent<TextStyle>().Color;

    [Test]
    public void WithNoThemeAnythingUsesTheDefault()
    {
        UiNode button = _layer.Button("GO").Primary();

        Assert.That(button.Roles, Is.SameAs(UiThemes.Default));
        Assert.That(FillOf(button), Is.EqualTo(UiThemes.Default.Primary.Fill));
    }

    [Test]
    public void AThemeReachesEverythingBuiltUnderIt()
    {
        UiNode column = _layer.Theme(UiThemes.Menu).Column();
        UiNode row = column.Row();
        UiNode button = row.Button("PLAY").Primary();

        Assert.That(button.Roles, Is.SameAs(UiThemes.Menu));
        Assert.That(FillOf(button), Is.EqualTo(Palette.Gold));
        Assert.That(TextOf(button), Is.EqualTo(UiThemes.Menu.Primary.On));
    }

    [Test]
    public void TheNearestThemeWins()
    {
        UiNode inner = _layer.Theme(UiThemes.Menu).Column().Theme(UiThemes.Hud);

        Assert.That(inner.Button("PAUSE").Secondary().Roles, Is.SameAs(UiThemes.Hud));
        Assert.That(TextOf(inner.Button("PAUSE").Secondary()), Is.EqualTo(Palette.Gold));
    }

    [Test]
    public void AButtonsHoverFollowsItsVariant()
    {
        UiNode button = _layer.Theme(UiThemes.Menu).Button("PLAY", color: Colors.Red).Primary();

        Assert.That(button.Entity.GetComponent<ButtonVisual>().Normal, Is.EqualTo(Palette.Gold));
    }

    [Test]
    public void SurfacesAndTextTakeTheirRoles()
    {
        UiNode hud = _layer.Theme(UiThemes.Hud);

        Assert.That(FillOf(hud.Rect(Colors.Red).Backing()), Is.EqualTo(UiThemes.Hud.Backing));
        Assert.That(FillOf(hud.Rect(Colors.Red).Solid()), Is.EqualTo(UiThemes.Hud.Surface));
        Assert.That(TextOf(hud.Text("TITLE").AccentText()), Is.EqualTo(Palette.Gold));
        Assert.That(TextOf(hud.Text("caption").DimText()), Is.EqualTo(UiThemes.Hud.TextDim));
    }

    [Test]
    public void ThemesDifferOnlyWhereTheyMeanTo()
    {
        Assert.That(UiThemes.Menu.Secondary, Is.EqualTo(UiThemes.Tools.Secondary));
        Assert.That(UiThemes.Hud.Primary, Is.EqualTo(UiThemes.Menu.Primary));
        Assert.That(UiThemes.Hud.Backing.A, Is.EqualTo(0.3f).Within(0.001f));
    }
}
