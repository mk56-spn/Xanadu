using Xanadu.Tests.Support.Fakes;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Core.Particles;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Tests;

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

    /// <summary>Holds the selected thing's settings, as a part or emitter would, and counts what the editor asked of it.</summary>
    private sealed class FakeHost : IParticleEditorHost
    {
        public XpfParticle? Particle { get; set; }
        public string Title => "Emitter";
        public int Uses { get; private set; }
        public int Edits { get; private set; }
        public int Gestures { get; private set; }
        public int Changes { get; private set; }

        public void Use(XpfParticle settings)
        {
            Particle = settings;
            Uses++;
        }

        public void Edit(Action change)
        {
            change();
            Edits++;
        }

        public void BeginGesture() => Gestures++;

        public void EndGesture()
        {
        }

        public void Changed() => Changes++;
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

        _host.Particle = new XpfParticle { Name = "mine" };
        Frame();

        Assert.That(_panel.Tags.Has<Disabled>(), Is.False);
        Assert.That(_panel.GetComponent<Position>().Value, Is.EqualTo(Home));
    }

    [Test]
    public void TheArrowsLoadTheNextPresetAsACopyThroughTheHost()
    {
        _host.Particle = _library.Find("dust")!.Clone();
        Frame();

        Click(ParticleEditorActionKind.Next);

        Assert.That(_host.Particle!.Name, Is.EqualTo("sparks"));
        Assert.That(_host.Particle, Is.Not.SameAs(_library.Find("sparks")), "the preset itself stays as stored");
        Assert.That(_host.Uses, Is.EqualTo(1));
    }

    [Test]
    public void DraggingASliderChangesTheSelectedSettingsAsOneGesture()
    {
        _host.Particle = _library.Find("sparks")!.Clone();
        Frame();

        SliderOf(XpfField.Amount).GetComponent<UiSlider>().Value = 120f;
        Frame();

        Assert.That(_host.Particle.Amount, Is.EqualTo(120));
        Assert.That(_library.Find("sparks")!.Amount, Is.Not.EqualTo(120), "the preset is not edited");
        Assert.That(_host.Gestures, Is.EqualTo(1));
        Assert.That(_host.Changes, Is.GreaterThan(0));
    }

    [Test]
    public void SlidersFollowTheSelectionWithoutWritingIt()
    {
        var slow = new XpfParticle { Name = "slow", Lifetime = 2f };
        var quick = new XpfParticle { Name = "quick", Lifetime = 0.6f };
        _host.Particle = quick;
        Frame();
        _host.Particle = slow;
        Frame();

        Assert.That(SliderOf(XpfField.Lifetime).GetComponent<UiSlider>().Value, Is.EqualTo(2f));
        Assert.That(quick.Lifetime, Is.EqualTo(0.6f), "switching does not copy values across");
        Assert.That(_host.Gestures, Is.Zero);
    }

    [Test]
    public void StoreKeepsACopyInTheLibraryUnderItsName()
    {
        _host.Particle = new XpfParticle { Name = "mine", Amount = 7 };
        Frame();

        Click(ParticleEditorActionKind.Store);

        XpfParticle? stored = _library.Find("mine");
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored, Is.Not.SameAs(_host.Particle));
        Assert.That(stored!.Amount, Is.EqualTo(7));
        Assert.That(ParticleLibrary.Unsaved, Is.True);
    }

    [Test]
    public void TheLookButtonsCycleTheSettingsLookAsEdits()
    {
        _host.Particle = _library.Find("dust")!.Clone();
        Frame();

        Click(ParticleEditorActionKind.Texture);
        Click(ParticleEditorActionKind.ScaleCurve);

        Assert.That(_host.Particle.Texture, Is.EqualTo(XpfTexture.NoteGlyphs));
        Assert.That(XpfEditing.ScalePreset(_host.Particle), Is.EqualTo(XpfScalePreset.Shrink));
        Assert.That(_host.Edits, Is.EqualTo(2));
    }
}
