using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor.Analysis;
using Xanadu.Timeline;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>Everything one editor frame draws with, read once per tick.</summary>
public readonly record struct EditorFrame(
    Entity Entity,
    PlayfieldView View,
    Playfield Playfield,
    Playhead Playhead,
    NoteSelection Selection,
    TimelineView Timeline,
    WaveformPreview Preview,
    UiMarqueeBox Box,
    LevelGroups Groups,
    EditorGroupState GroupState,
    EditorAnalysis Analysis,
    Color? Background,
    Color? Outside,
    Color? Frame,
    PlayerPath? Path,
    float Seconds);
