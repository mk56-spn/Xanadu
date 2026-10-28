using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Systems.Shaders;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// A note whose Xif points at the common rainbow shader: proves the shader
/// actually loads and compiles into a real material inside a running engine,
/// which a headless NUnit run cannot do (there is no RenderingServer there).
/// Also proves the cached material survives a GC pass with nothing else
/// holding a C# reference to it, since a Godot RefCounted whose C# wrapper
/// is collected becomes unusable even if something still has its Rid.
/// </summary>
public sealed class RainbowNoteShaderSceneTest : SceneTest
{
    private ulong _waitStartMs;

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
                SceneUi.PlayHudLayer));

        AddAssert(
            "the note carries the common shader component",
            () =>
            {
                Entity note = Context.Entities.Query<Note>().ToEntityList()[0];
                return ItemCommonShader.Of(note) == CommonShaders.Paths[0];
            });

        AddAssert(
            "the common rainbow shader compiles to a real material",
            () =>
            {
                Rid rid = CommonShaderMaterials.MaterialOf(CommonShaders.Paths[0]);
                return rid.IsValid && CommonShaderMaterials.IsLive(CommonShaders.Paths[0]);
            });

        AddStep("note the time", () => _waitStartMs = Time.GetTicksMsec());

        AddUntilStep(
            "wait 10 seconds, forcing a GC pass each frame",
            () =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return Time.GetTicksMsec() - _waitStartMs >= 10_000;
            },
            timeoutSeconds: 15);

        AddAssert(
            "the material is still live after the wait, not just a non-zero Rid",
            () => CommonShaderMaterials.IsLive(CommonShaders.Paths[0]));
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
