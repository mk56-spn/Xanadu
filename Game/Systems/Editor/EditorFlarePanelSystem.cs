using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>The flare tool's controls on the editor entity: one readout per setting, in <see cref="FlareSetting"/> order, and the colour.</summary>
public struct EditorFlarePanel(Entity section, Entity[] readouts, Entity color) : IComponent
{
    public Entity Section = section;
    public Entity[] Readouts = readouts;
    public Entity Color = color;
}

/// <summary>
/// Tunes flares from the flare tool's panel, which the tool section system
/// shows while the flare tool is out. With flares selected the controls
/// change the selected flares, each change one undo step, and the brush
/// follows so the next flare matches; with none selected they change the
/// brush alone. A typed colour goes to what was selected when typing began.
/// </summary>
public sealed class EditorFlarePanelSystem(EditorHistory history)
    : QuerySystem<NoteSelection, EditorFlarePanel, EditorPlacement>
{
    private readonly ButtonPress _presses = new();
    private readonly EditorColorEdit _color = new(
        history,
        IsFlare,
        (item, color) => item.GetComponent<LevelFlare>().Color = color,
        (editor, color) => editor.GetComponent<EditorPlacement>().Flare.Color = color);

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        EditorFlarePanel panel = editor.GetComponent<EditorFlarePanel>();
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Entity first = EditorSelectionScan.First(selection, IsFlare);

        // A dragged picker colours the flares as it moves; the drag is one undo step.
        _color.Update(editor, panel.Color, selection);

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.FlareStep))
        {
            if (_presses.Started(button))
                Step(editor, selection, first, button.GetComponent<UiCommand>().Arg);
        }

        LevelFlare shown = first.IsNull
            ? editor.GetComponent<EditorPlacement>().Flare
            : first.GetComponent<LevelFlare>();
        UiColorField.Show(panel.Color, shown.Color);
        for (int i = 0; i < panel.Readouts.Length; i++)
            UiText.Set(panel.Readouts[i], Label((FlareSetting)i, shown), CommandBuffer);
    }

    /// <summary>A setting's readout: its name, then its value in the units it is stepped in.</summary>
    public static string Label(FlareSetting setting, LevelFlare flare) => setting switch
    {
        FlareSetting.Width => $"WIDTH {flare.Width:0}",
        FlareSetting.Height => $"HEIGHT {flare.Height:0}",
        FlareSetting.Axes => $"AXES {flare.Axes}",
        FlareSetting.Thickness => $"THICKNESS {flare.Thickness * 100:0}%",
        FlareSetting.Falloff => $"FALLOFF {flare.Falloff:0.00}",
        FlareSetting.Core => $"CORE {flare.Core * 100:0}%",
        FlareSetting.Halo => $"HALO {flare.Halo * 100:0}%",
        FlareSetting.Glow => $"GLOW {flare.Glow * 100:0}%",
        FlareSetting.Intensity => $"INTENSITY {flare.Intensity:0.0}",
        _ => $"HEAT {flare.Heat * 100:0}%",
    };

    /// <summary>The argument a step button carries: the setting plus one, negative for less.</summary>
    public static int StepArg(FlareSetting setting, int sign) => ((int)setting + 1) * sign;

    private static bool IsFlare(Entity item) => !item.IsNull && item.HasComponent<LevelFlare>();

    /// <summary>
    /// The selected flares all take the first one's value plus the step, so a
    /// mixed selection ends up matching; the brush takes the first one's settings.
    /// </summary>
    private void Step(Entity editor, NoteSelection selection, Entity first, int arg)
    {
        if (arg == 0)
            return;

        var setting = (FlareSetting)(System.Math.Abs(arg) - 1);
        float delta = System.Math.Sign(arg) * LevelFlare.Step(setting);
        ref LevelFlare brush = ref editor.GetComponent<EditorPlacement>().Flare;

        if (first.IsNull)
        {
            brush.Set(setting, brush.Get(setting) + delta);
            return;
        }

        float value = first.GetComponent<LevelFlare>().Get(setting) + delta;
        EditorSnapshot before = history.Capture();
        foreach (Entity item in selection.AllNotes)
        {
            if (IsFlare(item))
                item.GetComponent<LevelFlare>().Set(setting, value);
        }

        history.Record(before, history.Capture());
        brush = first.GetComponent<LevelFlare>() with { Position = default, RotationDegrees = 0f };
    }
}
