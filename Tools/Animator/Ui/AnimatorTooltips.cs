using Friflo.Engine.ECS;
using Writer.Ecs;

namespace Writer.Tools.Animator;

/// <summary>
/// One-line explanations for the animator's abbreviated buttons. Every button
/// carries the matching <see cref="UiTooltip"/> so shortening a label never
/// hides what it does or which key runs it.
/// </summary>
public static class AnimatorTooltips
{
    /// <summary>The hover text for an action, or null for pointer-only actions with no button.</summary>
    public static string? For(ToolActionKind kind, string argument = "") => kind switch
    {
        ToolActionKind.Play => "Play from the playhead (Space)",
        ToolActionKind.Stop => "Pause playback (Space)",
        ToolActionKind.TogglePlay => "Play or pause (Space)",
        ToolActionKind.Save => "Save the XAF package (Ctrl+S)",
        ToolActionKind.Load => "Load the XAF package (Ctrl+O)",
        ToolActionKind.NewSkeleton => "Start an empty skeleton (Ctrl+N)",
        ToolActionKind.Undo => "Undo the last edit (Ctrl+Z)",
        ToolActionKind.Redo => "Redo the last undone edit (Ctrl+Y)",
        ToolActionKind.RotateLeft => "Turn selected bones -0.25 rad (Q)",
        ToolActionKind.RotateRight => "Turn selected bones +0.25 rad (E)",
        ToolActionKind.ScaleDown => "Scale selected bones down by 0.1 ([)",
        ToolActionKind.ScaleUp => "Scale selected bones up by 0.1 (])",
        ToolActionKind.KeyBone => "Key selected bones at the playhead (K)",
        ToolActionKind.KeyPose => "Key every bone's pose (Shift+K)",
        ToolActionKind.CopyPose => "Copy the whole pose (Ctrl+C)",
        ToolActionKind.PastePose => "Paste the copied pose (Ctrl+V)",
        ToolActionKind.MirrorPose => "Mirror the pose left to right (M)",
        ToolActionKind.ToggleMovable => "Let the bone translate or lock its length (T)",
        ToolActionKind.AddBone => "Add a child bone to the selected bone (B)",
        ToolActionKind.DeleteBone => "Delete the selected bone and its keys (Shift+Delete)",
        ToolActionKind.RenameBone => "Rename this bone (F2)",
        ToolActionKind.ReparentBone => "Move this bone under another bone",
        ToolActionKind.AddSvg => "Attach an SVG group to this bone",
        ToolActionKind.AttachSvg => "Attach this SVG group to the bone",
        ToolActionKind.NewClip => "Add an empty clip (Ctrl+Shift+N)",
        ToolActionKind.DuplicateClip => "Duplicate the current clip (Ctrl+D)",
        ToolActionKind.DeleteClip => "Delete the current clip (Ctrl+Shift+Delete)",
        ToolActionKind.DurationDown => "Shorten the clip by 0.1 s (Ctrl+-)",
        ToolActionKind.DurationUp => "Lengthen the clip by 0.1 s (Ctrl+=)",
        ToolActionKind.ToggleLoop => "Loop the clip; drag the teal handles to set the range (L)",
        ToolActionKind.AddKey => "Insert a key at this time",
        ToolActionKind.DeleteKey => "Delete the selected keys (Delete or Backspace)",
        ToolActionKind.CopyKeys => "Copy selected keys across times or clips (Ctrl+Shift+C)",
        ToolActionKind.PasteKeys => "Paste copied keys at the playhead (Ctrl+Shift+V)",
        ToolActionKind.SequenceGame => "Preview jump, fall, land and idle playing into each other, as in the game (P)",
        ToolActionKind.SequenceAll => "Preview every clip in the file playing into the next (Shift+P)",
        ToolActionKind.SelectAllKeys => "Select every key in the clip (Ctrl+A)",
        ToolActionKind.FitPreview => "Frame the whole rig in the viewport (F)",
        ToolActionKind.ResetPreview => "Reset the viewport to 1:1 (Shift+F)",
        ToolActionKind.ResetTransform => "Clear the selected pose back to rest (Shift+R)",
        ToolActionKind.Clip => $"Edit clip {argument.ToUpperInvariant()}",
        ToolActionKind.SelectBone => "Select this bone",
        ToolActionKind.SelectAttachment => "Select this SVG attachment",
        ToolActionKind.Interpolate => argument switch
        {
            "Step" => "Hold the value until the next key (1)",
            "Linear" => "Move at a constant speed (2)",
            "Ease" => "Ease in and out of the next key (3)",
            "EaseIn" => "Start slow, end at full speed (4)",
            "EaseOut" => "Start at full speed, end slow (5)",
            _ => "Set how the selected keys interpolate",
        },
        ToolActionKind.ToggleMode => argument switch
        {
            nameof(AnimatorMode.AutoKey) => "Key every edit automatically (A)",
            nameof(AnimatorMode.Onion) => "Show poses just before and after (O)",
            nameof(AnimatorMode.Rest) => "Edit the rest pose instead of the animation (R)",
            nameof(AnimatorMode.Svg) => "Place SVG attachments instead of bones (G)",
            nameof(AnimatorMode.Pin) => "In the rest pose, moving a bone leaves the bones below it where they are (H)",
            nameof(AnimatorMode.Ik) => "Drag a joint as a two-bone IK chain (I)",
            _ => "Toggle this editing mode",
        },
        _ => null,
    };

    /// <summary>Adds the explanation to a button when the action has one.</summary>
    public static Entity Attach(Entity button, ToolActionKind kind, string argument = "")
    {
        if (button.IsNull || button.HasComponent<UiTooltip>())
            return button;

        string? text = For(kind, argument);
        if (!string.IsNullOrEmpty(text))
            button.AddComponent(new UiTooltip(text));
        return button;
    }
}
