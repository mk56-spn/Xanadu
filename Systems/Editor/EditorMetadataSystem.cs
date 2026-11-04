using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Levels;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

/// <summary>Shows the ECS metadata panel and syncs its text fields to editor state.</summary>
public sealed class EditorMetadataSystem : QuerySystem<EditorMetadataPanel>
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;

    public EditorMetadataSystem(EntityStore entities, EditorCommandBus commands)
    {
        _entities = entities;
        _commands = commands;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorMetadataPanel, LevelInfo, Playhead>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        ref EditorMetadataPanel panel =
            ref editor.GetComponent<EditorMetadataPanel>();

        if (editor.HasComponent<EditorUiRequest>())
        {
            ref EditorUiRequest request = ref editor.GetComponent<EditorUiRequest>();
            if (request.Kind == EditorButtonKind.ToggleMetadata)
            {
                request.Kind = EditorButtonKind.None;
                if (!panel.Panel.IsNull &&
                    panel.Panel.HasComponent<Position>())
                {
                    panel.Panel.GetComponent<Position>().Value = panel.Home;
                    CommandBuffer buffer = CommandBuffer;
                    buffer.AddTag<Moved>(panel.Panel.Id);
                    UiLayout.SetHidden(panel.Panel, buffer, !UiLayout.IsHidden(panel.Panel));
                }
            }
        }

        LevelInfo level = editor.GetComponent<LevelInfo>();
        Playhead playhead = editor.GetComponent<Playhead>();
        UpdateBackgroundColorControl(level);
        bool hasSelectedStart = TryGetSelectedStart(
            editor,
            out Entity selectedStartEntity,
            out StartPoint selectedStart);

        if (editor.HasComponent<EditorStartPanel>() &&
            editor.GetComponent<EditorStartPanel>().Panel is { IsNull: false } startPanelEntity)
        {
            ref EditorStartPanel startPanel =
                ref editor.GetComponent<EditorStartPanel>();
            bool visible = hasSelectedStart;
            if (UiLayout.IsHidden(startPanelEntity) == visible)
            {
                if (startPanelEntity.HasComponent<Position>())
                {
                    startPanelEntity.GetComponent<Position>().Value = startPanel.Home;
                    CommandBuffer startBuffer = CommandBuffer;
                    startBuffer.AddTag<Moved>(startPanelEntity.Id);
                    UiLayout.SetHidden(startPanelEntity, startBuffer, !visible);
                }
            }
        }

        foreach (Entity entity in _entities
                     .Query<EditorField, TextField>()
                     .WithDisabled()
                     .ToEntityList())
        {
            EditorField binding = entity.GetComponent<EditorField>();
            ref TextField field = ref entity.GetComponent<TextField>();
            bool focused = entity.Tags.Has<TextFieldFocused>();

            bool hasStart = hasSelectedStart;
            Entity startEntity = selectedStartEntity;
            StartPoint start = selectedStart;
            if (!focused)
            {
                string expected = binding.Kind switch
                {
                    EditorFieldKind.LevelName => level.Name,
                    EditorFieldKind.Artist => level.Artist,
                    EditorFieldKind.Difficulty => level.Difficulty,
                    EditorFieldKind.Bpm => playhead.Bpm.ToString(),
                    EditorFieldKind.Offset => playhead.OffsetMs.ToString(),
                    EditorFieldKind.StartTime => hasStart
                        ? start.TimeMs.ToString()
                        : "",
                    EditorFieldKind.StartMomentumX => hasStart
                        ? start.Momentum.X.ToString("0.###", CultureInfo.InvariantCulture)
                        : "",
                    EditorFieldKind.StartMomentumY => hasStart
                        ? start.Momentum.Y.ToString("0.###", CultureInfo.InvariantCulture)
                        : "",
                    _ => "",
                };
                if (field.Value != expected)
                    field.Value = expected;
                continue;
            }

            switch (binding.Kind)
            {
                case EditorFieldKind.LevelName:
                    if (field.Value != level.Name)
                        _commands.Enqueue(EditorCommand.SetName(field.Value));
                    break;
                case EditorFieldKind.Artist:
                    if (field.Value != level.Artist)
                        _commands.Enqueue(EditorCommand.SetArtist(field.Value));
                    break;
                case EditorFieldKind.Difficulty:
                    if (field.Value != level.Difficulty)
                        _commands.Enqueue(EditorCommand.SetDifficulty(field.Value));
                    break;
                case EditorFieldKind.Bpm:
                    if (int.TryParse(field.Value, out int bpm) &&
                        bpm != playhead.Bpm)
                    {
                        _commands.Enqueue(EditorCommand.SetBpm(bpm));
                    }
                    break;
                case EditorFieldKind.Offset:
                    if (long.TryParse(field.Value, out long offset) &&
                        offset != playhead.OffsetMs)
                    {
                        _commands.Enqueue(EditorCommand.SetOffset(offset));
                    }
                    break;
                case EditorFieldKind.StartTime when hasStart:
                    if (long.TryParse(field.Value, out long startTime))
                    {
                        ref StartPoint point = ref startEntity.GetComponent<StartPoint>();
                        point.TimeMs = Math.Max(0, startTime);
                    }
                    break;
                case EditorFieldKind.StartMomentumX when hasStart:
                    if (float.TryParse(
                            field.Value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float momentumX))
                    {
                        ref StartPoint point = ref startEntity.GetComponent<StartPoint>();
                        point.Momentum.X = momentumX;
                    }
                    break;
                case EditorFieldKind.StartMomentumY when hasStart:
                    if (float.TryParse(
                            field.Value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float momentumY))
                    {
                        ref StartPoint point = ref startEntity.GetComponent<StartPoint>();
                        point.Momentum.Y = momentumY;
                    }
                    break;
            }
        }
    }

    /// <summary>Mirrors the level's background and outside colours onto their metadata swatches and hex labels.</summary>
    private void UpdateBackgroundColorControl(LevelInfo level)
    {
        ShowColor(
            level.Background,
            LevelBackground.Default,
            _entities.Query<EditorBackgroundSwatch, FillColor>().WithDisabled().ToEntityList(),
            _entities.Query<EditorBackgroundHex, Text>().WithDisabled().ToEntityList());
        ShowColor(
            level.Outside,
            LevelBackground.OutsideDefault,
            _entities.Query<EditorOutsideSwatch, FillColor>().WithDisabled().ToEntityList(),
            _entities.Query<EditorOutsideHex, Text>().WithDisabled().ToEntityList());
        ShowColor(
            level.Frame,
            LevelBackground.FrameDefault,
            _entities.Query<EditorFrameSwatch, FillColor>().WithDisabled().ToEntityList(),
            _entities.Query<EditorFrameHex, Text>().WithDisabled().ToEntityList());
    }

    private void ShowColor(
        Color? stored,
        Color fallback,
        System.Collections.Generic.IEnumerable<Entity> swatches,
        System.Collections.Generic.IEnumerable<Entity> hexes)
    {
        Color color = stored ?? fallback;
        foreach (Entity entity in swatches)
        {
            ref FillColor fill = ref entity.GetComponent<FillColor>();
            if (fill.Value == color)
                continue;

            fill.Value = color;
            CommandBuffer.AddTag<Dirty>(entity.Id);
        }

        string label = stored is { } value
            ? LevelBackground.Format(value)
            : "DEFAULT";
        foreach (Entity entity in hexes)
        {
            ref Text text = ref entity.GetComponent<Text>();
            if (text.Value == label)
                continue;

            text.Value = label;
            CommandBuffer.AddTag<Dirty>(entity.Id);
        }
    }

    private static bool TryGetSelectedStart(
        Entity editor,
        out Entity startEntity,
        out StartPoint start)
    {
        startEntity = default;
        start = default;
        if (!editor.HasComponent<NoteSelection>())
            return false;

        foreach (Entity entity in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!entity.HasComponent<StartPoint>())
                continue;

            startEntity = entity;
            start = entity.GetComponent<StartPoint>();
            return true;
        }

        return false;
    }
}
