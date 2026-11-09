using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Physics;

namespace Xanadu.Systems.Editor;

/// <summary>What a dash exit stepper row changes.</summary>
public enum DashExitSetting
{
    Speed,
    Direction,
}

/// <summary>The dash tool's exit controls on the editor entity: one readout per setting, in <see cref="DashExitSetting"/> order.</summary>
public struct EditorDashExitPanel(Entity[] readouts) : IComponent
{
    public Entity[] Readouts = readouts;
}

/// <summary>
/// Sets the push out of a dash from the dash tool's panel. With dash notes
/// selected the steppers change them, each step one undo step, and the brush
/// follows so the next dash matches; with none selected they change the brush.
/// </summary>
public sealed class EditorDashExitPanelSystem(EditorHistory history)
    : QuerySystem<NoteSelection, EditorDashExitPanel, EditorPlacement>
{
    /// <summary>How much one step changes the push, in playfield units per second.</summary>
    public const float SpeedStep = 100f;

    private readonly ButtonPress _presses = new();

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        EditorDashExitPanel panel = editor.GetComponent<EditorDashExitPanel>();
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Entity first = EditorSelectionScan.First(selection, IsDash);

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.DashExitStep))
        {
            if (_presses.Started(button))
                Step(editor, selection, first, button.GetComponent<EditorButton>().Arg);
        }

        (DashDirection direction, float speed) = Shown(editor, first);
        for (int i = 0; i < panel.Readouts.Length; i++)
            UiText.Set(panel.Readouts[i], Label((DashExitSetting)i, direction, speed), CommandBuffer);
    }

    private static (DashDirection Direction, float Speed) Shown(Entity editor, Entity first)
    {
        if (first.IsNull)
        {
            EditorPlacement brush = editor.GetComponent<EditorPlacement>();
            return (brush.ExitDirection, brush.ExitMomentum);
        }

        Note note = first.GetComponent<Note>();
        return (note.ExitDirection, note.ExitMomentum);
    }

    /// <summary>A setting's readout: its name, then its value.</summary>
    public static string Label(DashExitSetting setting, DashDirection direction, float speed) => setting switch
    {
        DashExitSetting.Speed => speed > 0f ? $"EXIT SPEED {speed:0}" : "EXIT SPEED OFF",
        _ => $"EXIT {Name(direction)}",
    };

    private static string Name(DashDirection direction) => direction switch
    {
        DashDirection.Up => "UP",
        DashDirection.UpRight => "UP RIGHT",
        DashDirection.Right => "RIGHT",
        DashDirection.DownRight => "DN RIGHT",
        DashDirection.Down => "DOWN",
        DashDirection.DownLeft => "DN LEFT",
        DashDirection.Left => "LEFT",
        _ => "UP LEFT",
    };

    /// <summary>The argument a step button carries: the setting plus one, negative for less.</summary>
    public static int StepArg(DashExitSetting setting, int sign) => ((int)setting + 1) * sign;

    /// <summary>The push one step away, from off up to the strongest momentum.</summary>
    public static float SteppedSpeed(float speed, int sign) =>
        Math.Clamp(MathF.Round(speed / SpeedStep) * SpeedStep + sign * SpeedStep, 0f, Box2DPhysicsWorld.MaxMomentum);

    /// <summary>The way one eighth of a turn round, clockwise for a positive sign.</summary>
    public static DashDirection SteppedDirection(DashDirection direction, int sign) =>
        (DashDirection)(((int)direction + sign + 8) % 8);

    private static bool IsDash(Entity item) =>
        !item.IsNull && item.HasComponent<Note>() && item.GetComponent<Note>().Kind == NoteKind.Dash;

    /// <summary>
    /// The selected dash notes all take the first one's value plus the step,
    /// so a mixed selection ends up matching; the brush takes the result.
    /// </summary>
    private void Step(Entity editor, NoteSelection selection, Entity first, int arg)
    {
        if (arg == 0)
            return;

        var setting = (DashExitSetting)(Math.Abs(arg) - 1);
        int sign = Math.Sign(arg);
        ref EditorPlacement brush = ref editor.GetComponent<EditorPlacement>();
        (DashDirection direction, float speed) = Shown(editor, first);
        if (setting == DashExitSetting.Speed)
            speed = SteppedSpeed(speed, sign);
        else
            direction = SteppedDirection(direction, sign);

        brush.ExitDirection = direction;
        brush.ExitMomentum = speed;
        if (first.IsNull)
            return;

        EditorSnapshot before = history.Capture();
        foreach (Entity item in selection.AllNotes)
        {
            if (!IsDash(item))
                continue;

            ref Note note = ref item.GetComponent<Note>();
            note.ExitDirection = direction;
            note.ExitMomentum = speed;
        }

        history.Record(before, history.Capture());
    }
}
