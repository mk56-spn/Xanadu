using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorReturnStateTests : EcsSystemTest
{
    private Entity CreateEditor() => Store.CreateEntity(
        new PlayfieldMode(PlayfieldKind.Editor),
        new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
        new NoteSelection(default),
        new EditorPlacement { Mode = EditorPlacementMode.Notes });

    [Test]
    public void RestoresTheViewToolAndSelectionOnARebuiltEditor()
    {
        Entity editor = CreateEditor();
        editor.GetComponent<PlayfieldView>() = new PlayfieldView(new Vector2(40, 50), 1.5f, new Vector2(1000, 1000));
        editor.GetComponent<EditorPlacement>().Mode = EditorPlacementMode.Spikes;
        editor.GetComponent<EditorPlacement>().Degrees = 180f;
        Entity spike = EditorItems.Create(Store, new Spike(new Vector2(100, 100), new Vector2(50, 50)));
        EditorItems.Create(Store, new Spike(new Vector2(300, 100), new Vector2(50, 50)));
        editor.GetComponent<NoteSelection>().Note = spike;

        EditorReturnState state = EditorReturnState.Capture(Store)!;

        foreach (Entity item in Store.Query<Spike>().ToEntityList())
            item.DeleteEntity();
        editor.DeleteEntity();
        Entity rebuilt = CreateEditor();
        Entity again = EditorItems.Create(Store, new Spike(new Vector2(100, 100), new Vector2(50, 50)));
        EditorItems.Create(Store, new Spike(new Vector2(300, 100), new Vector2(50, 50)));

        state.Restore(Store, rebuilt);

        Assert.That(rebuilt.GetComponent<PlayfieldView>().Scale, Is.EqualTo(1.5f));
        Assert.That(rebuilt.GetComponent<PlayfieldView>().ScreenOrigin, Is.EqualTo(new Vector2(40, 50)));
        Assert.That(rebuilt.GetComponent<EditorPlacement>().Mode, Is.EqualTo(EditorPlacementMode.Spikes));
        Assert.That(rebuilt.GetComponent<EditorPlacement>().Degrees, Is.EqualTo(180f));
        Assert.That(rebuilt.GetComponent<NoteSelection>().AllNotes, Is.EquivalentTo(new[] { again }));
    }

    [Test]
    public void APlayheadStartBeginsAtTheLastStartBeforeIt()
    {
        CreateEditor();
        var starts = new List<StartPoint>
        {
            new("a", 1_000, new Vector2(100, 200), System.Numerics.Vector2.Zero, new Vector2(0, -1)),
            new("b", 9_000, new Vector2(800, 800), System.Numerics.Vector2.Zero, new Vector2(0, -1)),
        };

        StartPoint start = EditorReturnState.StartAtPlayhead(Store, new LevelDocument(), starts, 5_000);

        Assert.That(start.TimeMs, Is.EqualTo(5_000));
        Assert.That(start.Position, Is.EqualTo(new Vector2(100, 200)));
    }

    [Test]
    public void APlayheadStartPrefersWhereTheLastPreviewWasThen()
    {
        Entity editor = CreateEditor();
        editor.AddComponent(new PlayerPath(
            new List<PlayerPathSample>
            {
                new(new Vector2(10, 10), 1_000, false),
                new(new Vector2(20, 20), 4_000, false),
                new(new Vector2(30, 30), 6_000, false),
            },
            new Vector2(30, 200)));

        StartPoint start = EditorReturnState.StartAtPlayhead(
            Store, new LevelDocument(), new List<StartPoint>(), 5_000);

        Assert.That(start.Position, Is.EqualTo(new Vector2(20, 20)));
    }

    [Test]
    public void APlayheadStartWithNothingToGoOnUsesTheLevelsOpeningSpot()
    {
        CreateEditor();
        var document = new LevelDocument { PlayfieldWidth = 1000, PlayfieldHeight = 1000 };

        StartPoint start = EditorReturnState.StartAtPlayhead(Store, document, new List<StartPoint>(), 5_000);

        Assert.That(start.Position.X, Is.EqualTo(500));
        Assert.That(start.Position.Y, Is.EqualTo(1000 - Player.ScaledSize(1f).Y / 2f));
    }
}
