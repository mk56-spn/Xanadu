using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Levels;

namespace Xanadu.Editor;

/// <summary>
/// The map's background colour. The metadata panel's swatch opens a native
/// picker; changes apply live and are saved with the level.
/// </summary>
public partial class EditorOverlay : CanvasLayer
{
    private AcceptDialog _backgroundDialog = null!;
    private ColorPicker _backgroundPicker = null!;
    private bool _syncingBackground;

    private void BuildBackgroundDialog()
    {
        _backgroundDialog = new AcceptDialog
        {
            Title = "Map background colour",
            OkButtonText = "Done",
        };

        var box = new VBoxContainer();
        _backgroundDialog.AddChild(box);
        box.AddChild(new Label
        {
            Text = "The colour behind the map's playfield. It saves with the level.",
        });

        _backgroundPicker = new ColorPicker
        {
            EditAlpha = true,
            CustomMinimumSize = new Vector2(620, 420),
        };
        _backgroundPicker.ColorChanged += OnBackgroundColorPicked;
        box.AddChild(_backgroundPicker);

        AddChild(_backgroundDialog);
    }

    private void OpenBackgroundDialog()
    {
        if (_editor.IsNull)
            return;

        LevelInfo level = _editor.GetComponent<LevelInfo>();
        _syncingBackground = true;
        _backgroundPicker.Color = level.Background ?? LevelBackground.Default;
        _syncingBackground = false;
        _backgroundDialog.PopupCentered(new Vector2I(680, 540));
    }

    private void OnBackgroundColorPicked(Color color)
    {
        if (_syncingBackground)
            return;

        _commands.Enqueue(EditorCommand.SetBackgroundColor(color));
    }

    private bool IsBackgroundDialogVisible() => _backgroundDialog?.Visible ?? false;
}
