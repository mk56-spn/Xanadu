using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Levels;

namespace Xanadu.Editor;

/// <summary>
/// The map's colours: its background, the colour around its playfield, and the
/// playfield's frame. The metadata panel's swatches open one native picker for
/// any of them; changes apply live and are saved with the level.
/// </summary>
public partial class EditorOverlay : CanvasLayer
{
    private AcceptDialog _backgroundDialog = null!;
    private ColorPicker _backgroundPicker = null!;
    private bool _syncingBackground;

    /// <summary>Which of the map's colours the open picker sets.</summary>
    private MapColor _picking;
    private Label _backgroundHint = null!;

    private void BuildBackgroundDialog()
    {
        _backgroundDialog = new AcceptDialog
        {
            Title = "Map background colour",
            OkButtonText = "Done",
        };

        var box = new VBoxContainer();
        _backgroundDialog.AddChild(box);
        _backgroundHint = new Label();
        box.AddChild(_backgroundHint);

        _backgroundPicker = new ColorPicker
        {
            EditAlpha = true,
            CustomMinimumSize = new Vector2(620, 420),
        };
        _backgroundPicker.ColorChanged += OnBackgroundColorPicked;
        box.AddChild(_backgroundPicker);

        AddChild(_backgroundDialog);
    }

    private void OpenBackgroundDialog(MapColor which)
    {
        if (_editor.IsNull)
            return;

        LevelInfo level = _editor.GetComponent<LevelInfo>();
        _picking = which;
        (_backgroundDialog.Title, _backgroundHint.Text) = which switch
        {
            MapColor.Outside => ("Colour outside the playfield", "The colour around the map's playfield. It saves with the level."),
            MapColor.Frame => ("Playfield frame colour", "The frame and border line round the map's playfield. It saves with the level."),
            _ => ("Map background colour", "The colour behind the map's playfield. It saves with the level."),
        };
        _syncingBackground = true;
        _backgroundPicker.Color = which switch
        {
            MapColor.Outside => level.Outside ?? LevelBackground.OutsideDefault,
            MapColor.Frame => level.Frame ?? LevelBackground.FrameDefault,
            _ => level.Background ?? LevelBackground.Default,
        };
        _syncingBackground = false;
        _backgroundDialog.PopupCentered(new Vector2I(680, 540));
    }

    private void OnBackgroundColorPicked(Color color)
    {
        if (_syncingBackground)
            return;

        _commands.Enqueue(_picking switch
        {
            MapColor.Outside => EditorCommand.SetOutsideColor(color),
            MapColor.Frame => EditorCommand.SetFrameColor(color),
            _ => EditorCommand.SetBackgroundColor(color),
        });
    }

    private bool IsBackgroundDialogVisible() => _backgroundDialog?.Visible ?? false;

    /// <summary>The map colours the picker can set.</summary>
    private enum MapColor
    {
        Background,
        Outside,
        Frame,
    }
}
