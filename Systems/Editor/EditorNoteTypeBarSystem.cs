using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;
using Writer.Settings;
using Writer.Systems.Playfields;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Editor;

/// <summary>
/// The note-type bar: a row of colour swatches that shows only in note mode.
/// Pressing one picks the type new notes get; the picked swatch stays bright
/// and the rest dim.
/// </summary>
public sealed class EditorNoteTypeBarSystem : QuerySystem<EditorNoteSwatch>
{
    private static readonly Color DimTo = new(0.08f, 0.1f, 0.16f);
    private const float Dimming = 0.6f;

    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly HashSet<int> _held = new();

    public EditorNoteTypeBarSystem(EntityStore entities, EditorCommandBus commands)
    {
        _entities = entities;
        _commands = commands;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorPlacementModeState, EditorNoteTarget>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        bool visible = editor.GetComponent<EditorPlacementModeState>().Mode ==
                       EditorPlacementMode.Notes;
        KeybindAction target = editor.GetComponent<EditorNoteTarget>().Target;
        CommandBuffer buffer = CommandBuffer;

        foreach (Entity panel in _entities.Query<EditorNoteBarPanel>().ToEntityList())
        {
            Move(panel, visible ? panel.GetComponent<EditorNoteBarPanel>().Home : UiLayout.Hidden, buffer);
        }

        Query.ForEachEntity((ref swatch, entity) =>
        {
            Move(entity, visible ? swatch.Home : UiLayout.Hidden, buffer);

            bool pressed = visible && entity.Tags.Has<Pressed>();
            bool wasPressed = _held.Contains(entity.Id);
            if (pressed)
                _held.Add(entity.Id);
            else
                _held.Remove(entity.Id);

            if (pressed && !wasPressed)
            {
                _commands.Enqueue(EditorCommand.SetTarget(swatch.Action));
                target = swatch.Action;
            }

            Shade(entity, swatch.Action, swatch.Action == target, buffer);
        });
    }

    private static void Shade(
        Entity swatch,
        KeybindAction action,
        bool selected,
        CommandBuffer buffer)
    {
        if (!swatch.HasComponent<ButtonVisual>())
            return;

        Color color = PlayfieldRenderer.ColorFor(action);
        Color normal = selected ? color : color.Lerp(DimTo, Dimming);
        ref ButtonVisual visual = ref swatch.GetComponent<ButtonVisual>();
        if (visual.Normal == normal)
            return;

        visual.Normal = normal;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
        visual.Flash = Colors.White;
        buffer.AddTag<Dirty>(swatch.Id);
    }

    private static void Move(Entity entity, Vector2 target, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        ref Position position = ref entity.GetComponent<Position>();
        if (position.Value == target)
            return;

        position.Value = target;
        buffer.AddTag<Moved>(entity.Id);
    }
}
