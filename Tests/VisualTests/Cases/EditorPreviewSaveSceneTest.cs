using System;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Recovery;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Regression: entering preview saves the level first, so the editor rebuilt on
/// return does not offer the autosave back as an unsaved recovery.
/// </summary>
public sealed class EditorPreviewSaveSceneTest : SceneTest
{
    private string _savedPath = "";
    private EditorOverlay? _overlay;

    public override string Name => "Editor preview save";
    public override string Slug => "editor-preview-save";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorLeftMenu>().Count > 0);
        AddStep("stage a level with a recovery", StageLevel);
        AddAssert(
            "a recovery was staged",
            () => LevelRecoveryStore.Default.Read("") is not null);
        AddStep("create editor overlay", CreateOverlay);
        AddWaitStep("overlay settles", 2);
        AddStep("click preview", ClickPreview);
        AddWaitStep("hold preview", 2);
        AddStep("release preview", () => Context.Pointer.Release());
        AddUntilStep(
            "preview saved the untitled level",
            () => _savedPath != "");
        AddAssert("the level file was saved", () => File.Exists(_savedPath));
        AddUntilStep(
            "saving cleared the staged recovery",
            () => LevelRecoveryStore.Default.Read("") is null);
        AddStep("close editor overlay", CloseOverlay);
        AddUntilStep(
            "preview is playing",
            () => Context.Scenes.Current == SceneKind.Play);
        AddStep("pause preview", () => Context.Scenes.PausePlay());
        AddUntilStep("pause is visible", PauseVisible);
        AddStep("click leave level", ClickLeave);
        AddWaitStep("hold leave", 2);
        AddStep("release leave", () => Context.Pointer.Release());
        AddUntilStep(
            "back in the editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<LevelInfo>().Count > 0);
        AddAssert(
            "the returned editor kept the saved path",
            () => EditorPath() == _savedPath);
        AddWaitStep("let autosave rebase", 3);
        AddAssert(
            "no recovery is offered",
            () => Context.Entities.Query<EditorRecoveryOffer>().Count == 0);
        AddStep("clean up", Cleanup);
    }

    private void StageLevel()
    {
        CloseOverlay();
        _savedPath = "";

        Entity editor = Context.Entities.Query<LevelInfo, Playhead>().ToEntityList()[0];
        ref LevelInfo info = ref editor.GetComponent<LevelInfo>();
        info.Name = "Preview Save";
        info.SongId = "preview-save-song";

        Context.Entities.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            KeybindAction.M,
            NoteKind.Hit,
            DashDirection.Up,
            1,
            CharacterKind.Contact));

        // An autosave the preview has to clean up: without saving first, the
        // editor rebuilt on return would offer these extra edits back.
        LevelDocument staged = LevelDocumentMapper.Capture(Context.Entities, editor);
        staged.Notes.Add(new NoteDocument
        {
            TimeMs = 300,
            X = 20,
            Y = 20,
        });
        LevelRecoveryStore.Default.Write("", staged, DateTimeOffset.UtcNow);
    }

    private void CreateOverlay()
    {
        _overlay = new EditorOverlay(
            Context.Entities,
            Context.Systems.Commands,
            Context.Pointer,
            () => Context.Scenes.TryBack(),
            () => Context.Scenes.TryForward(),
            SaveAndPreview,
            openSongDialog: false);
        Context.Host.AddChild(_overlay);
    }

    private void SaveAndPreview(LevelDocument document, string path, long playheadMs)
    {
        _savedPath = path;
        Context.Scenes.StartEditorPreview(
            new LevelEntry(path, document, new SongStore().Find(document.SongId)),
            playheadMs);
    }

    private void ClickPreview()
    {
        foreach (Entity entity in Context.Entities
                     .Query<EditorButton, Position>()
                     .ToEntityList())
        {
            if (entity.GetComponent<EditorButton>().Kind != EditorButtonKind.Preview)
                continue;

            Context.Pointer.Click(UiTransform.WorldPosition(
                entity,
                entity.GetComponent<Position>().Value));
            return;
        }

        throw new SceneTestAssertionException("Preview button missing");
    }

    private bool PauseVisible()
    {
        foreach (Entity entity in Context.Entities.Query<ScenePause>().ToEntityList())
            return !UiLayout.IsHidden(entity);

        return false;
    }

    private string EditorPath()
    {
        foreach (Entity entity in Context.Entities.Query<LevelFile>().ToEntityList())
            return entity.GetComponent<LevelFile>().Path;

        return "";
    }

    private void ClickLeave()
    {
        foreach (Entity entity in Context.Entities.Query<ScenePause>().ToEntityList())
        {
            Entity leave = entity.GetComponent<ScenePause>().Leave;
            if (leave.IsNull)
                continue;

            Context.Pointer.Click(UiTransform.WorldPosition(
                leave,
                leave.GetComponent<Position>().Value));
            return;
        }

        throw new SceneTestAssertionException("Leave level button missing");
    }

    private void CloseOverlay()
    {
        _overlay?.Close();
        _overlay = null;
    }

    private void Cleanup()
    {
        CloseOverlay();
        LevelRecoveryStore.Default.Delete("");
        if (_savedPath == "")
            return;

        LevelRecoveryStore.Default.Delete(_savedPath);
        if (File.Exists(_savedPath))
            File.Delete(_savedPath);
    }
}
