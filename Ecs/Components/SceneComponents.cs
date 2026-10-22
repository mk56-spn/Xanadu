using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Writer.Ecs;

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
    Entity title,
    Entity meta,
    Entity stats,
    Entity index,
    Entity scoresViewport,
    Entity scoresContent,
    List<Entity> scoreRows) : IComponent
{
    public int Selected = selected;
    public int Count = count;
    public Entity Title = title;
    public Entity Meta = meta;
    public Entity Stats = stats;
    public Entity Index = index;
    public Entity ScoresViewport = scoresViewport;
    public Entity ScoresContent = scoresContent;
    public List<Entity> ScoreRows = scoreRows;
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

/// <summary>Play HUD button that opens the pause screen.</summary>
public struct PauseAction : IComponent
{
    public int Value;
}

/// <summary>Restarts the current level from the beginning.</summary>
public struct RetryAction : IComponent;

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
