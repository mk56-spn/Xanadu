using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Scene-scoped button/action identifiers.</summary>
public enum SceneActionId
{
    None,
    Exit,
    Save,
    SaveAs,
    LoadSong,
    LoadLevel,
    ToggleMetadata,
    SongTiming,
    Retry,
    SongSelect,
    MainMenu,
    Resume,
    Leave,
    Pause,
    Play,
    Edit,
    Previous,
    Next,
    Back,
    WatchReplay,
}

/// <summary>Data-only command attached to a button or action entity.</summary>
public struct SceneCommand(SceneActionId action) : IComponent
{
    public SceneActionId Action = action;
    public string Text = "";
    public int Value;
}
