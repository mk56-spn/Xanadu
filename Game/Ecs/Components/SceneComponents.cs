using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Levels;

namespace Xanadu.Ecs;

/// <summary>Top-level game scene managed by the ECS scene system.</summary>
public enum SceneKind
{
    None,
    Profiles,
    Main,
    SongSelect,
    Play,
    Results,
    Editor,
    OffsetWizard,
    Settings,
    SongTiming,
    ProfileEdit,
}

/// <summary>Persistent game scene state and the next requested scene.</summary>
public struct SceneState(SceneKind scene) : IComponent
{
    public SceneKind Current = scene;
    public SceneKind Pending = scene;
    public int SelectedIndex;
    public string SelectedLevelPath = "";
    public string SelectedSongId = "";
    public SceneKind SongTimingBack = SceneKind.SongSelect;
}

/// <summary>
/// The background a scene stands on. Scenes naming the same kind share one: it
/// survives the switch between them instead of being rebuilt.
/// </summary>
public enum SceneBackgroundKind
{
    /// <summary>The scene draws its own background, if it has one.</summary>
    None,

    /// <summary>The notes shader behind profiles, main menu and song select.</summary>
    Menu,
}

/// <summary>Marks the layer holding a background that outlives the scene root.</summary>
public struct SceneBackground(SceneBackgroundKind kind) : IComponent
{
    public SceneBackgroundKind Kind = kind;
}

/// <summary>A profile button's command; its <see cref="UiCommand.Text"/> is the profile id.</summary>
public enum SceneActionKind
{
    /// <summary>Selects a local profile and continues to the main menu.</summary>
    SelectProfile,
    /// <summary>Creates a new local profile and opens the screen that edits it.</summary>
    CreateProfile,
    /// <summary>Makes a profile the active one and opens the screen that edits it.</summary>
    EditProfile,
}

/// <summary>
/// What a menu button does when it is pressed, as a <see cref="UiCommand"/>: a
/// <see cref="SceneKind"/> command goes to that scene, a
/// <see cref="SceneActionKind"/> one acts on a profile.
/// </summary>
public static class SceneAction
{
    public static UiCommand Go(SceneKind target) => UiCommand.Of(target);

    public static UiCommand SelectProfile(string profileId) =>
        UiCommand.Of(SceneActionKind.SelectProfile, text: profileId);

    public static UiCommand CreateProfile() => UiCommand.Of(SceneActionKind.CreateProfile);

    public static UiCommand EditProfile(string profileId) =>
        UiCommand.Of(SceneActionKind.EditProfile, text: profileId);

    /// <summary>The menu buttons of one kind, hidden ones included.</summary>
    public static System.Collections.Generic.List<Entity> All(EntityStore store, SceneActionKind kind)
    {
        var found = new System.Collections.Generic.List<Entity>();
        foreach ((Entity entity, SceneActionKind each, _) in UiCommand.All<SceneActionKind>(store))
        {
            if (each == kind)
                found.Add(entity);
        }

        return found;
    }
}

/// <summary>The controls of the profile edit screen and the art they tint.</summary>
public struct ProfileEditView(Entity name, Entity picker, Entity art) : IComponent
{
    public Entity Name = name;
    public Entity Picker = picker;
    public Entity Art = art;

    /// <summary>The name text last handed to the profile, so only real edits rename it.</summary>
    public string Typed = "";
}

/// <summary>Song-select UI state and the text entities it updates.</summary>
public struct SongSelectState(
    int selected,
    int count,
    List<Entity> tiles,
    Entity thumb,
    List<Entity> chips,
    Entity play,
    Entity edit,
    Entity emptyTitle,
    Entity emptyHint) : IComponent
{
    public int Selected = selected;
    public int Count = count;

    /// <summary>Animated list position in levels; eases toward <see cref="Selected"/>.</summary>
    public float Scroll;

    public IReadOnlyList<LevelEntry> Catalog = [];
    public List<Entity> Tiles = tiles;
    public Entity Thumb = thumb;
    public List<Entity> Chips = chips;
    public Entity Play = play;
    public Entity Edit = edit;

    /// <summary>The hammer's parts inside the edit button.</summary>
    public List<Entity> ActionIcons = [];

    /// <summary>The button that asks to delete the selected level, and the bin inside it.</summary>
    public Entity Delete;
    public List<Entity> DeleteIcons = [];

    /// <summary>Shown instead of the rows when there are no levels: why, and the line that opens the editor.</summary>
    public Entity EmptyTitle = emptyTitle;
    public Entity EmptyHint = emptyHint;

    /// <summary>The info card's lines: difficulty name, genre, title and artist.</summary>
    public Entity InfoDifficulty;
    public Entity InfoGenre;
    public Entity InfoTitle;
    public Entity InfoArtist;

    /// <summary>The level readout in the panel under the player banner.</summary>
    public Entity LevelLabel;

    /// <summary>The panel behind the level readout; it resizes to fit the text.</summary>
    public Entity LevelPanel;

    /// <summary>The clipped viewport the score rows scroll in.</summary>
    public Entity ScoreViewport;

    /// <summary>Holds one row per score and is as tall as them; kept as a node to add rows later.</summary>
    public UiNode ScoreContent;

    /// <summary>The thumb on the scores scrollbar, and the span last drawn for it.</summary>
    public Entity ScoreThumb;
    public Vector2 ScoreThumbSpan;

    /// <summary>The invisible areas that take presses for the scores and level list scrollbars.</summary>
    public Entity ScoreTrack;
    public Entity ListTrack;

    /// <summary>The level the score rows were last filled for, so a new level starts at the top.</summary>
    public string ScoresFor = "";
}

/// <summary>Marks a song-select list the pointer is dragging, so its scroll stops easing.</summary>
public struct SongSelectDragging : ITag { }

/// <summary>A clickable level slot in the song-select grid.</summary>
public struct SongSelectTile(int index) : IComponent
{
    public int Index = index;
}

/// <summary>Scene leave confirmation popup state.</summary>
public struct SceneConfirmation(
    Entity title,
    Entity message,
    Entity yes,
    Entity no,
    Entity save) : IComponent
{
    public Entity Title = title;
    public Entity Message = message;
    public Entity Yes = yes;
    public Entity No = no;
    public Entity Save = save;
    public SceneKind Target;
}

/// <summary>The song-select right-click menu and its two actions.</summary>
public readonly struct SceneSongMenu(Entity timing, Entity delete)
{
    public readonly Entity Timing = timing;
    public readonly Entity Delete = delete;
}

/// <summary>The delete-level confirmation opened from the song-select menu.</summary>
public readonly struct SceneDeleteLevel(Entity confirm, Entity cancel)
{
    public readonly Entity Confirm = confirm;
    public readonly Entity Cancel = cancel;
}

/// <summary>The gameplay pause overlay's parts.</summary>
public readonly struct ScenePause(
    Entity title,
    Entity message,
    Entity resume,
    Entity retry,
    Entity leave)
{
    public readonly Entity Title = title;
    public readonly Entity Message = message;
    public readonly Entity Resume = resume;
    public readonly Entity Retry = retry;
    public readonly Entity Leave = leave;
}
