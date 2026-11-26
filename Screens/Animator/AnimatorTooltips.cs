using Friflo.Engine.ECS;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>
/// One-line explanations for the animator's abbreviated buttons. Every button
/// carries the matching <see cref="UiTooltip"/> so shortening a label never
/// hides what it does or which key runs it.
/// </summary>
public static class AnimatorTooltips
{
    /// <summary>The hover text for an intent, or null for pointer-only intents with no button.</summary>
    public static string? For(IIntent intent) => intent switch
    {
        StartPlaying => "Play from the playhead (Space)",
        StopPlaying => "Stop playback (Space); when stopped, return to the start",
        TogglePlay => "Play or pause (Space)",
        Save => "Save the rig (Ctrl+S)",
        Load => "Load a rig (Ctrl+O)",
        NewSkeleton => "Start an empty skeleton (Ctrl+N)",
        Undo => "Undo the last edit (Ctrl+Z)",
        TogglePathWindow => "Motion paths for the whole clip, or one second around the playhead (Shift+N)",
        OpenInfo => "The file's path and what it holds (Ctrl+I)",
        CloseInfo => "Close the file info",
        CloseEditor => "Quit the animator (Ctrl+Q); with unsaved changes, press again to discard them",
        TogglePreviewMode => "Hide the editing panels and watch the clip (Tab)",
        SetWorkspace w => w.Workspace switch
        {
            AnimatorWorkspace.Rig => "Build the rig: bones, parts and the rest pose every clip starts from. Changes here move every clip",
            AnimatorWorkspace.Preview => "Hide the editing panels and watch the clip (Tab)",
            _ => "Animate the clip: pose and key it",
        },
        ExitPreviewMode => "Back to editing (Esc)",
        Redo => "Redo the last undone edit (Ctrl+Y)",
        TurnSelected { Steps: < 0 } => "Turn selected bones -0.25 rad (Q)",
        TurnSelected { Steps: > 0 } => "Turn selected bones +0.25 rad (E)",
        ScaleSelected { Steps: < 0 } => "Scale selected bones down by 0.1 ([)",
        ScaleSelected { Steps: > 0 } => "Scale selected bones up by 0.1 (])",
        AddOrbit => AnimatorLayout.TargetTooltip(AnimatorTargetVerb.Orbit),
        StepTargetAnchor => AnimatorLayout.TargetTooltip(AnimatorTargetVerb.Anchor),
        DetachTarget => AnimatorLayout.TargetTooltip(AnimatorTargetVerb.Detach),
        SetTargetFrom => AnimatorLayout.TargetTooltip(AnimatorTargetVerb.From),
        SetTargetTo => AnimatorLayout.TargetTooltip(AnimatorTargetVerb.To),
        StepTargetEase => AnimatorLayout.TargetTooltip(AnimatorTargetVerb.Ease),
        FlipTarget => AnimatorLayout.TargetTooltip(AnimatorTargetVerb.Flip),
        PlantBone => AnimatorLayout.TargetTooltip(AnimatorTargetVerb.Pin),
        KeyBone => "Key selected bones at the playhead (K)",
        KeyPose => "Key every bone's pose (Shift+K)",
        CopyPose => "Copy the whole pose (Ctrl+C)",
        PastePose => "Paste the copied pose (Ctrl+V)",
        MirrorPose => "Mirror the pose left to right (M)",
        EditXif => "Draw the selected part: opens the XIF editor over everything on its art (the alternate it shows at the playhead, if any), or on the selected chain's art. Esc or DONE closes it",
        AddArt => "Give the selected part more art it can switch to, such as a fist for an open hand: starts as a copy of its own",
        DropArt => "Remove the alternate art the selected part shows at the playhead; keys on it go back to its own art (Ctrl+Shift+A)",
        StepArt { By: < 0 } => "Key the selected part's previous art at the playhead; it switches exactly there (Shift+[)",
        StepArt { By: > 0 } => "Key the selected part's next art at the playhead; it switches exactly there (Shift+])",
        StepLayerKey { By: < 0 } => "Key the selected part one layer further back from the playhead on, to pass behind the body (Shift+,)",
        StepLayerKey { By: > 0 } => "Key the selected part one layer further forward from the playhead on (Shift+.)",
        ToggleArtVisible => "Key the selected part hidden from the playhead on, or shown again (Shift+U)",
        ToggleMesh =>
            "Draw the selected part as a mesh that bends with its bone and the ones below, so joints have no hard seam; again for rigid (Shift+G)",
        PickMeshBones => "Then click bones to let them bend the selected mesh, or stop them; click nothing to finish",
        StepMeshDetail { By: < 0 } => "Fewer mesh cells: cheaper, bends more coarsely (Ctrl+[)",
        StepMeshDetail { By: > 0 } => "More mesh cells: smoother bends (Ctrl+])",
        StepMeshBlend { By: < 0 } => "A narrower zone where two bones share the mesh: a tighter crease (Ctrl+,)",
        StepMeshBlend { By: > 0 } => "A wider zone where two bones share the mesh: a softer bend (Ctrl+.)",
        AddEvent =>
            "Mark the playhead's moment in this clip with a named event the game is told about, such as the frame a kick lands (Shift+E)",
        DeleteEvent => "Delete the selected event (Ctrl+Shift+E)",
        LockBend =>
            "Lock the selected knee or elbow to bend only the way it bends now, or free it again (Shift+J)",
        StepDepth { By: < 0 } =>
            "Move the selected joints away from you, out of the screen's plane: the bones either side of each foreshorten (Ctrl+Shift+,)",
        StepDepth { By: > 0 } =>
            "Bring the selected joints toward you, out of the screen's plane: the bones either side of each foreshorten (Ctrl+Shift+.)",
        AutoWeights =>
            "Trace the selected part's outline into a skin mesh and weight it to the bones that bend it, by heat diffusion; replaces painted weights (Ctrl+Shift+W)",
        PaintWeights =>
            "Paint the selected skin's weights: click a joint to choose its bone, drag to add its weight, Ctrl-drag to take it away (Ctrl+W)",
        StepBrushRadius { By: < 0 } => "A smaller weight brush (Ctrl+Shift+-)",
        StepBrushRadius { By: > 0 } => "A larger weight brush (Ctrl+Shift+=)",
        StepBrushStrength { By: < 0 } => "Paint weight on more gently (Ctrl+Shift+9)",
        StepBrushStrength { By: > 0 } => "Paint weight on harder (Ctrl+Shift+0)",
        ToggleLimit =>
            "Keep the selected bones from turning more than 45 degrees either way from rest while you pose, or free them (Shift+L). Clips still play as keyed",
        StepLimit { Max: false, By: < 0 } => "Let the selected bones turn further the negative way (Ctrl+;)",
        StepLimit { Max: false, By: > 0 } => "Let the selected bones turn less far the negative way (Ctrl+')",
        StepLimit { Max: true, By: < 0 } => "Let the selected bones turn less far the positive way (Ctrl+Shift+;)",
        StepLimit { Max: true, By: > 0 } => "Let the selected bones turn further the positive way (Ctrl+Shift+')",
        ToggleMovable => "Let the bone translate or lock its length (T). Ctrl+drag any joint to offset it off its rest spot, which turns this on for you",
        AddBone => "Add a child bone to the selected bone (B)",
        DeleteBone => "Delete the selected bone and its keys (Shift+Delete)",
        RenameBone => "Rename this bone (F2)",
        ReparentBone => "Move this bone under another bone",
        AddPart => "Add a part to this bone, then draw it in the art editor",
        AddChain => "Hang a Verlet chain off the selected bone, for hair or a tail (V)",
        PickChainBone { Pick: AnimatorChainPick.Anchor } => "Then click the bone the chain hangs from; the chain stays where it is",
        PickChainBone { Pick: AnimatorChainPick.Collider } => "Then click the bone the collider follows; the collider stays where it is",
        SelectChain => "Select this Verlet chain",
        AddCloth =>
            "Add a dress hanging from the selected bone: its bodice pinned down to a waist just above the bone's joint, its hem just above the feet (C)",
        SelectCloth => "Select this cloth",
        DeleteCloth => "Delete the selected cloth (Shift+C)",
        PickCloth { Pick: AnimatorClothPick.Bone } => "Then click the bone the cloth is laid out from",
        PickCloth { Pick: AnimatorClothPick.Pins } =>
            "Then click points of the cloth: a free point is pinned to the nearest bone, a pinned one tethered to it instead (free to swing within REACH), a tethered one freed; click again here to stop",
        PickCloth { Pick: AnimatorClothPick.Colliders } =>
            "Then click bones the cloth's edges cannot pass through, or click one again to drop it; click here to stop",
        AddEffect => "Hang a speed streak off the selected bone, shown for 0.15 s from the playhead (W)",
        AddParticles =>
            "Hang a particle stream off the selected bone, flowing for 0.5 s from the playhead; key it on and off like a streak",
        DeleteEffect => "Delete the selected effect and its keys (Shift+W)",
        ToggleEffectVisible => "Key the selected effect shown, or hidden if it is showing, at the playhead (U)",
        StepEffectOpacity { By: < 0 } => "Key the selected effect 25% more see-through at the playhead (9)",
        StepEffectOpacity { By: > 0 } => "Key the selected effect 25% more opaque at the playhead (0)",
        DeleteChain => "Delete the selected chain (Ctrl+Delete)",
        ToggleChainSheet =>
            "Draw the chain as one sheet bent along it, its art stretched root to tip, such as a strand of hair; or as a piece per link again (Ctrl+H)",
        OpenNewClip => "Open the new clip dialog (Ctrl+Shift+N)",
        CreateClip => "Create the clip with this name and length",
        CloseNewClip => "Close without creating a clip",
        OpenClipLength => "Type the clip's length or step it with the arrows (Ctrl+L)",
        DuplicateClip => "Duplicate the current clip (Ctrl+D)",
        DeleteClip => "Delete the current clip (Ctrl+Shift+Delete)",
        TryoutEase t => $"Ease the move into this key: {EaseNames.Describe(t.Ease)}",
        TryoutReplay => "Restart the looping move into this key from the previous key",
        TryoutClose => "Hide this bar",
        ToggleShortcuts => "Show every shortcut (F1 or ?)",
        ClearClipKeys => "Remove every key, so the clip plays the rest pose again; undo brings them back",
        StepClipLength { Direction: < 0 } => "Shorten the clip by 0.1 s (Ctrl+-)",
        StepClipLength { Direction: > 0 } => "Lengthen the clip by 0.1 s (Ctrl+=)",
        ToggleLoop => "Loop the clip; drag the teal handles to set the range (L)",
        AddKey => "Insert a key at this time",
        DeleteKey => "Delete the selected keys (Delete or Backspace)",
        CopyKeys => "Copy selected keys across times or clips (Ctrl+Shift+C)",
        PasteKeys => "Paste copied keys at the playhead (Ctrl+Shift+V)",
        PlayGameSequence => "Preview jump, fall, land and idle playing into each other, as in the game (P)",
        PlayAllClips => "Preview every clip in the file playing into the next (Shift+P)",
        StepMetronome { By: < 0 } => "Slow the attack metronome by 5 BPM",
        StepMetronome { By: > 0 } => "Speed the attack metronome up by 5 BPM",
        StepAttackEvery { By: < 0 } => "Fire an attack on more of the beats",
        StepAttackEvery { By: > 0 } => "Fire an attack on fewer of the beats, leaving room for the follow-through",
        CycleAttacks => "Fire the attacks in turn on a metronome click, each from its contact frame over idle or the fall, as a note hit plays it in the game (Shift+A)",
        AllowTransition a => $"Allow or forbid this clip fading into {a.To.ToUpperInvariant()}",
        SelectTransition t => $"Edit and preview the fade into {t.To.ToUpperInvariant()}",
        StepTransitionFade { By: < 0 } => "Shorten the fade by 0.05 s (Shift+-)",
        StepTransitionFade { By: > 0 } => "Lengthen the fade by 0.05 s (Shift+=)",
        SetTransitionCurve c => c.Curve switch
        {
            RigFadeCurve.Linear => "Blend at a constant rate",
            RigFadeCurve.Smooth => "Blend slowly at both ends, so neither clip is left or reached with a jolt",
            RigFadeCurve.EaseIn => "Hold the old clip longer, then hurry into the new one",
            RigFadeCurve.EaseOut => "Leave the old clip at once, then settle slowly into the new one",
            _ => "Set how the fade blends",
        },
        PreviewTransition => "Play this clip into the selected one with its fade (Ctrl+P)",
        ToggleFlowLoop => "Repeat the transition preview until stopped (Ctrl+Shift+P)",
        ToggleSlow => "Play previews at quarter speed to watch a fade closely (,)",
        SelectAllKeys => "Select every key in the clip (Ctrl+A)",
        RemoveOverride { KeepKeys: false } => "Remove this override and the keys it made; the limb goes back to its own keys",
        RemoveOverride { KeepKeys: true } => "Remove this override but keep the keys it made, as ordinary keys",
        FitPreview => "Frame the selection, or the whole rig with nothing selected (F)",
        ResetPreview => "Reset the viewport to 1:1 (Shift+F)",
        ResetTransform => "Clear the selected pose back to rest (Shift+R)",
        PlayClip c => $"Edit clip {c.Clip.ToUpperInvariant()}",
        SelectBone => "Select this bone",
        SelectPart => "Select this part",
        MoveLayer { By: > 0 } => "Draw this art in front of the one above it (Ctrl+Shift+])",
        MoveLayer { By: < 0 } => "Draw this art behind the one below it (Ctrl+Shift+[)",
        SetEase e => $"{EaseNames.Label(e.Ease)}: {EaseNames.Describe(e.Ease)}{AnimatorEasePicker.Shortcut(e.Ease)}",
        ToggleEasingWindow => "Choose how the selected keys ease into the next ones, from every curve (Ctrl+E)",
        ToggleMode m => m.Mode switch
        {
            AnimatorMode.AutoKey => "Key every pose edit at the playhead; off, edits only preview until you key them (K, Shift+K)",
            AnimatorMode.Onion => "Show poses just before and after (O)",
            AnimatorMode.Rest => "Edit the rig itself: the rest pose every clip starts from. Changes here move every clip",
            AnimatorMode.Parts => "Place parts instead of bones; their squares only show in this mode (G)",
            AnimatorMode.Pin => "In the rest pose, moving a bone leaves the bones below it where they are (H)",
            AnimatorMode.Kites => "Show each bone as a filled kite; off leaves only joints and labels (X)",
            AnimatorMode.Handles => "Show joint dots and bone labels; off leaves just the art (J)",
            AnimatorMode.Ik => "Drag a joint as a two-bone IK chain (I)",
            AnimatorMode.Align =>
                "Line dragged joints up with the other joints; hold Shift while dragging to stay free",
            AnimatorMode.FullKeys =>
                "Keyframe rows: SUMMARY shows what has keys; FULL shows everything the selection can key (its fields, pins and ellipses, and its parts), unkeyed fields dimmed",
            AnimatorMode.MotionPaths =>
                "Show where joints travel: the selected bones, the extremities in preview; red marks sharp turns (N, Shift+N for around the playhead only)",
            _ => "Toggle this editing mode",
        },
        _ => null,
    };

    /// <summary>Adds the explanation to a button when the intent has one.</summary>
    public static Entity Attach(Entity button, IIntent intent)
    {
        if (button.IsNull || button.HasComponent<UiTooltip>())
            return button;

        string? text = For(intent);
        if (!string.IsNullOrEmpty(text))
            button.AddComponent(new UiTooltip(text));
        return button;
    }
}
