using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator;

/// <summary>
/// Every keybinding in the animator: the actions buttons perform, plus the key
/// that does the same thing. Shortcuts run them through
/// <see cref="Commands.AnimatorCommands.Run"/>, the one path a pressed button
/// takes too, so a binding needs no code of its own.
/// </summary>
public static class AnimatorBindings
{
    /// <summary>One action and the key that runs it; Ctrl and Shift must match exactly.</summary>
    public readonly record struct Binding(ToolAction Action, Key Key, bool Ctrl = false, bool Shift = false);

    public static readonly Binding[] All =
    {
        // Playback and files.
        new(new ToolAction(ToolActionKind.TogglePlay), Key.Space),
        new(new ToolAction(ToolActionKind.Save), Key.S, Ctrl: true),
        new(new ToolAction(ToolActionKind.Load), Key.O, Ctrl: true),
        new(new ToolAction(ToolActionKind.NewSkeleton), Key.N, Ctrl: true),

        // History.
        new(new ToolAction(ToolActionKind.Undo), Key.Z, Ctrl: true),
        new(new ToolAction(ToolActionKind.Redo), Key.Y, Ctrl: true),
        new(new ToolAction(ToolActionKind.Redo), Key.Z, Ctrl: true, Shift: true),

        // Pose and rig edits.
        new(new ToolAction(ToolActionKind.KeyBone), Key.K),
        new(new ToolAction(ToolActionKind.KeyPose), Key.K, Shift: true),
        new(new ToolAction(ToolActionKind.CopyPose), Key.C, Ctrl: true),
        new(new ToolAction(ToolActionKind.PastePose), Key.V, Ctrl: true),
        new(new ToolAction(ToolActionKind.CopyKeys), Key.C, Ctrl: true, Shift: true),
        new(new ToolAction(ToolActionKind.PasteKeys), Key.V, Ctrl: true, Shift: true),
        new(new ToolAction(ToolActionKind.SelectAllKeys), Key.A, Ctrl: true),
        new(new ToolAction(ToolActionKind.ResetTransform), Key.R, Shift: true),
        new(new ToolAction(ToolActionKind.MirrorPose), Key.M),
        new(new ToolAction(ToolActionKind.RotateLeft), Key.Q),
        new(new ToolAction(ToolActionKind.RotateRight), Key.E),
        new(new ToolAction(ToolActionKind.ScaleDown), Key.Bracketleft),
        new(new ToolAction(ToolActionKind.ScaleUp), Key.Bracketright),
        new(new ToolAction(ToolActionKind.ToggleMovable), Key.T),
        new(new ToolAction(ToolActionKind.AddBone), Key.B),
        new(new ToolAction(ToolActionKind.DeleteBone), Key.Delete, Shift: true),
        new(new ToolAction(ToolActionKind.RenameBone), Key.F2),

        // Preview framing.
        new(new ToolAction(ToolActionKind.FitPreview), Key.F),
        new(new ToolAction(ToolActionKind.ResetPreview), Key.F, Shift: true),

        // Editing modes.
        new(new ToolAction(ToolActionKind.ToggleMode, nameof(AnimatorMode.AutoKey)), Key.A),
        new(new ToolAction(ToolActionKind.ToggleMode, nameof(AnimatorMode.Onion)), Key.O),
        new(new ToolAction(ToolActionKind.ToggleMode, nameof(AnimatorMode.Rest)), Key.R),
        new(new ToolAction(ToolActionKind.ToggleMode, nameof(AnimatorMode.Svg)), Key.G),
        new(new ToolAction(ToolActionKind.ToggleMode, nameof(AnimatorMode.Ik)), Key.I),

        // Timeline keys.
        new(new ToolAction(ToolActionKind.DeleteKey), Key.Delete),

        // Clips.
        new(new ToolAction(ToolActionKind.NewClip), Key.N, Ctrl: true, Shift: true),
        new(new ToolAction(ToolActionKind.DuplicateClip), Key.D, Ctrl: true),
        new(new ToolAction(ToolActionKind.DeleteClip), Key.Delete, Ctrl: true, Shift: true),
        new(new ToolAction(ToolActionKind.DurationDown), Key.Minus, Ctrl: true),
        new(new ToolAction(ToolActionKind.DurationUp), Key.Equal, Ctrl: true),
        new(new ToolAction(ToolActionKind.ToggleLoop), Key.L),

        // Easing presets, in the order the timeline's buttons list them.
        new(new ToolAction(ToolActionKind.Interpolate, nameof(XafEasePreset.Step)), Key.Key1),
        new(new ToolAction(ToolActionKind.Interpolate, nameof(XafEasePreset.Linear)), Key.Key2),
        new(new ToolAction(ToolActionKind.Interpolate, nameof(XafEasePreset.Ease)), Key.Key3),
        new(new ToolAction(ToolActionKind.Interpolate, nameof(XafEasePreset.EaseIn)), Key.Key4),
        new(new ToolAction(ToolActionKind.Interpolate, nameof(XafEasePreset.EaseOut)), Key.Key5),
    };

    /// <summary>
    /// The binding a key chord runs, or null. Each chord must match its
    /// modifiers exactly, so plain O and Ctrl+O are different bindings.
    /// </summary>
    public static Binding? Find(Key key, bool ctrl, bool shift)
    {
        foreach (Binding binding in All)
        {
            if (binding.Key == key && binding.Ctrl == ctrl && binding.Shift == shift)
                return binding;
        }

        return null;
    }
}
