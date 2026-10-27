using System;
using Godot;
using Xanadu.Levels;
using Xanadu.Ecs;
using Xanadu.Systems.Scenes;

namespace Xanadu;

/// <summary>Developer-only song timing/add-song overlay.</summary>
public partial class SongTimingOverlay : CanvasLayer
{
    private readonly SceneSystem _scenes;
    private readonly Button _add = new();
    private readonly FileDialog _dialog = new();

    public SongTimingOverlay(SceneSystem scenes)
    {
        _scenes = scenes;
        Layer = 3000;
    }

    public override void _Ready()
    {
        _add.Text = "ADD SONG...";
        _add.Position = new Vector2(20, 20);
        _add.Size = new Vector2(180, 40);
        _add.Pressed += () => _dialog.PopupCentered(new Vector2I(900, 600));
        AddChild(_add);

        _dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        _dialog.Access = FileDialog.AccessEnum.Filesystem;
        _dialog.UseNativeDialog = false;
        _dialog.Title = "Add song";
        _dialog.Filters = ["*.mp3, *.wav, *.ogg, *.flac ; Audio Files"];
        _dialog.FileSelected += OnFileSelected;
        AddChild(_dialog);
    }

    private void OnFileSelected(string path)
    {
        try
        {
            SongDocument song = new SongStore().Import(path);
            _scenes.OpenSongTiming(song.Id, SceneKind.SongSelect);
        }
        catch (Exception exception)
        {
            GD.PushError($"Add song failed: {exception.Message}");
        }
    }
}
