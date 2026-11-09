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
using Xanadu.Systems.Scenes;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Regression: entering preview saves the level first, so the editor rebuilt on
/// return does not offer the autosave back as an unsaved recovery. Leaving that
/// editor through SAVE &amp; LEAVE must then save and go, not report it as closed.
/// </summary>
public sealed class EditorPreviewSaveSceneTest : SceneTest
{
    private string _savedPath = "";
    private EditorOverlay? _overlay;

    public override string Name => "Editor preview save";
    public override string Slug => "editor-preview-save";

    private static readonly LoadedSong PreviewSong = TestContent.Song("preview-save-song", "Preview Save");

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
        AddAssert(
            "with no overlay, saving says the editor is closed",
            () => EditorOverlay.SaveForLeaving(null).Problem == "the editor is closed");

        AddStep("open the returned editor's overlay", () =>
        {
            CreateOverlay();
            Context.Scenes.SaveEditor = () => EditorOverlay.SaveForLeaving(_overlay);
        });
        AddWaitStep("overlay settles again", 2);
        AddStep("request leave", () => Context.Scenes.TryBack());
        AddUntilStep("leave confirmation is visible", ConfirmationVisible);

        AddStep("make the save refuse", () => Context.Scenes.SaveEditor =
            () => EditorSaveOutcome.Failed("test refusal"));
        ClickSaveAndLeave();
        AddUntilStep(
            "a refused save says why",
            () => VisualAssert.HasText(
                Context.Entities,
                "Can't save: test refusal. YES leaves without saving."));
        AddAssert("a refused save stays in the editor", () => Context.Scenes.Current == SceneKind.Editor);

        AddStep("let the real save run", () =>
            Context.Scenes.SaveEditor = () => EditorOverlay.SaveForLeaving(_overlay));
        AddStep("remove the file so the save has to rewrite it", () => File.Delete(_savedPath));
        ClickSaveAndLeave();
        AddUntilStep(
            "saving leaves for song select",
            () => Context.Scenes.Current == SceneKind.SongSelect);
        AddAssert("the level was written again", () => File.Exists(_savedPath));
        AddAssert(
            "no save problem was reported",
            () => !VisualAssert.HasText(Context.Entities, "Can't save: the editor is closed. YES leaves without saving."));
        AddStep("clean up", Cleanup);
    }

    private void ClickSaveAndLeave()
    {
        AddStep("click SAVE & LEAVE", () =>
        {
            foreach (Entity entity in Context.Entities.Query<SceneConfirmation>().ToEntityList())
            {
                Entity save = entity.GetComponent<SceneConfirmation>().Save;
                Context.Pointer.Click(UiTransform.WorldPosition(
                    save,
                    save.GetComponent<Position>().Value));
                return;
            }

            throw new SceneTestAssertionException("SAVE & LEAVE button missing");
        });
        AddWaitStep("hold save", 2);
        AddStep("release save", () => Context.Pointer.Release());
    }

    private bool ConfirmationVisible()
    {
        foreach (Entity entity in Context.Entities.Query<SceneConfirmation>().ToEntityList())
            return !UiLayout.IsHidden(entity);

        return false;
    }

    private void StageLevel()
    {
        CloseOverlay();
        _savedPath = "";

        Entity editor = Context.Entities.Query<LevelInfo, Playhead>().ToEntityList()[0];
        ref LevelInfo info = ref editor.GetComponent<LevelInfo>();
        info.Name = "Preview Save";
        info.SongId = PreviewSong.Id;
        info.Song = PreviewSong;

        Context.Entities.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            KeybindAction.Action2,
            NoteKind.Hit));

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
            new LevelEntry(path, document, PreviewSong),
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
