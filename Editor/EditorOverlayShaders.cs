using System;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Levels;

namespace Xanadu.Editor;

/// <summary>
/// F9: the level's custom shaders. Pick a slot, import a .gdshader into it,
/// and assign the slot to the selected notes, mines and blocks, or to the
/// player or the playfield. Uniform values are tuned with the F8 panel.
/// </summary>
public partial class EditorOverlay : CanvasLayer
{
    private AcceptDialog _shaderDialog = null!;
    private ItemList _shaderList = null!;
    private Label _shaderInfo = null!;
    private FileDialog _shaderFileDialog = null!;
    private double _shaderRefreshIn;

    private void BuildShaderDialog()
    {
        _shaderDialog = new AcceptDialog
        {
            Title = "Level shaders (F9)",
            OkButtonText = "Close",
        };

        var box = new VBoxContainer();
        _shaderDialog.AddChild(box);
        box.AddChild(new Label
        {
            Text = $"Up to {ShaderLimits.Count} canvas shaders per level. Select a slot, then:",
        });

        _shaderList = new ItemList
        {
            CustomMinimumSize = new Vector2(520, 250),
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        box.AddChild(_shaderList);

        _shaderInfo = new Label { Text = "" };
        box.AddChild(_shaderInfo);

        var slotRow = new HBoxContainer();
        box.AddChild(slotRow);
        AddShaderButton(slotRow, "Import .gdshader…", OpenShaderImport);
        AddShaderButton(slotRow, "Remove", () => WithSlot(slot => _commands.Enqueue(EditorCommand.RemoveShader(slot))));

        var itemRow = new HBoxContainer();
        box.AddChild(itemRow);
        AddShaderButton(itemRow, "Apply to selection", () => WithSlot(slot => _commands.Enqueue(EditorCommand.AssignShader(slot))));
        AddShaderButton(itemRow, "Clear selection", () => _commands.Enqueue(EditorCommand.AssignShader(0)));

        var specialRow = new HBoxContainer();
        box.AddChild(specialRow);
        AddShaderButton(specialRow, "Use on player", () => WithSlot(slot => _commands.Enqueue(EditorCommand.SetPlayerShader(slot))));
        AddShaderButton(specialRow, "Use on field", () => WithSlot(slot => _commands.Enqueue(EditorCommand.SetFieldShader(slot))));
        AddShaderButton(specialRow, "Player off", () => _commands.Enqueue(EditorCommand.SetPlayerShader(0)));
        AddShaderButton(specialRow, "Field off", () => _commands.Enqueue(EditorCommand.SetFieldShader(0)));

        AddChild(_shaderDialog);

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

    private static void AddShaderButton(Container row, string text, Action pressed)
    {
        var button = new Button { Text = text };
        button.Pressed += pressed;
        row.AddChild(button);
    }

    private void ToggleShaderDialog()
    {
        if (_shaderDialog.Visible)
        {
            _shaderDialog.Hide();
            return;
        }

        RefreshShaderList();
        _shaderDialog.PopupCentered(new Vector2I(580, 470));
    }

    private bool IsShaderDialogVisible() =>
        (_shaderDialog?.Visible ?? false) || (_shaderFileDialog?.Visible ?? false);

    /// <summary>Commands land on the next frame, so the list catches up a moment later.</summary>
    private void ProcessShaderDialog(double delta)
    {
        if (!_shaderDialog.Visible)
            return;

        _shaderRefreshIn -= delta;
        if (_shaderRefreshIn > 0)
            return;

        _shaderRefreshIn = 0.2;
        RefreshShaderList();
    }

    private void RefreshShaderList()
    {
        if (_editor.IsNull || !_editor.TryGetComponent(out LevelShaders shaders))
            return;

        int selected = _shaderList.GetSelectedItems().Length > 0 ? _shaderList.GetSelectedItems()[0] : 0;
        var uses = new int[ShaderLimits.Count + 1];
        foreach (Entity item in _entities.Query<ItemShader>().ToEntityList())
        {
            int slot = item.GetComponent<ItemShader>().Slot;
            if (slot is >= 1 and <= ShaderLimits.Count)
                uses[slot]++;
        }

        _shaderList.Clear();
        for (int i = 0; i < ShaderLimits.Count; i++)
        {
            int slot = i + 1;
            ShaderSlot? shader = shaders.Slots[i];
            string text = shader is null
                ? $"{slot}:  (empty)"
                : $"{slot}:  {(shader.Name.Length > 0 ? shader.Name : "unnamed")}   ·   {uses[slot]} objects";
            if (shaders.PlayerSlot == slot)
                text += "   [player]";
            if (shaders.FieldSlot == slot)
                text += "   [field]";

            _shaderList.AddItem(text);
        }

        _shaderList.Select(Math.Clamp(selected, 0, ShaderLimits.Count - 1));
        _shaderInfo.Text = "Tune a shader's uniforms with F8 while it is on screen; they save with the level.";
    }

    private void WithSlot(Action<int> action)
    {
        int[] selected = _shaderList.GetSelectedItems();
        if (selected.Length > 0)
            action(selected[0] + 1);
    }

    private void OpenShaderImport()
    {
        if (_shaderList.GetSelectedItems().Length == 0)
            return;

        _shaderFileDialog.PopupCentered(new Vector2I(900, 600));
    }

    private void ImportShaderFile(string path)
    {
        int[] selected = _shaderList.GetSelectedItems();
        if (selected.Length == 0)
            return;

        try
        {
            string source = File.ReadAllText(path);
            if (!ShaderSourceRules.IsAcceptable(source, out string problem))
            {
                _shaderInfo.Text = $"Not imported: {problem}";
                return;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            _commands.Enqueue(EditorCommand.ImportShader(selected[0] + 1, name, source));
            _shaderInfo.Text = $"Imported {name} into slot {selected[0] + 1}.";
        }
        catch (Exception exception)
        {
            _shaderInfo.Text = $"Could not read the file: {exception.Message}";
        }
    }
}
