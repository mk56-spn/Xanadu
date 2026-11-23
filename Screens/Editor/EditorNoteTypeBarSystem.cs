using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Logic.Editor;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor.Intents;
using Xanadu.Model.Playfields;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Editor;

/// <summary>
/// The note-key bar: a row of colour swatches that shows while placing notes
/// and movement notes. Pressing one picks the key
/// new notes get; the picked swatch stays bright and the rest dim.
/// </summary>
public sealed class EditorNoteTypeBarSystem : QuerySystem<UiCommand>
{
    private static readonly Color DimTo = new(0.08f, 0.1f, 0.16f);
    private const float Dimming = 0.6f;

    private readonly EntityStore _entities;
    private readonly ButtonPress _presses = new();

    public EditorNoteTypeBarSystem(EntityStore entities)
    {
        _entities = entities;

        // A press can land on a control that hides itself the same frame.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<EditorPlacement>(_entities);

        if (editor.IsNull)
            return;

        bool visible = ShowsFor(editor.GetComponent<EditorPlacement>().Mode);
        KeybindAction target = editor.GetComponent<EditorPlacement>().Note;
        CommandBuffer buffer = CommandBuffer;

        foreach (Entity panel in _entities.Query().AllTags(Tags.Get<EditorNoteBarPanel>()).WithDisabled().Entities)
        {
            SetShown(panel, visible, buffer);
            Outline(panel, PlayfieldColors.ColorFor(target), buffer);
        }

        Query.EachEntity(new RunNoteTypes(Query.Store, CommandBuffer, _presses, editor, visible, target));
    }

    /// <summary>Action notes carry a key: the bar picks which.</summary>
    public static bool ShowsFor(EditorPlacementMode mode) =>
        mode is EditorPlacementMode.Note;

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

        Color color = PlayfieldColors.ColorFor(action);
        Color normal = selected ? color : Dimmed(color);
        ref ButtonVisual visual = ref swatch.GetComponent<ButtonVisual>();
        if (visual.Normal == normal)
            return;

        visual.Normal = normal;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
        visual.Flash = Colors.White;
        buffer.AddTag<Dirty>(swatch.Id);
    }

    /// <summary>The box's outline takes the picked key's colour, so the choice reads at a glance.</summary>
    private static void Outline(Entity panel, Color color, CommandBuffer buffer)
    {
        if (!panel.HasComponent<UiBorder>())
            return;

        ref UiBorder border = ref panel.GetComponent<UiBorder>();
        if (border.Color == color)
            return;

        border.Color = color;
        buffer.AddTag<Dirty>(panel.Id);
    }

    /// <summary>
    /// A shown bar rejoins its container, which places it; a hidden one leaves
    /// the layout and is hidden.
    /// </summary>
    private static void SetShown(Entity panel, bool shown, CommandBuffer buffer) =>
        UiLayout.SetHidden(panel, buffer, !shown);

    private struct RunNoteTypes(EntityStore Store, CommandBuffer Buffer, ButtonPress Presses, Entity Editor, bool Visible, KeybindAction Target) : IEachEntity<UiCommand>
    {
        private KeybindAction Target = Target;

        public void Execute(ref UiCommand button, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (!button.Is(EditorButtonKind.NoteType))
                return;

            var action = (KeybindAction)button.Arg;
            if (Presses.Started(entity, Visible && entity.Tags.Has<Pressed>()))
            {
                Editor.EmitSignal(new SetNoteTarget(action));
                Target = action;
            }

            Shade(entity, action, action == Target, Buffer);
        }
    }
}
