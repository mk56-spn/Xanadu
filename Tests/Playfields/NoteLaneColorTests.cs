using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class NoteLaneColorTests
{
    [Test]
    public void NoteTargetDeterminesItsColor()
    {
        var note = new Note(
            1_000,
            new Vector2(500, 500),
            target: KeybindAction.Action2);

        Assert.That(
            PlayfieldRenderer.ColorFor(note.Target),
            Is.EqualTo(PlayfieldRenderer.ColorFor(KeybindAction.Action2)));
    }

    [Test]
    public void NoteKindsCanBeToldApart()
    {
        var colors = new System.Collections.Generic.HashSet<Color>();
        foreach (KeybindAction action in KeybindSettings.ActionKeys)
            colors.Add(PlayfieldRenderer.ColorFor(new Note(0, Vector2.Zero, target: action)));
        colors.Add(PlayfieldRenderer.ColorFor(new Note(0, Vector2.Zero, kind: NoteKind.Neutral)));
        colors.Add(PlayfieldRenderer.ColorFor(new Note(0, Vector2.Zero, kind: NoteKind.Dash)));
        colors.Add(PlayfieldRenderer.ColorFor(new Note(0, Vector2.Zero, kind: NoteKind.Momentum)));

        Assert.That(colors.Count, Is.EqualTo(6));
    }

    [Test]
    public void EachActionKeyHasItsOwnColor()
    {
        var colors = new System.Collections.Generic.HashSet<Color>();

        foreach (KeybindAction action in KeybindSettings.ActionKeys)
            colors.Add(PlayfieldRenderer.ColorFor(action));

        Assert.That(colors.Count, Is.EqualTo(KeybindSettings.ActionKeys.Length));
    }
}
