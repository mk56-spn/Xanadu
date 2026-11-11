using System;
using System.IO;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Levels;
using Xanadu.Systems.Editor;

namespace Xanadu.Editor;

/// <summary>
/// F10: the level's custom shaders. The slots are an ECS dialog
/// (<see cref="EditorDialogKind.Shaders"/>); this side only asks the host for
/// the file its IMPORT button wants. Uniform values are tuned with the F2 panel.
/// </summary>
public sealed partial class EditorOverlay
{
    /// <summary>
    /// Opens the host's file picker for a Slang shader; the callback gets the
    /// picked path on the main thread. Null when the host has no picker.
    /// </summary>
    public Action<Action<string>>? PickShaderFile { get; set; }

    /// <summary>Whether the host's file picker is up; the pointer is held off the ECS UI while it is.</summary>
    public Func<bool>? FileDialogOpen { get; set; }

    private void ToggleShaderDialog()
    {
        if (EditorDialogs.IsOpen(_entities, EditorDialogKind.Shaders))
            EditorDialogs.Dismiss(_entities);
        else if (!EditorDialogs.AnyOpen(_entities))
            EditorDialogs.Open(_entities, EditorDialogKind.Shaders);
    }

    private void OpenShaderImport() => PickShaderFile?.Invoke(ImportShaderFile);

    private void ImportShaderFile(string path)
    {
        if (_closed || _editor.IsNull || !_editor.TryGetComponent(out EditorDialogs dialogs))
            return;

        int slot = dialogs.Shader;
        try
        {
            string source = File.ReadAllText(path);
            if (!ShaderSourceRules.IsAcceptable(source, out string problem))
            {
                EditorDialogSystem.ShowShaderInfo(_editor, $"Not imported: {problem}");
                return;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            _commands.Enqueue(EditorCommand.ImportShader(slot, name, source));
            EditorDialogSystem.ShowShaderInfo(_editor, $"Imported {name} into slot {slot}.");
        }
        catch (Exception exception)
        {
            EditorDialogSystem.ShowShaderInfo(_editor, $"Could not read the file: {exception.Message}");
        }
    }
}
