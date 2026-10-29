using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using SceneState = Xanadu.Ecs.SceneState;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private const float SongMenuWidth = 200;
    private const float SongMenuItemWidth = 184;
    private const float SongMenuItemHeight = 32;
    private const float SongMenuHeight = 84;

    private const float DeleteWidth = 420;
    private const float DeleteHeight = 190;
    private const float DeleteTextWidth = 380;
    private const float DeleteMessageHeight = 50;
    private const float DeleteActionWidth = 150;
    private const float DeleteActionHeight = 42;

    /// <summary>Half the card the right-click menu answers on: the level art.</summary>
    private const float CardHalfWidth = 390;
    private const float CardHalfHeight = 165;
    private const float CardOffsetY = -10;
    private const float MenuMargin = 8;

    private Entity _songMenu;
    private Entity _deleteLevelPanel;
    private bool _wasSecondary;

    private void BuildSongMenu()
    {
        UiNode ui = UiNode.Layer(
            _entities, default, SceneUi.PauseLayer, _visuals, UiStage.Full);

        UiNode menu = ui.Column()
            .Size(SongMenuWidth, SongMenuHeight)
            .At(UiLayout.Hidden)
            .Fill(SceneUi.PanelColor)
            .Pad(UiMetrics.Space.Xs)
            .Gap(UiMetrics.Space.Xxs)
            .Main(StackMainAlignment.Start);

        Entity timing = menu.Button(
                "SONG TIMING",
                UiType.Small,
                SceneUi.MutedButtonColor)
            .Size(SongMenuItemWidth, SongMenuItemHeight)
            .Entity;
        Entity delete = menu.Button(
                "DELETE LEVEL",
                UiType.Small,
                SceneUi.MutedButtonColor)
            .Size(SongMenuItemWidth, SongMenuItemHeight)
            .Entity;
        menu.With(new SceneSongMenu(timing, delete));
        _songMenu = menu.Entity;

        UiNode confirm = ui.Column()
            .Size(DeleteWidth, DeleteHeight)
            .At(UiLayout.Hidden)
            .Fill(SceneUi.PanelColor)
            .Pad(UiMetrics.Space.Md)
            .Gap(UiMetrics.Space.Md)
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center);

        confirm.Text(
            "DELETE LEVEL?",
            UiType.Section,
            SceneUi.AccentColor,
            TextAlign.Center);
        confirm.Text(
                "Delete this level? This cannot be undone.",
                UiType.Body,
                SceneUi.TextColor,
                TextAlign.Center)
            .Size(DeleteTextWidth, DeleteMessageHeight);

        UiNode actions = confirm.Row().Gap(UiMetrics.Space.Sm);
        Entity yes = actions.Button(
                "DELETE",
                UiType.Label,
                SceneUi.DangerColor)
            .Size(DeleteActionWidth, DeleteActionHeight)
            .Entity;
        Entity no = actions.Button(
                "CANCEL",
                UiType.Label,
                SceneUi.MutedButtonColor)
            .Size(DeleteActionWidth, DeleteActionHeight)
            .Entity;

        confirm.With(new SceneDeleteLevel(yes, no));
        _deleteLevelPanel = confirm.Entity;
    }

    private bool HandleSongMenu(ref SceneState state)
    {
        if (_songMenu.IsNull || _deleteLevelPanel.IsNull)
            return false;

        ref SceneDeleteLevel confirm =
            ref _deleteLevelPanel.GetComponent<SceneDeleteLevel>();
        if (confirm.Visible)
        {
            if (IsPressed(confirm.Confirm))
            {
                DeleteSelectedLevel();
                HideDeleteLevel();
                return true;
            }

            if (IsPressed(confirm.Cancel))
            {
                HideDeleteLevel();
                return true;
            }

            return true;
        }

        ref SceneSongMenu menu = ref _songMenu.GetComponent<SceneSongMenu>();
        if (menu.Visible)
        {
            if (IsPressed(menu.Timing))
            {
                HideSongMenu();
                OpenSelectedTiming();
                return true;
            }

            if (IsPressed(menu.Delete))
            {
                HideSongMenu();
                ShowDeleteLevel();
                return true;
            }
        }

        if (_pointer is null || state.Current != SceneKind.SongSelect)
        {
            _wasSecondary = false;
            return false;
        }

        PointerInput input = _pointer.Read();
        bool rising = input.SecondaryPressed && !_wasSecondary;
        _wasSecondary = input.SecondaryPressed;
        if (!rising)
            return false;

        Vector2 center = UiStage.Full.Center + new Vector2(0, CardOffsetY);
        bool onCard =
            Math.Abs(input.Position.X - center.X) <= CardHalfWidth &&
            Math.Abs(input.Position.Y - center.Y) <= CardHalfHeight;

        if (onCard && SelectedLevel is not null)
            ShowSongMenu(input.Position);
        else
            HideSongMenu();

        return false;
    }

    private static bool IsPressed(Entity entity) =>
        !entity.IsNull && entity.Tags.Has<Pressed>();

    private void ShowSongMenu(Vector2 at)
    {
        if (_songMenu.IsNull)
            return;

        ref SceneSongMenu menu = ref _songMenu.GetComponent<SceneSongMenu>();
        menu.Visible = true;
        Vector2 size = UiStage.Full.Size;
        ref Position position = ref _songMenu.GetComponent<Position>();
        position.Value = new Vector2(
            Math.Clamp(
                at.X,
                SongMenuWidth / 2 + MenuMargin,
                size.X - SongMenuWidth / 2 - MenuMargin),
            Math.Clamp(
                at.Y,
                SongMenuHeight / 2 + MenuMargin,
                size.Y - SongMenuHeight / 2 - MenuMargin));
        _songMenu.AddTag<Moved>();
    }

    private void HideSongMenu()
    {
        if (_songMenu.IsNull)
            return;

        ref SceneSongMenu menu = ref _songMenu.GetComponent<SceneSongMenu>();
        if (!menu.Visible)
            return;

        menu.Visible = false;
        ref Position position = ref _songMenu.GetComponent<Position>();
        position.Value = UiLayout.Hidden;
        _songMenu.AddTag<Moved>();
    }

    private void ShowDeleteLevel()
    {
        if (_deleteLevelPanel.IsNull)
            return;

        ref SceneDeleteLevel confirm =
            ref _deleteLevelPanel.GetComponent<SceneDeleteLevel>();
        confirm.Visible = true;
        ref Position position = ref _deleteLevelPanel.GetComponent<Position>();
        position.Value = UiStage.Full.Center;
        _deleteLevelPanel.AddTag<Moved>();
    }

    private void HideDeleteLevel()
    {
        if (_deleteLevelPanel.IsNull)
            return;

        ref SceneDeleteLevel confirm =
            ref _deleteLevelPanel.GetComponent<SceneDeleteLevel>();
        if (!confirm.Visible)
            return;

        confirm.Visible = false;
        ref Position position = ref _deleteLevelPanel.GetComponent<Position>();
        position.Value = UiLayout.Hidden;
        _deleteLevelPanel.AddTag<Moved>();
    }

    private void OpenSelectedTiming()
    {
        LevelEntry? entry = SelectedLevel;
        if (entry is null || !entry.HasSong)
            return;

        OpenSongTiming(entry.Document.SongId, SceneKind.SongSelect);
    }

    private void DeleteSelectedLevel()
    {
        LevelEntry? entry = SelectedLevel;
        if (entry is null)
            return;

        LevelStore.Delete(entry.Path);
        ReloadCurrent();
    }
}
