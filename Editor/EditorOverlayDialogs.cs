using System;
using System.IO;
using System.Linq;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Editor.Commands;

namespace Xanadu.Editor;

/// <summary>
/// Editor overlay saving. A level only ever saves to its own file in the level
/// directory: there is no save-as or load, so a player never picks where files
/// go. The song picker is an ECS dialog.
/// </summary>
public partial class EditorOverlay : CanvasLayer
{
    private void OpenSongDialog() => EditorDialogs.Open(_entities, EditorDialogKind.Song);

    public bool SaveCurrent() => SaveCurrent(out _);

    /// <summary>Saves on the way out through whichever overlay is open, if one is.</summary>
    public static Xanadu.Systems.Scenes.EditorSaveOutcome SaveForLeaving(EditorOverlay? overlay) =>
        overlay?.SaveForLeaving() ??
        Xanadu.Systems.Scenes.EditorSaveOutcome.Failed("the editor is closed");

    private Xanadu.Systems.Scenes.EditorSaveOutcome SaveForLeaving()
    {
        _saveProblem = "";
        return SaveCurrent()
            ? Xanadu.Systems.Scenes.EditorSaveOutcome.Success
            : Xanadu.Systems.Scenes.EditorSaveOutcome.Failed(_saveProblem);
    }

    /// <summary>
    /// Saves the level to its current path, or to a fresh unnamed one, and
    /// reports where it wrote so a caller can keep using that path.
    /// </summary>
    private bool SaveCurrent(out string path)
    {
        path = "";

        // A rebuilt scene replaces the entity this overlay was opened on.
        if (_editor.IsNull)
            _editor = FindEditor();

        if (_editor.IsNull)
        {
            _saveProblem = "no level is open";
            return false;
        }

        if (!ValidateCurrent())
            return false;

        try
        {
            LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
            string target = CurrentLevelPath();
            if (string.IsNullOrEmpty(target))
                target = LevelStore.NextUnnamedPath();

            LevelStore.Save(document, target);
            _commands.Enqueue(EditorCommand.SetLevelFile(target));
            SetStatus($"Saved {Path.GetFileName(target)}");
            path = target;
            return true;
        }
        catch (Exception exception)
        {
            _saveProblem = exception.Message;
            SetStatus($"Save failed: {exception.Message}");
            return false;
        }
    }
}
