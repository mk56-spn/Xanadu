using Godot;
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
}

/// <summary>Persistent game scene state and the next requested scene.</summary>
public struct SceneState(SceneKind scene) : IComponent
{
    public SceneKind Current = scene;
    public SceneKind Pending = scene;
    public int SelectedIndex;
    public string SelectedLevelPath = "";
    public string SelectedSongId = "";
    public SceneKind SongTimingBack = SceneKind.Editor;
}

/// <summary>Marks the root entity that owns a scene's entity subtree.</summary>
public struct SceneRoot(SceneKind scene) : IComponent
{
    public SceneKind Scene = scene;
}

/// <summary>Requests a scene change when its ECS button is pressed.</summary>
public struct SceneAction(SceneKind target) : IComponent
{
    public SceneKind Target = target;
}

/// <summary>Selects a local profile and continues to the main menu.</summary>
public struct ProfileSelect(string profileId) : IComponent
{
    public string ProfileId = profileId;
}

/// <summary>Creates a new local profile and continues to the main menu.</summary>
public struct ProfileCreate : IComponent;

/// <summary>Opens the editor with the currently selected song-select level.</summary>
public struct EditLevelAction : IComponent;

/// <summary>Moves the song-select carousel by one entry.</summary>
public struct CarouselStep(int direction) : IComponent
{
    public int Direction = direction;
}

/// <summary>Song-select UI state and the text entities it updates.</summary>
public struct SongSelectState(
    int selected,
    int count,
    List<Entity> tiles,
    List<Entity> names,
    List<Entity> songs,
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
    public List<Entity> Names = names;
    public List<Entity> Songs = songs;
    public Entity Thumb = thumb;
    public List<Entity> Chips = chips;
    public Entity Play = play;
    public Entity Edit = edit;

    /// <summary>The icon shapes inside the play and edit buttons: the play triangle first, then the hammer's parts.</summary>
    public List<Entity> ActionIcons = [];

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

    /// <summary>Holds every score row; it is as tall as the rows in use.</summary>
    public Entity ScoreContent;

    /// <summary>One black bar, accuracy text and score text per pooled row.</summary>
    public List<Entity> ScoreBars = [];
    public List<Entity> ScoreAccuracies = [];
    public List<Entity> ScoreValues = [];

    /// <summary>The thumb on the scores scrollbar, and the span last drawn for it.</summary>
    public Entity ScoreThumb;
    public Vector2 ScoreThumbSpan;

    /// <summary>The level the score rows were last filled for, so a new level starts at the top.</summary>
    public string ScoresFor = "";
}

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
    public bool Visible;
}

/// <summary>Gameplay pause overlay state.</summary>
public struct ScenePause(
    Entity title,
    Entity message,
    Entity resume,
    Entity retry,
    Entity leave) : IComponent
{
    public Entity Title = title;
    public Entity Message = message;
    public Entity Resume = resume;
    public Entity Retry = retry;
    public Entity Leave = leave;
    public bool Visible;
}
