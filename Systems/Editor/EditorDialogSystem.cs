using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Editor.Recovery;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Theme;
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

    /// <param name="visuals">Draws the list rows the dialogs add as they fill; null for the real renderer.</param>
    /// <param name="recovery">Where discarded autosaves are deleted from; null for the user's own.</param>
    public EditorDialogSystem(
        EntityStore entities,
        EditorCommandBus commands,
        IVisualRidFactory? visuals = null,
        LevelRecoveryStore? recovery = null)
    {
        this.entities = entities;
        this.commands = commands;
        _visuals = visuals;
        _recovery = recovery;

        // A closed dialog is hidden, and still has to hear that it should open.
        Filter.WithDisabled();
    }

    private const float ShaderRefresh = 0.2f;
    private const string ShaderHint = "Tune a shader's uniforms with F2 while it is on screen; they save with the level.";
    private static readonly Color RowIdle = Palette.SurfaceRaised;
    private static readonly Color RowPicked = Palette.Primary;

    private readonly HashSet<int> _held = new();

    protected override void OnUpdate()
    {
        Entity editor = FindEditor();
        if (editor.IsNull)
            return;

        OfferRecovery(editor);
        OpenRequested(editor);

        foreach (Entity button in entities.Query<EditorDialogButton>().WithDisabled().ToEntityList())
        {
            if (PressedNow(button))
                Act(editor, button.GetComponent<EditorDialogButton>());
        }

        Entity colorDialog = EditorDialogs.Find(entities, EditorDialogKind.MapColor);
        if (!colorDialog.IsNull && !UiLayout.IsHidden(colorDialog))
            FollowPicker(editor);

        Entity shaderDialog = EditorDialogs.Find(entities, EditorDialogKind.Shaders);
        if (!shaderDialog.IsNull && !UiLayout.IsHidden(shaderDialog))
        {
            ref EditorDialogs dialogs = ref editor.GetComponent<EditorDialogs>();
            dialogs.ShaderRefreshIn -= Tick.deltaTime;
            if (dialogs.ShaderRefreshIn <= 0f)
            {
                // Commands land on the next frame, so the list catches up a moment later.
                dialogs.ShaderRefreshIn = ShaderRefresh;
                FillShaders(editor);
            }
        }
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in entities.Query<EditorDialogs, LevelInfo>().ToEntityList())
            return entity;

        return default;
    }

    private bool PressedNow(Entity entity)
    {
        bool pressed = entity.Tags.Has<Pressed>();
        bool was = !_held.Add(entity.Id);
        if (!pressed)
            _held.Remove(entity.Id);
        return pressed && !was;
    }

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
                case EditorDialogKind.Shaders:
                    UiText.Set(editor.GetComponent<EditorDialogs>().ShaderInfo, ShaderHint);
                    editor.GetComponent<EditorDialogs>().ShaderRefreshIn = 0f;
                    FillShaders(editor);
                    break;
                case EditorDialogKind.MapColor:
                    ShowColor(editor, (EditorColorTarget)state.Arg);
                    break;
            }

            UiLayout.SetHidden(dialog, false);
        }
    }

    private void Act(Entity editor, EditorDialogButton button)
    {
        ref EditorDialogs dialogs = ref editor.GetComponent<EditorDialogs>();
        int slot = dialogs.Shader;
        switch (button.Action)
        {
            case EditorDialogAction.Close:
                EditorDialogs.Dismiss(entities);
                break;

            case EditorDialogAction.PickSong:
                dialogs.Song = button.Arg;
                ShowSongPick(ref dialogs);
                break;
            case EditorDialogAction.UseSong:
                if (dialogs.Song >= 0 && dialogs.Song < dialogs.Songs.Count)
                {
                    commands.Enqueue(EditorCommand.LoadSongById(dialogs.Songs[dialogs.Song].Id));
                    Close(EditorDialogKind.Song);
                }
                break;

            case EditorDialogAction.Recover:
                AnswerRecovery(editor, restore: true);
                break;
            case EditorDialogAction.Discard:
                AnswerRecovery(editor, restore: false);
                break;

            case EditorDialogAction.PickShader:
                dialogs.Shader = button.Arg;
                FillShaders(editor);
                break;
            case EditorDialogAction.ImportShader:
                if (editor.HasComponent<EditorUiRequest>())
                    editor.GetComponent<EditorUiRequest>().Kind = EditorButtonKind.ImportShader;
                break;
            case EditorDialogAction.RemoveShader:
                commands.Enqueue(EditorCommand.RemoveShader(slot));
                break;
            case EditorDialogAction.ApplyShader:
                commands.Enqueue(EditorCommand.AssignShader(slot));
                break;
            case EditorDialogAction.ClearShader:
                commands.Enqueue(EditorCommand.AssignShader(0));
                break;
            case EditorDialogAction.PlayerShader:
                commands.Enqueue(EditorCommand.SetPlayerShader(slot));
                break;
            case EditorDialogAction.FieldShader:
                commands.Enqueue(EditorCommand.SetFieldShader(slot));
                break;
            case EditorDialogAction.PlayerShaderOff:
                commands.Enqueue(EditorCommand.SetPlayerShader(0));
                break;
            case EditorDialogAction.FieldShaderOff:
                commands.Enqueue(EditorCommand.SetFieldShader(0));
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

    private void FillSongs(Entity editor)
    {
        ref EditorDialogs dialogs = ref editor.GetComponent<EditorDialogs>();
        var store = new SongStore();
        dialogs.Songs.Clear();
        dialogs.Songs.AddRange(store.LoadAll().Where(song => store.AudioExists(song)));
        dialogs.Song = dialogs.Songs.Count > 0 ? 0 : -1;

        var labels = new List<string>(dialogs.Songs.Count);
        foreach (SongDocument song in dialogs.Songs)
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

        SongDocument song = dialogs.Songs[Math.Clamp(dialogs.Song, 0, dialogs.Songs.Count - 1)];
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

    private void FillShaders(Entity editor)
    {
        ref EditorDialogs dialogs = ref editor.GetComponent<EditorDialogs>();
        if (!editor.TryGetComponent(out LevelShaders shaders))
            return;

        var uses = new int[ShaderLimits.Count + 1];
        foreach (Entity item in entities.Query<ItemShader>().ToEntityList())
        {
            int slot = item.GetComponent<ItemShader>().Slot;
            if (slot is >= 1 and <= ShaderLimits.Count)
                uses[slot]++;
        }

        var labels = new List<string>(ShaderLimits.Count);
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
            labels.Add(text);
        }

        dialogs.Shader = Math.Clamp(dialogs.Shader, 1, ShaderLimits.Count);
        FillRows(dialogs.ShaderList, labels, EditorDialogAction.PickShader, 1);
        TintRows(dialogs.ShaderList, dialogs.Shader - 1);
    }

    /// <summary>Sets an editor dialog's shader message, such as how an import went.</summary>
    public static void ShowShaderInfo(Entity editor, string message)
    {
        if (!editor.IsNull && editor.TryGetComponent(out EditorDialogs dialogs))
            UiText.Set(dialogs.ShaderInfo, message);
    }

    // ----------------------------------------------------------------- colour

    private static void ShowColor(Entity editor, EditorColorTarget target)
    {
        ref EditorDialogs dialogs = ref editor.GetComponent<EditorDialogs>();
        LevelInfo level = editor.GetComponent<LevelInfo>();
        dialogs.ColorTarget = target;
        (string title, string hint, Color color) = target switch
        {
            EditorColorTarget.Outside => (
                "COLOUR OUTSIDE THE PLAYFIELD",
                "The colour around the map's playfield. It saves with the level.",
                level.Outside ?? LevelBackground.OutsideDefault),
            EditorColorTarget.Frame => (
                "PLAYFIELD FRAME COLOUR",
                "The frame and border line round the map's playfield. It saves with the level.",
                level.Frame ?? LevelBackground.FrameDefault),
            _ => (
                "MAP BACKGROUND COLOUR",
                "The colour behind the map's playfield. It saves with the level.",
                level.Background ?? LevelBackground.Default),
        };

        UiText.Set(dialogs.ColorTitle, title);
        UiText.Set(dialogs.ColorHint, hint);
        dialogs.ColorPicker.GetComponent<UiColorPicker>().Color = color;
        dialogs.ColorShown = dialogs.ColorPicker.GetComponent<UiColorPicker>().Color;
    }

    /// <summary>Applies the picker's colour live as it changes.</summary>
    private void FollowPicker(Entity editor)
    {
        ref EditorDialogs dialogs = ref editor.GetComponent<EditorDialogs>();
        Color picked = dialogs.ColorPicker.GetComponent<UiColorPicker>().Color;
        if (picked.IsEqualApprox(dialogs.ColorShown))
            return;

        dialogs.ColorShown = picked;
        commands.Enqueue(dialogs.ColorTarget switch
        {
            EditorColorTarget.Outside => EditorCommand.SetOutsideColor(picked),
            EditorColorTarget.Frame => EditorCommand.SetFrameColor(picked),
            _ => EditorCommand.SetBackgroundColor(picked),
        });
    }

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
                .Size(width, EditorScene.DialogListRowHeight)
                .Align(TextAlign.Left)
                .With(new EditorDialogButton(action, i + first))
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
            editor.GetComponent<EditorHud>().StatusText = message;
    }
}
