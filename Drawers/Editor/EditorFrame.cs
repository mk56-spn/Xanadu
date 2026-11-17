using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Drawers.Editor;

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
    Color? Background,
    Color? Outside,
    Color? Frame,
    PlayerPath? Path,
    float Seconds);
