using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Profiles;
using Xanadu.Scenes;
using Xanadu.Systems.Scenes;

namespace Xanadu.Tests.Profiles;

[TestFixture]
public sealed class ProfileEditSystemTests
{
    private string _path = null!;
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private ProfileStore _profiles = null!;
    private UserProfile _profile = null!;
    private ProfileEditView _view;

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), "profile-edit-" + Guid.NewGuid().ToString("N") + ".json");
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _profiles = new ProfileStore(_path);
        _profiles.Create("Taken");
        _profile = _profiles.Create("Alice");
        _root.Add(new ProfileEditSystem(_profiles));

        // Stands in for the scene: the system only needs the controls' components.
        Entity name = _store.CreateEntity(new TextField("Alice"));
        var picker = new UiColorPicker { Color = ProfileArt.Tint(_profile) };
        Entity square = _store.CreateEntity(picker);
        Entity art = _store.CreateEntity();
        _store.CreateEntity(new ProfileEditView(name, square, art) { Typed = "Alice" });
        _view = new ProfileEditView(name, square, art);
        Frame();
    }

    [TearDown]
    public void TearDown() => File.Delete(_path);

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private ProfileStore Reloaded() => new(_path);

    [Test]
    public void OpeningTheScreenLeavesTheDefaultTintAlone()
    {
        Assert.That(_profile.NoteTint, Is.EqualTo(ProfileStore.DefaultNoteTint));
    }

    [Test]
    public void TypingRenamesAndPersists()
    {
        _view.Name.GetComponent<TextField>().Value = "Dario";
        Frame();

        Assert.That(Reloaded().ActiveProfile?.Name, Is.EqualTo("Dario"));
    }

    [Test]
    public void ABlankNameIsRefusedAndTheFieldRestored()
    {
        _view.Name.GetComponent<TextField>().Value = "   ";
        Frame();

        Assert.That(_profile.Name, Is.EqualTo("Alice"));
        Assert.That(_view.Name.GetComponent<TextField>().Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void ATakenNameSettlesOnASuffixedOne()
    {
        _view.Name.GetComponent<TextField>().Value = "taken";
        Frame();

        Assert.That(_profile.Name, Is.EqualTo("taken 2"));
        Assert.That(_view.Name.GetComponent<TextField>().Value, Is.EqualTo("taken 2"));
    }

    [Test]
    public void FocusedTextIsNotRewrittenWhileTyping()
    {
        _view.Name.AddTag<TextFieldFocused>();
        _view.Name.GetComponent<TextField>().Value = "taken";
        Frame();

        Assert.That(_view.Name.GetComponent<TextField>().Value, Is.EqualTo("taken"));
    }

    [Test]
    public void TintPersistsOnceItHoldsStillAndIgnoresAlpha()
    {
        _view.Picker.GetComponent<UiColorPicker>().Color = new Color(1, 0, 0, 0.5f);
        Frame();

        Assert.That(_profile.NoteTint, Is.EqualTo("#ff0000"));
        Assert.That(Reloaded().ActiveProfile?.NoteTint, Is.EqualTo(ProfileStore.DefaultNoteTint));

        Frame();
        Assert.That(Reloaded().ActiveProfile?.NoteTint, Is.EqualTo("#ff0000"));
    }

    [Test]
    public void TintStillSavesWhenTheScreenClosesMidDrag()
    {
        _view.Picker.GetComponent<UiColorPicker>().Color = new Color(0, 1, 0);
        Frame();
        foreach (Entity view in _store.Query<ProfileEditView>().ToEntityList())
            view.DeleteEntity();
        Frame();

        Assert.That(Reloaded().ActiveProfile?.NoteTint, Is.EqualTo("#00ff00"));
    }

    [Test]
    public void TheDefaultTintKeepsTheOriginalBlueGradient()
    {
        (Color top, Color bottom) = ProfileArt.Gradient(ProfileArt.Tint(null));

        Assert.That(top.B, Is.EqualTo(0.2f).Within(0.01f));
        Assert.That(top.R, Is.EqualTo(0.04f).Within(0.01f));
        Assert.That(bottom.B, Is.EqualTo(0.45f).Within(0.01f));
        Assert.That(bottom.R, Is.EqualTo(0.09f).Within(0.02f));
    }
}
