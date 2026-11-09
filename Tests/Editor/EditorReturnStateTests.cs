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
        editor.GetComponent<EditorPlacement>().BlockSize = BlockBrushSize.Platform;
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
        Assert.That(
            rebuilt.GetComponent<EditorPlacement>().BlockSize,
            Is.EqualTo(BlockBrushSize.Platform));
        Assert.That(rebuilt.GetComponent<NoteSelection>().AllNotes, Is.EquivalentTo(new[] { again }));
    }

    [Test]
    public void RestoresToolSettingsSnapMetronomeAndLayers()
    {
        Entity editor = CreateEditor();
        editor.AddComponent(new Playhead(0, 120, 0, false) { SnapDivision = 8 });
        editor.AddComponent(new EditorMetronome(true));
        editor.AddComponent(EditorLayers.Create());
        ref EditorPlacement placement = ref editor.GetComponent<EditorPlacement>();
        placement.Strength = DashStrength.Double;
        placement.ExitMomentum = 3f;
        placement.Modulate = Colors.Red;
        ref EditorLayers layers = ref editor.GetComponent<EditorLayers>();
        layers.Open = true;
        layers.Hidden.Add(2);
        layers.Solo = 3;

        EditorReturnState state = EditorReturnState.Capture(Store)!;
        editor.DeleteEntity();

        Entity rebuilt = CreateEditor();
        rebuilt.AddComponent(new Playhead(0, 120, 0, false));
        rebuilt.AddComponent(new EditorMetronome(false));
        rebuilt.AddComponent(EditorLayers.Create());
        state.Restore(Store, rebuilt);

        EditorPlacement restored = rebuilt.GetComponent<EditorPlacement>();
        Assert.That(restored.Strength, Is.EqualTo(DashStrength.Double));
        Assert.That(restored.ExitMomentum, Is.EqualTo(3f));
        Assert.That(restored.Modulate, Is.EqualTo(Colors.Red));
        Assert.That(rebuilt.GetComponent<Playhead>().SnapDivision, Is.EqualTo(8));
        Assert.That(rebuilt.GetComponent<EditorMetronome>().Enabled, Is.True);
        EditorLayers restoredLayers = rebuilt.GetComponent<EditorLayers>();
        Assert.That(restoredLayers.Open, Is.True);
        Assert.That(restoredLayers.Hidden, Does.Contain(2));
        Assert.That(restoredLayers.Solo, Is.EqualTo(3));
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
