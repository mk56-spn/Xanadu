using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator;

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
        ToolActionKind.TogglePathWindow => "Motion paths for the whole clip, or one second around the playhead (Shift+N)",
        ToolActionKind.OpenInfo => "The file's path and what it holds (Ctrl+I)",
        ToolActionKind.CloseInfo => "Close the file info",
        ToolActionKind.TogglePreviewMode => "Hide the editing panels and watch the clip (Tab)",
        ToolActionKind.ExitPreviewMode => "Back to editing (Esc)",
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
        ToolActionKind.AddChain => "Hang a Verlet chain off the selected bone, for hair or a tail (V)",
        ToolActionKind.PickChainArt => "Choose the SVG group drawn on every segment of the chain",
        ToolActionKind.PickChainBone => "Then click the bone the chain hangs from; the chain stays where it is",
        ToolActionKind.PickColliderBone => "Then click the bone the collider follows; the collider stays where it is",
        ToolActionKind.SelectChain => "Select this Verlet chain",
        ToolActionKind.AddCloth =>
            "Add a dress hanging from the selected bone, its bodice pinned down to the bone's joint (C)",
        ToolActionKind.SelectCloth => "Select this cloth",
        ToolActionKind.DeleteCloth => "Delete the selected cloth (Shift+C)",
        ToolActionKind.PickClothBone => "Then click the bone the cloth is laid out from",
        ToolActionKind.PinClothPoints =>
            "Then click points of the cloth to pin each to the nearest bone, or free it; click again here to stop",
        ToolActionKind.PickClothColliders =>
            "Then click bones the cloth's edges cannot pass through, or click one again to drop it; click here to stop",
        ToolActionKind.AddEffect => "Hang a speed streak off the selected bone, shown for 0.15 s from the playhead (W)",
        ToolActionKind.SelectEffect => "Select this effect; key it like a bone, show or hide it with U",
        ToolActionKind.DeleteEffect => "Delete the selected effect and its keys (Shift+W)",
        ToolActionKind.ToggleEffectVisible => "Key the selected effect shown, or hidden if it is showing, at the playhead (U)",
        ToolActionKind.EffectOpacityDown => "Key the selected effect 25% more see-through at the playhead (9)",
        ToolActionKind.EffectOpacityUp => "Key the selected effect 25% more opaque at the playhead (0)",
        ToolActionKind.DeleteChain => "Delete the selected chain (Ctrl+Delete)",
        ToolActionKind.OpenNewClip => "Open the new clip dialog (Ctrl+Shift+N)",
        ToolActionKind.CreateClip => "Create the clip with this name and length",
        ToolActionKind.CloseNewClip => "Close without creating a clip",
        ToolActionKind.OpenClipLength => "Type the clip's length or step it with the arrows (Ctrl+L)",
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
        ToolActionKind.AllowTransition => $"Allow or forbid this clip fading into {argument.ToUpperInvariant()}",
        ToolActionKind.SelectTransition => $"Edit and preview the fade into {argument.ToUpperInvariant()}",
        ToolActionKind.TransitionFadeDown => "Shorten the fade by 0.05 s (Shift+-)",
        ToolActionKind.TransitionFadeUp => "Lengthen the fade by 0.05 s (Shift+=)",
        ToolActionKind.TransitionCurve => argument switch
        {
            "Linear" => "Blend at a constant rate",
            "Smooth" => "Blend slowly at both ends, so neither clip is left or reached with a jolt",
            "EaseIn" => "Hold the old clip longer, then hurry into the new one",
            "EaseOut" => "Leave the old clip at once, then settle slowly into the new one",
            _ => "Set how the fade blends",
        },
        ToolActionKind.PreviewTransition => "Play this clip into the selected one with its fade (Ctrl+P)",
        ToolActionKind.ToggleFlowLoop => "Repeat the transition preview until stopped (Ctrl+Shift+P)",
        ToolActionKind.ToggleSlow => "Play previews at quarter speed to watch a fade closely (,)",
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
            nameof(AnimatorMode.Svg) => "Place SVG attachments instead of bones; their squares only show in this mode (G)",
            nameof(AnimatorMode.Pin) => "In the rest pose, moving a bone leaves the bones below it where they are (H)",
            nameof(AnimatorMode.Kites) => "Show each bone as a filled kite; off leaves only joints and labels (X)",
            nameof(AnimatorMode.Handles) => "Show joint dots and bone labels; off leaves just the art (J)",
            nameof(AnimatorMode.Ik) => "Drag a joint as a two-bone IK chain (I)",
            nameof(AnimatorMode.MotionPaths) =>
                "Show where joints travel: the selected bones, the extremities in preview; red marks sharp turns (N, Shift+N for around the playhead only)",
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
