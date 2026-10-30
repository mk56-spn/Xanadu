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
/// or dash notes, the two kinds that carry a key. Pressing one picks the key
/// new notes get; the picked swatch stays bright and the rest dim.
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

        bool visible = ShowsFor(editor.GetComponent<EditorPlacementModeState>().Mode);
        KeybindAction target = editor.GetComponent<EditorNoteTarget>().Target;
        CommandBuffer buffer = CommandBuffer;

        foreach (Entity panel in _entities.Query<EditorNoteBarPanel>().ToEntityList())
            SetShown(panel, visible, buffer);

        Query.ForEachEntity((ref swatch, entity) =>
        {
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

    /// <summary>
    /// Normal notes carry a specific key and pair notes take their number from
    /// the selected key; dash and momentum notes are any-key and hide the bar.
    /// </summary>
    public static bool ShowsFor(EditorPlacementMode mode) =>
        mode is EditorPlacementMode.Notes or
            EditorPlacementMode.Pair or
            EditorPlacementMode.Channel or
            EditorPlacementMode.FormStep;

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

    /// <summary>
    /// A shown bar rejoins its container, which places it; a hidden one leaves
    /// the layout and parks off screen.
    /// </summary>
    private static void SetShown(Entity panel, bool shown, CommandBuffer buffer)
    {
        if (shown == !panel.Tags.Has<LayoutIgnore>())
            return;

        if (!shown)
        {
            UiLayout.Hide(panel, buffer);
            return;
        }

        UiLayout.SetIgnored(panel, buffer, false);
        Entity parent = panel.Parent;
        if (!parent.IsNull && parent.HasComponent<UiContainer>())
            buffer.AddTag<LayoutDirty>(parent.Id);
    }
}
