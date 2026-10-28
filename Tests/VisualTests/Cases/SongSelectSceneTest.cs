using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.VisualTests;

public sealed class SongSelectSceneTest : SceneTest
{
    public override string Name => "Song select";
    public override string Slug => "song-select";

    protected override void SetUpSteps()
    {
        AddStep("open song select", () => Context.Scenes.Start(SceneKind.SongSelect));
        AddUntilStep(
            "wait for song select",
            () => VisualAssert.IsSceneReady(
                Context,
                SceneKind.SongSelect,
                SceneUi.MenuLayer) &&
                VisualAssert.HasText(Context.Entities, "SONG SELECT"));
        AddAssert(
            "SONG SELECT title exists",
            () => VisualAssert.HasText(Context.Entities, "SONG SELECT"));
        AddAssert(
            "song select layer is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                SceneUi.MenuLayer) == 0);
        AddAssert(
            "level list has nine rows",
            () => Context.Entities.Query<SongSelectTile>().Count == 9);
        AddAssert(
            "the empty carousel says so",
            () => VisualAssert.HasText(Context.Entities, "NO LEVELS FOUND"));
        AddAssert(
            "the empty carousel offers to create a level",
            () => VisualAssert.HasText(
                Context.Entities,
                "CREATE A LEVEL IN THE EDITOR"));
        AddStep("click the create level button", ClickCreateLevel);
        AddUntilStep(
            "the editor opens a new level",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<LevelFile>().Count == 1);
    }

    private void ClickCreateLevel()
    {
        foreach (Entity entity in Context.Entities.Query<Text>().ToEntityList())
        {
            if (entity.GetComponent<Text>().Value !=
                "CREATE A LEVEL IN THE EDITOR")
            {
                continue;
            }

            Context.Pointer.Click(UiTransform.WorldPosition(
                entity,
                entity.GetComponent<Position>().Value));
            return;
        }
    }
}
