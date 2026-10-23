using System;
using Godot;
using Writer.Levels;
using Writer.Scenes;

namespace Writer;

/// <summary>Native right-click menu for Song Select level actions.</summary>
public partial class SongSelectOverlay : CanvasLayer
{
    private readonly Func<LevelEntry?> _selected;
    private readonly Action _reload;
    private readonly Action<LevelEntry> _openTiming;
    private readonly PopupMenu _menu = new();
    private readonly ConfirmationDialog _confirm = new();

    public SongSelectOverlay(
        Func<LevelEntry?> selected,
        Action reload,
        Action<LevelEntry> openTiming)
    {
        _selected = selected;
        _reload = reload;
        _openTiming = openTiming;
        Layer = 3000;
    }

    public override void _Ready()
    {
        _menu.AddItem("Song timing", 1);
        _menu.AddItem("Delete level", 0);
        _menu.IdPressed += OnMenuPressed;
        AddChild(_menu);

        _confirm.Title = "Delete level";
        _confirm.DialogText = "Delete this level?";
        _confirm.Confirmed += DeleteSelected;
        AddChild(_confirm);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton
            {
                ButtonIndex: MouseButton.Right,
                Pressed: true,
            } mouse ||
            _selected() is null)
        {
            return;
        }

        Vector2 screen = GetViewport().GetVisibleRect().Size;
        Vector2 cardCenter = new(screen.X / 2, screen.Y / 2 - 10);
        Vector2 point = mouse.Position;
        if (Math.Abs(point.X - cardCenter.X) > 390 ||
            Math.Abs(point.Y - cardCenter.Y) > 165)
        {
            return;
        }

        _menu.Position = (Vector2I)point;
        _menu.Popup();
        GetViewport().SetInputAsHandled();
    }

    private void OnMenuPressed(long id)
    {
        LevelEntry? entry = _selected();
        if (entry is null)
            return;

        if (id == 1)
        {
            if (entry.HasSong)
                _openTiming(entry);
            return;
        }

        if (id == 0)
            _confirm.PopupCentered(new Vector2I(420, 180));
    }

    private void DeleteSelected()
    {
        LevelEntry? entry = _selected();
        if (entry is null)
            return;

        LevelStore.Delete(entry.Path);
        _reload();
    }
}
