using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Editor.Commands;
using Xanadu.Editor.Recovery;
using Xanadu.Model.Levels;
using Xanadu.Scenes;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Runs the editor's modal dialogs: opens the one asked for, fills its lists,
/// acts on its buttons, and offers to recover autosaved edits when the editor
/// finds some. Importing a shader asks the overlay for its native file dialog.
/// </summary>
public sealed class EditorDialogSystem : QuerySystem<EditorDialog>
{
    private readonly EntityStore entities;
    private readonly EditorCommandBus commands;
    private readonly IVisualRidFactory? _visuals;
    private readonly LevelRecoveryStore? _recovery;
    private readonly SongStore? _songs;

    /// <param name="visuals">Draws the list rows the dialogs add as they fill; null for the real renderer.</param>
    /// <param name="recovery">Where discarded autosaves are deleted from; null for the user's own.</param>
    /// <param name="songs">The song library the picker lists; null for the game's own.</param>
    public EditorDialogSystem(
        EntityStore entities,
        EditorCommandBus commands,
        IVisualRidFactory? visuals = null,
        LevelRecoveryStore? recovery = null,
        SongStore? songs = null)
    {
        this.entities = entities;
        this.commands = commands;
        _visuals = visuals;
        _recovery = recovery;
        _songs = songs;

        // A closed dialog is hidden, and still has to hear that it should open.
        Filter.WithDisabled();
    }

    private static readonly Color RowIdle = Palette.SurfaceRaised;
    private static readonly Color RowPicked = Palette.Primary;

    private readonly ButtonPress _presses = new();

    protected override void OnUpdate()
    {
        Entity editor = FindEditor();
        if (editor.IsNull)
            return;

        OfferRecovery(editor);
        HoldForSong(editor);
        OpenRequested(editor);

        foreach ((Entity button, EditorDialogAction action, UiCommand command) in UiCommand.All<EditorDialogAction>(entities))
        {
            if (PressedNow(button))
                Act(editor, action, command.Arg);
        }
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in entities.Query<EditorDialogs, LevelInfo>().ToEntityList())
            return entity;

        return default;
    }

    private bool PressedNow(Entity entity) => _presses.Started(entity);

    private void OfferRecovery(Entity editor)
    {
        if (!editor.TryGetComponent(out EditorRecoveryOffer offer) || offer.Asked)
            return;

        editor.GetComponent<EditorRecoveryOffer>().Asked = true;
        string name = string.IsNullOrEmpty(offer.Path)
            ? "this untitled level"
            : System.IO.Path.GetFileName(offer.Path);
        UiText.Set(
            editor.GetComponent<EditorDialogs>().RecoveryMessage,
            $"Found edits to {name} that were never saved ({offer.SavedAt.ToLocalTime():g}). " +
            "Recover them, or discard and keep the saved version?");
        EditorDialogs.Open(entities, EditorDialogKind.Recovery);
    }

    private void OpenRequested(Entity editor)
    {
        foreach (Entity dialog in Query.ToEntityList())
        {
            ref EditorDialog state = ref dialog.GetComponent<EditorDialog>();
            if (!state.OpenRequested)
                continue;

            state.OpenRequested = false;
            switch (state.Kind)
            {
                case EditorDialogKind.Song:
                    FillSongs(editor);
                    break;
            }

            UiLayout.SetHidden(dialog, false);
        }
    }

    /// <param name="arg">For a list row, which row.</param>
    private void Act(Entity editor, EditorDialogAction action, int arg)
    {
        ref EditorDialogs dialogs = ref editor.GetComponent<EditorDialogs>();
        switch (action)
        {
            case EditorDialogAction.Close:
                EditorDialogs.Dismiss(entities);
                break;

            case EditorDialogAction.PickSong:
                dialogs.Song = arg;
                ShowSongPick(ref dialogs);
                break;
            case EditorDialogAction.UseSong:
                if (dialogs.Song >= 0 && dialogs.Song < dialogs.Songs.Count)
                {
                    string id = dialogs.Songs[dialogs.Song].Id;
                    commands.Enqueue(EditorCommand.LoadSongById(id));
                    _awaitedSong = id;
                    _awaitFrames = AwaitSongFrames;
                    Close(EditorDialogKind.Song);
                }
                break;
            case EditorDialogAction.Leave:
                if (editor.HasComponent<EditorUiRequest>())
                    editor.GetComponent<EditorUiRequest>().Kind = EditorButtonKind.Leave;
                break;

            case EditorDialogAction.Recover:
                AnswerRecovery(editor, restore: true);
                break;
            case EditorDialogAction.Discard:
                AnswerRecovery(editor, restore: false);
                break;

        }
    }

    private void Close(EditorDialogKind kind)
    {
        Entity dialog = EditorDialogs.Find(entities, kind);
        if (!dialog.IsNull)
            UiLayout.SetHidden(dialog, true);
    }

    // ------------------------------------------------------------------ songs

    /// <summary>Frames a picked song has to land before the picker comes back.</summary>
    private const int AwaitSongFrames = 10;

    private SongStore Songs => _songs ?? SongStore.Shared;
    private string _awaitedSong = "";
    private int _awaitFrames;

    /// <summary>
    /// A level without a song it can play keeps the song picker up: its scrim
    /// dims the editor and takes every press, and its way out is BACK, not CANCEL.
    /// </summary>
    private void HoldForSong(Entity editor)
    {
        LevelInfo level = editor.GetComponent<LevelInfo>();

        // A picked song lands with the commands a frame or so later; until then the
        // picker would only flash back up.
        if (_awaitFrames > 0)
        {
            if (level.SongId == _awaitedSong)
                _awaitFrames = 0;
            else
                _awaitFrames--;
        }

        ref EditorDialogs dialogs = ref editor.GetComponent<EditorDialogs>();
        bool required = level.Song is null && _awaitFrames == 0;
        if (dialogs.SongRequired != required)
        {
            dialogs.SongRequired = required;
            UiLayout.SetHidden(dialogs.SongCancel, required);
            UiLayout.SetHidden(dialogs.SongBack, !required);
        }

        if (!required)
            return;

        // BACK asks the scene to confirm leaving, on a layer under this one: the
        // picker steps aside while it asks, and comes back if the leave is cancelled.
        if (ConfirmationShown())
        {
            if (EditorDialogs.IsOpen(entities, EditorDialogKind.Song))
            {
                Entity song = EditorDialogs.Find(entities, EditorDialogKind.Song);
                song.GetComponent<EditorDialog>().OpenRequested = false;
                UiLayout.SetHidden(song, true);
            }

            return;
        }

        if (!EditorDialogs.AnyOpen(entities))
            EditorDialogs.Open(entities, EditorDialogKind.Song);
    }

    private bool ConfirmationShown()
    {
        foreach (Entity panel in entities.Query<SceneConfirmation>().WithDisabled().ToEntityList())
        {
            if (!UiLayout.IsHidden(panel))
                return true;
        }

        return false;
    }

    private void FillSongs(Entity editor)
    {
        ref EditorDialogs dialogs = ref editor.GetComponent<EditorDialogs>();
        dialogs.Songs.Clear();
        dialogs.Songs.AddRange(Songs.Playable());
        dialogs.Song = dialogs.Songs.Count > 0 ? 0 : -1;

        var labels = new List<string>(dialogs.Songs.Count);
        foreach (LoadedSong song in dialogs.Songs)
        {
            string artist = string.IsNullOrWhiteSpace(song.Artist) ? "Unknown artist" : song.Artist;
            labels.Add($"{song.Title}   ·   {artist}");
        }

        FillRows(dialogs.SongList, labels, EditorDialogAction.PickSong, 0);
        UiLayout.SetHidden(dialogs.SongUse, dialogs.Songs.Count == 0);
        ShowSongPick(ref dialogs);
    }

    private static void ShowSongPick(ref EditorDialogs dialogs)
    {
        TintRows(dialogs.SongList, dialogs.Song);
        if (dialogs.Songs.Count == 0)
        {
            UiText.Set(dialogs.SongInfo, "No songs in the library. Import one first.");
            return;
        }

        LoadedSong song = dialogs.Songs[Math.Clamp(dialogs.Song, 0, dialogs.Songs.Count - 1)];
        UiText.Set(dialogs.SongInfo, $"{song.Bpm} BPM   ·   offset {song.OffsetMs} ms   ·   {song.Slug}");
    }

    // --------------------------------------------------------------- recovery

    private void AnswerRecovery(Entity editor, bool restore)
    {
        Close(EditorDialogKind.Recovery);
        if (!editor.TryGetComponent(out EditorRecoveryOffer offer))
            return;

        editor.RemoveComponent<EditorRecoveryOffer>();
        try
        {
            if (restore)
            {
                commands.Enqueue(EditorCommand.RecoverLevel(offer.Level, offer.Path));
                SetStatus(editor, "Recovered unsaved changes");
            }
            else
            {
                (_recovery ?? LevelRecoveryStore.Default).Delete(offer.Path);
            }
        }
        catch (Exception exception)
        {
            SetStatus(editor, $"Recovery failed: {exception.Message}");
        }
    }

    // ---------------------------------------------------------------- shaders

    // ----------------------------------------------------------------- colour

    // ------------------------------------------------------------------- rows

    /// <summary>
    /// Shows one row per label in a dialog list, making rows as it needs them and
    /// hiding the ones left over; each row's button carries its index plus <paramref name="first"/>.
    /// </summary>
    private void FillRows(Entity list, List<string> labels, EditorDialogAction action, int first)
    {
        float width = list.GetComponent<Rectangle>().Bounds.Size.X;
        var rows = new List<Entity>();
        foreach (Entity child in list.ChildEntities)
            rows.Add(child);

        UiNode content = UiNode.Wrap(list, _visuals);
        for (int i = rows.Count; i < labels.Count; i++)
        {
            rows.Add(content.Button("", 13, RowIdle)
                .Size(width, EditorSceneDialogs.DialogListRowHeight)
                .Align(TextAlign.Left)
                .With(UiCommand.Of(action, i + first))
                .Entity);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool shown = i < labels.Count;
            UiLayout.SetHidden(rows[i], !shown);
            if (shown)
                UiText.Set(rows[i], "   " + labels[i]);
        }
    }

    /// <summary>Lights the picked row of a dialog list, the bright blue of a selection.</summary>
    private static void TintRows(Entity list, int picked)
    {
        int index = 0;
        foreach (Entity row in list.ChildEntities)
        {
            if (row.HasComponent<ButtonVisual>())
            {
                ref ButtonVisual visual = ref row.GetComponent<ButtonVisual>();
                Color normal = index == picked ? RowPicked : RowIdle;
                if (visual.Normal != normal)
                {
                    visual.Normal = normal;
                    visual.Hover = normal.Lerp(Colors.White, 0.12f);
                    row.AddTag<Dirty>();
                }
            }

            index++;
        }
    }

    private static void SetStatus(Entity editor, string message)
    {
        if (editor.HasComponent<EditorHud>())
            editor.GetComponent<EditorHud>().SetStatus(message);
    }
}
