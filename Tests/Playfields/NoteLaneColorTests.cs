using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class NoteLaneColorTests
{
    [Test]
    public void NoteTargetDeterminesItsColor()
    {
        var note = new Note(
            1_000,
            new Vector2(500, 500),
            target: KeybindAction.R2);

        Assert.That(
            PlayfieldRenderer.ColorFor(note.Target),
            Is.EqualTo(PlayfieldRenderer.ColorFor(KeybindAction.R2)));
    }

    [Test]
    public void EveryTargetHasItsOwnColor()
    {
        var colors = new System.Collections.Generic.HashSet<Color>();

        foreach (KeybindAction action in KeybindSettings.Actions)
            colors.Add(PlayfieldRenderer.ColorFor(action));

        Assert.That(colors.Count, Is.EqualTo(KeybindSettings.Actions.Length));
    }
}
