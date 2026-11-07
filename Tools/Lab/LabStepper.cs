using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Lab;

/// <summary>A side-panel stepper: a label, then - and + buttons, read as fresh presses.</summary>
public sealed class LabStepper
{
    private readonly UiNode _label;
    private readonly Entity _minus;
    private readonly Entity _plus;
    private bool _minusHeld;
    private bool _plusHeld;

    public LabStepper(UiNode panel, string text = "")
    {
        UiNode row = panel.Row().Gap(UiMetrics.Space.Sm);
        _label = row.Text(text, UiType.Label, Palette.Text).Width(220);
        _minus = row.Button("-", UiType.Label).Size(UiMetrics.Control.Md, UiMetrics.Control.Md);
        _plus = row.Button("+", UiType.Label).Size(UiMetrics.Control.Md, UiMetrics.Control.Md);
    }

    /// <summary>-1 on a fresh press of -, +1 on one of +, otherwise 0. Call once a frame.</summary>
    public int Step() => (Pressed(_plus, ref _plusHeld) ? 1 : 0) - (Pressed(_minus, ref _minusHeld) ? 1 : 0);

    /// <summary><paramref name="index"/> moved by this frame's step, kept within <paramref name="count"/> steps.</summary>
    public int Clamped(int index, int count) => Math.Clamp(index + Step(), 0, count - 1);

    /// <summary><paramref name="index"/> moved by this frame's step, wrapping around <paramref name="count"/> steps.</summary>
    public int Wrapped(int index, int count) => ((index + Step()) % count + count) % count;

    public void SetText(string text) => _label.SetText(text);

    /// <summary>True on the first frame a button is down.</summary>
    public static bool Pressed(Entity button, ref bool held)
    {
        bool down = !button.IsNull && button.Tags.Has<Pressed>();
        bool fresh = down && !held;
        held = down;
        return fresh;
    }
}
