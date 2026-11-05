using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>The grass tool's controls on the editor entity: one readout per setting, in <see cref="GrassSetting"/> order, and the colour.</summary>
public struct EditorGrassPanel(Entity section, Entity[] readouts, Entity color) : IComponent
{
    public Entity Section = section;
    public Entity[] Readouts = readouts;
    public Entity Color = color;
}

/// <summary>
/// Tunes grass from the grass tool's panel, which the tool section system
/// shows while the grass tool is out. With grass selected the controls change
/// the selected patches, each change one undo step, and the brush follows so
/// the next patch matches; with none selected they change the brush alone. As
/// with the other colour fields, a typed colour goes to what was selected when
/// typing began.
/// </summary>
public sealed class EditorGrassPanelSystem(EditorHistory history)
    : QuerySystem<NoteSelection, EditorGrassPanel, EditorPlacement>
{
    private readonly List<Entity> _targets = new();
    private readonly HashSet<int> _held = new();
    private EditorSnapshot? _before;
    private bool _wasEditing;

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        EditorGrassPanel panel = editor.GetComponent<EditorGrassPanel>();
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Entity first = FirstGrass(selection);
        bool editing = UiColorField.IsEditing(panel.Color);

        if (editing && !_wasEditing)
        {
            CollectTargets(selection);
            _before = history.Capture();
        }
        _wasEditing = editing;

        // A dragged picker colours the grass as it moves; the drag is one undo step.
        if (UiColorField.TakeLive(panel.Color, out Color live))
            PutColor(editor, live);
        if (UiColorField.TakeCommit(panel.Color, out Color color))
            ApplyColor(editor, color);
        if (!editing)
            _before = null;

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.GrassStep))
        {
            bool pressed = button.Tags.Has<Pressed>();
            bool was = !_held.Add(button.Id);
            if (!pressed)
                _held.Remove(button.Id);
            if (pressed && !was)
                Step(editor, selection, first, button.GetComponent<EditorButton>().Arg);
        }

        GrassPatch shown = first.IsNull
            ? editor.GetComponent<EditorPlacement>().Grass
            : first.GetComponent<GrassPatch>();
        UiColorField.Show(panel.Color, shown.Color);
        for (int i = 0; i < panel.Readouts.Length; i++)
            UiText.Set(panel.Readouts[i], Label((GrassSetting)i, shown), CommandBuffer);
    }

    /// <summary>A setting's readout: its name, then its value in the units it is stepped in.</summary>
    public static string Label(GrassSetting setting, GrassPatch patch) => setting switch
    {
        GrassSetting.Width => $"WIDTH {patch.Width:0}",
        GrassSetting.Length => $"LENGTH {patch.Length:0}",
        GrassSetting.Density => $"DENSITY {patch.Density:0}",
        GrassSetting.Sway => $"SWAY {patch.Sway:0}°",
        _ => $"VARIATION {patch.Variation * 100:0}%",
    };

    /// <summary>The argument a step button carries: the setting plus one, negative for less.</summary>
    public static int StepArg(GrassSetting setting, int sign) => ((int)setting + 1) * sign;

    private static Entity FirstGrass(NoteSelection selection)
    {
        foreach (Entity item in selection.AllNotes)
        {
            if (IsGrass(item))
                return item;
        }

        return default;
    }

    private static bool IsGrass(Entity item) => !item.IsNull && item.HasComponent<GrassPatch>();

    private void CollectTargets(NoteSelection selection)
    {
        _targets.Clear();
        foreach (Entity item in selection.AllNotes)
        {
            if (IsGrass(item))
                _targets.Add(item);
        }
    }

    private void ApplyColor(Entity editor, Color color)
    {
        _targets.RemoveAll(item => !IsGrass(item));
        if (_targets.Count == 0)
        {
            PutColor(editor, color);
            return;
        }

        EditorSnapshot before = _before ?? history.Capture();
        PutColor(editor, color);
        history.Record(before, history.Capture());
    }

    private void PutColor(Entity editor, Color color)
    {
        editor.GetComponent<EditorPlacement>().Grass.Color = color;
        _targets.RemoveAll(item => !IsGrass(item));
        foreach (Entity item in _targets)
            item.GetComponent<GrassPatch>().Color = color;
    }

    /// <summary>
    /// The selected patches all take the first one's value plus the step, so a
    /// mixed selection ends up matching; the brush takes the first one's settings.
    /// </summary>
    private void Step(Entity editor, NoteSelection selection, Entity first, int arg)
    {
        if (arg == 0)
            return;

        var setting = (GrassSetting)(System.Math.Abs(arg) - 1);
        float delta = System.Math.Sign(arg) * GrassPatch.Step(setting);
        ref GrassPatch brush = ref editor.GetComponent<EditorPlacement>().Grass;

        if (first.IsNull)
        {
            brush.Set(setting, brush.Get(setting) + delta);
            return;
        }

        float value = first.GetComponent<GrassPatch>().Get(setting) + delta;
        EditorSnapshot before = history.Capture();
        foreach (Entity item in selection.AllNotes)
        {
            if (IsGrass(item))
                item.GetComponent<GrassPatch>().Set(setting, value);
        }

        history.Record(before, history.Capture());
        brush = first.GetComponent<GrassPatch>() with { Position = default, Seed = 0 };
    }
}
