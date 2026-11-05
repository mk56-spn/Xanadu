using System;
using System.IO;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Levels;
using Xanadu.Systems.Editor;

namespace Xanadu.Editor;

/// <summary>
/// F9: the level's custom shaders. The slots are an ECS dialog
/// (<see cref="EditorDialogKind.Shaders"/>); this side only owns the native file
/// dialog its IMPORT button asks for. Uniform values are tuned with the F8 panel.
/// </summary>
public partial class EditorOverlay : CanvasLayer
{
    private FileDialog _shaderFileDialog = null!;

    private void BuildShaderImportDialog()
    {
        _shaderFileDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = false,
            Title = "Import shader",
            Filters = ["*.gdshader ; Godot shaders"],
        };
        _shaderFileDialog.FileSelected += ImportShaderFile;
        AddChild(_shaderFileDialog);
    }

    private void ToggleShaderDialog()
    {
        if (EditorDialogs.IsOpen(_entities, EditorDialogKind.Shaders))
            EditorDialogs.Dismiss(_entities);
        else if (!EditorDialogs.AnyOpen(_entities))
            EditorDialogs.Open(_entities, EditorDialogKind.Shaders);
    }

    private void OpenShaderImport() => _shaderFileDialog.PopupCentered(new Vector2I(900, 600));

    private void ImportShaderFile(string path)
    {
        if (_editor.IsNull || !_editor.TryGetComponent(out EditorDialogs dialogs))
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
