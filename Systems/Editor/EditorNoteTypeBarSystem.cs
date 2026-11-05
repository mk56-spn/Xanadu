using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The note-key bar: a row of colour swatches that shows while placing notes
/// and movement notes. Pressing one picks the key
/// new notes get; the picked swatch stays bright and the rest dim.
/// </summary>
public sealed class EditorNoteTypeBarSystem : QuerySystem<EditorButton>
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

        // A press can land on a control that hides itself the same frame.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorPlacement>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        bool visible = ShowsFor(editor.GetComponent<EditorPlacement>().Mode);
        KeybindAction target = editor.GetComponent<EditorPlacement>().Note;
        CommandBuffer buffer = CommandBuffer;

        foreach (Entity panel in _entities.Query<EditorNoteBarPanel>().WithDisabled().ToEntityList())
            SetShown(panel, visible, buffer);

        Query.ForEachEntity((ref button, entity) =>
        {
            if (button.Kind != EditorButtonKind.NoteType)
                return;

            var action = (KeybindAction)button.Arg;
            bool pressed = visible && entity.Tags.Has<Pressed>();
            bool wasPressed = _held.Contains(entity.Id);
            if (pressed)
                _held.Add(entity.Id);
            else
                _held.Remove(entity.Id);

            if (pressed && !wasPressed)
            {
                _commands.Enqueue(EditorCommand.SetTarget(action));
                target = action;
            }

            Shade(entity, action, action == target, buffer);
        });
    }

    /// <summary>
    /// Normal, dash and momentum notes carry a specific key and pair notes take
    /// their number from the selected key.
    /// </summary>
    public static bool ShowsFor(EditorPlacementMode mode) =>
        mode is EditorPlacementMode.Notes or
            EditorPlacementMode.Dash or
            EditorPlacementMode.Momentum or
            EditorPlacementMode.Pair or
            EditorPlacementMode.Channel or
            EditorPlacementMode.FormStep;

    /// <summary>The unselected variant of a note colour.</summary>
    public static Color Dimmed(Color color) => color.Lerp(DimTo, Dimming);

    private static void Shade(
        Entity swatch,
        KeybindAction action,
        bool selected,
        CommandBuffer buffer)
    {
        if (!swatch.HasComponent<ButtonVisual>())
            return;

        Color color = PlayfieldRenderer.ColorFor(action);
        Color normal = selected ? color : Dimmed(color);
        ref ButtonVisual visual = ref swatch.GetComponent<ButtonVisual>();
        if (visual.Normal == normal)
            return;

        visual.Normal = normal;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
        visual.Flash = Colors.White;
        buffer.AddTag<Dirty>(swatch.Id);
    }

    /// <summary>
    /// A shown bar rejoins its container, which places it; a hidden one leaves
    /// the layout and is hidden.
    /// </summary>
    private static void SetShown(Entity panel, bool shown, CommandBuffer buffer) =>
        UiLayout.SetHidden(panel, buffer, !shown);
}
