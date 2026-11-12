using System;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Particles;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

/// <summary>
/// The shared particle editor against a fake host, as any tool would host it:
/// shown only with a selection, choosing effects through the host, and edits
/// to the effect going to the shared library.
/// </summary>
[TestFixture]
public sealed class ParticleEditorTests : EcsSystemTest
{
    private static readonly Vector2 Home = new(400, 500);

    private FakeHost _host = null!;
    private Entity _panel;
    private XpfLibrary _library = null!;

    private sealed class FakeHost : IParticleEditorHost
    {
        public string? Particle { get; set; }
        public string Title => "Emitter";
        public int Uses { get; private set; }

        public void Use(string name)
        {
            Particle = name;
            Uses++;
        }
    }

    protected override void OnEcsSetUp()
    {
        _library = new XpfLibrary
        {
            Particles = { new XpfParticle { Name = "dust" }, new XpfParticle { Name = "sparks" } },
        };
        ParticleLibrary.UseForTests(_library);
        _host = new FakeHost();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new ParticleEditorSystem());
        UiNode layer = UiNode.Layer(Store, default, 5, new FakeVisualRidFactory());
        _panel = ParticleEditorPanel.Build(layer, Home, _host);
        Frame(2);
    }

    [TearDown]
    public void ForgetLibrary() => ParticleLibrary.UseForTests(null);

    private void Click(ParticleEditorActionKind kind)
    {
        foreach ((Entity button, ParticleEditorActionKind buttonKind, _) in UiCommand.All<ParticleEditorActionKind>(Store))
        {
            if (buttonKind != kind)
                continue;
            button.AddTag<Pressed>();
            Frame();
            button.RemoveTag<Pressed>();
            Frame();
            return;
        }

        throw new InvalidOperationException(kind.ToString());
    }

    private Entity SliderOf(XpfField field)
    {
        foreach (Entity track in Store.Query<ParticleEditorSlider>().ToEntityList())
        {
            if (track.GetComponent<ParticleEditorSlider>().Field == field)
                return track;
        }

        throw new InvalidOperationException(field.ToString());
    }

    [Test]
    public void ThePanelHidesWithNothingSelectedAndShowsAtHomeWithASelection()
    {
        Assert.That(_panel.Tags.Has<Disabled>(), Is.True);

        _host.Particle = "dust";
        Frame();

        Assert.That(_panel.Tags.Has<Disabled>(), Is.False);
        Assert.That(_panel.GetComponent<Position>().Value, Is.EqualTo(Home));
    }

    [Test]
    public void TheArrowsPickTheNextEffectThroughTheHost()
    {
        _host.Particle = "dust";
        Frame();

        Click(ParticleEditorActionKind.Next);

        Assert.That(_host.Particle, Is.EqualTo("sparks"));
        Assert.That(_host.Uses, Is.EqualTo(1));
    }

    [Test]
    public void DraggingASliderChangesTheLibrarysEffect()
    {
        _host.Particle = "sparks";
        Frame();
        int version = ParticleLibrary.Version;

        SliderOf(XpfField.Amount).GetComponent<UiSlider>().Value = 120f;
        Frame();

        Assert.That(_library.Find("sparks")!.Amount, Is.EqualTo(120));
        Assert.That(_library.Find("dust")!.Amount, Is.EqualTo(16), "only the selected effect");
        Assert.That(ParticleLibrary.Unsaved, Is.True);
        Assert.That(ParticleLibrary.Version, Is.GreaterThan(version));
    }

    [Test]
    public void SlidersFollowTheSelectedEffectWithoutWritingIt()
    {
        _library.Find("dust")!.Lifetime = 2f;
        _host.Particle = "sparks";
        Frame();
        _host.Particle = "dust";
        Frame();

        Assert.That(SliderOf(XpfField.Lifetime).GetComponent<UiSlider>().Value, Is.EqualTo(2f));
        Assert.That(_library.Find("sparks")!.Lifetime, Is.EqualTo(0.6f), "switching does not copy values across");
        Assert.That(ParticleLibrary.Unsaved, Is.False);
    }

    [Test]
    public void CopyPlaysACopySoOtherUsersKeepTheOriginal()
    {
        _host.Particle = "dust";
        Frame();

        Click(ParticleEditorActionKind.Copy);

        Assert.That(_host.Particle, Is.EqualTo("dust-2"));
        Assert.That(_library.Find("dust-2"), Is.Not.Null);
        Assert.That(_library.Find("dust"), Is.Not.Null);
    }

    [Test]
    public void TheLookButtonsCycleTheEffectsLook()
    {
        _host.Particle = "dust";
        Frame();

        Click(ParticleEditorActionKind.Texture);
        Click(ParticleEditorActionKind.ScaleCurve);

        XpfParticle dust = _library.Find("dust")!;
        Assert.That(dust.Texture, Is.EqualTo(XpfTexture.NoteGlyphs));
        Assert.That(XpfEditing.ScalePreset(dust), Is.EqualTo(XpfScalePreset.Shrink));
    }
}
