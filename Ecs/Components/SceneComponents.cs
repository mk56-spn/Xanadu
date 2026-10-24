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
    List<Entity> metas,
    Entity play,
    Entity edit,
    string playLabel,
    string editLabel) : IComponent
{
    public int Selected = selected;
    public int Count = count;
    public List<Entity> Tiles = tiles;
    public List<Entity> Names = names;
    public List<Entity> Metas = metas;
    public Entity Play = play;
    public Entity Edit = edit;
    public string PlayLabel = playLabel;
    public string EditLabel = editLabel;
    public int PageStart;
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
