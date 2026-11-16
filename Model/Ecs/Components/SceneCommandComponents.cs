using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Ecs.Components;

/// <summary>Scene-scoped button/action identifiers, carried as a <see cref="UiCommand"/>.</summary>
public enum SceneActionId
{
    None,
    Exit,
    Save,
    LoadSong,
    ToggleMetadata,
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
    AddSong,
    DeleteLevel,
}
