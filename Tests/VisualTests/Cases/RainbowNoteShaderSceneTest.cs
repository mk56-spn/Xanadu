using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Scenes;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Shaders;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// A note whose Xif points at the common rainbow shader: proves the shader
/// compiles in the running host (through Slang), which a headless NUnit run
/// cannot do. The Godot version also waited out GC passes for the material's
/// RefCounted wrapper; host materials are plain C# objects, so that is gone.
/// </summary>
public sealed class RainbowNoteShaderSceneTest : SceneTest
{
    public override string Name => "Note with the common rainbow shader";
    public override string Slug => "rainbow-note-shader";

    protected override void SetUpSteps()
    {
        AddStep("open synthetic play scene with a rainbow note", OpenPlay);

        AddUntilStep(
            "wait for play HUD",
            () => VisualAssert.IsSceneReady(
                Context,
                SceneKind.Play,
                UiLayers.PlayHud));

        AddAssert(
            "the note carries the common shader component",
            () =>
            {
                Entity note = Context.Entities.Query<Note>().ToEntityList()[0];
                return ItemCommonShader.Of(note) == CommonShaders.Paths[0];
            });

        AddAssert(
            "the common rainbow shader compiles in the host",
            () => HostPlatform.DescribeMaterial?.Invoke(
                new CanvasMaterial(CommonShaders.Paths[0], BlendMode.Mix)) is not null);
    }

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-rainbow-note", "Visual Rainbow Note");
        LevelDocument document = TestContent.Level("Rainbow Note Test", song);

        document.Notes.Add(new NoteDocument
        {
            TimeMs = 0,
            X = 400,
            Y = 600,
            Item = Xif.ForCommonShader(CommonShaders.Paths[0]),
        });

        Context.Scenes.StartPlay(
            new LevelEntry("visual://rainbow-note", document, song));
    }
}
