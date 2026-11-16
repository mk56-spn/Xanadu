using NUnit.Framework;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Xanadu.Model.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class NoteLaneColorTests
{
    [Test]
    public void NoteTargetDeterminesItsColor()
    {
        var note = new Note(1_000, new Vector2(500, 500), KeybindAction.Action2);

        Assert.That(
            PlayfieldColors.ColorFor(note.Target),
            Is.EqualTo(PlayfieldColors.ColorFor(KeybindAction.Action2)));
    }

    [Test]
    public void NoteKindsCanBeToldApart()
    {
        var colors = new System.Collections.Generic.HashSet<Color>();
        foreach (KeybindAction action in KeybindSettings.ActionKeys)
            colors.Add(PlayfieldColors.ColorFor(new Note(0, Vector2.Zero, action)));
        colors.Add(PlayfieldColors.ColorFor(new Note(0, Vector2.Zero, kind: NoteKind.Neutral)));
        colors.Add(PlayfieldColors.ColorFor(new Note(0, Vector2.Zero, kind: NoteKind.Dash)));
        colors.Add(PlayfieldColors.ColorFor(new Note(0, Vector2.Zero, kind: NoteKind.Momentum)));

        Assert.That(colors.Count, Is.EqualTo(6));
    }

    [Test]
    public void EachActionKeyHasItsOwnColor()
    {
        var colors = new System.Collections.Generic.HashSet<Color>();

        foreach (KeybindAction action in KeybindSettings.ActionKeys)
            colors.Add(PlayfieldColors.ColorFor(action));

        Assert.That(colors.Count, Is.EqualTo(KeybindSettings.ActionKeys.Length));
    }
}
