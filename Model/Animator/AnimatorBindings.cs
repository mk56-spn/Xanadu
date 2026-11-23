using Xanadu.Core.Keys;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Model.Animator;

/// <summary>
/// The animator's own keybindings (<see cref="TimelineShortcuts"/>
/// holds those every timeline shares): the intents buttons send, plus the key
/// that sends the same one. Shortcuts emit them on the session, the one path
/// a pressed button takes too, so a binding needs no code of its own.
/// </summary>
public static class AnimatorBindings
{
    /// <summary>One intent and the key that runs it; Ctrl and Shift must match exactly.</summary>
    public readonly record struct Binding(IIntent Intent, Key Key, bool Ctrl = false, bool Shift = false);

    public static readonly Binding[] All =
    [
        // Playback and files.
        new(new PlayGameSequence(), Key.P),
        new(new PlayAllClips(), Key.P, Shift: true),
        new(new CycleAttacks(), Key.A, Shift: true),
        new(new PreviewTransition(), Key.P, Ctrl: true),
        new(new ToggleFlowLoop(), Key.P, Ctrl: true, Shift: true),
        new(new ToggleSlow(), Key.Comma),
        new(new TogglePreviewMode(), Key.Tab),
        new(new TogglePreviewMode(), Key.F5),
        new(new ExitPreviewMode(), Key.Escape),
        new(new OpenInfo(), Key.I, Ctrl: true),
        new(new ToggleShortcuts(), Key.F1),
        new(new ToggleShortcuts(), Key.Slash, Shift: true),
        new(new CloseEditor(), Key.Q, Ctrl: true),
        new(new StepTransitionFade(-1), Key.Minus, Shift: true),
        new(new StepTransitionFade(1), Key.Equal, Shift: true),
        new(new Save(), Key.S, Ctrl: true),
        new(new Load(), Key.O, Ctrl: true),
        new(new NewSkeleton(), Key.N, Ctrl: true),

        // History.
        new(new Undo(), Key.Z, Ctrl: true),
        new(new Redo(), Key.Y, Ctrl: true),
        new(new Redo(), Key.Z, Ctrl: true, Shift: true),

        // Pose and rig edits.
        new(new KeyBone(), Key.K),
        new(new PlantBone(), Key.Y),
        new(new KeyPose(), Key.K, Shift: true),
        new(new CopyPose(), Key.C, Ctrl: true),
        new(new PastePose(), Key.V, Ctrl: true),
        new(new CopyKeys(), Key.C, Ctrl: true, Shift: true),
        new(new PasteKeys(), Key.V, Ctrl: true, Shift: true),
        new(new SelectAllKeys(), Key.A, Ctrl: true),
        new(new ResetTransform(), Key.R, Shift: true),
        new(new MirrorPose(), Key.M),
        new(new TurnSelected(-1), Key.Q),
        new(new TurnSelected(1), Key.E),
        new(new ScaleSelected(-1), Key.Bracketleft),
        new(new ScaleSelected(1), Key.Bracketright),
        new(new ToggleMovable(), Key.T),
        new(new LockBend(), Key.J, Shift: true),
        new(new ToggleLimit(), Key.L, Shift: true),
        new(new AutoWeights(), Key.W, Ctrl: true, Shift: true),
        new(new PaintWeights(), Key.W, Ctrl: true),
        new(new StepBrushRadius(-1), Key.Minus, Ctrl: true, Shift: true),
        new(new StepBrushRadius(1), Key.Equal, Ctrl: true, Shift: true),
        new(new StepBrushStrength(-1), Key.Key9, Ctrl: true, Shift: true),
        new(new StepBrushStrength(1), Key.Key0, Ctrl: true, Shift: true),
        new(new StepDepth(-1), Key.Comma, Ctrl: true, Shift: true),
        new(new StepDepth(1), Key.Period, Ctrl: true, Shift: true),
        new(new StepLimit(false, -1), Key.Semicolon, Ctrl: true),
        new(new StepLimit(false, 1), Key.Apostrophe, Ctrl: true),
        new(new StepLimit(true, -1), Key.Semicolon, Ctrl: true, Shift: true),
        new(new StepLimit(true, 1), Key.Apostrophe, Ctrl: true, Shift: true),
        new(new AddEvent(), Key.E, Shift: true),
        new(new DeleteEvent(), Key.E, Ctrl: true, Shift: true),
        new(new DropArt(), Key.A, Ctrl: true, Shift: true),
        new(new StepArt(-1), Key.Bracketleft, Shift: true),
        new(new StepArt(1), Key.Bracketright, Shift: true),
        new(new StepLayerKey(-1), Key.Comma, Shift: true),
        new(new StepLayerKey(1), Key.Period, Shift: true),
        new(new MoveLayer(0, -1), Key.Bracketleft, Ctrl: true, Shift: true),
        new(new MoveLayer(0, 1), Key.Bracketright, Ctrl: true, Shift: true),
        new(new ToggleArtVisible(), Key.U, Shift: true),
        new(new ToggleMesh(), Key.G, Shift: true),
        new(new StepMeshDetail(-1), Key.Bracketleft, Ctrl: true),
        new(new StepMeshDetail(1), Key.Bracketright, Ctrl: true),
        new(new StepMeshBlend(-1), Key.Comma, Ctrl: true),
        new(new StepMeshBlend(1), Key.Period, Ctrl: true),
        new(new AddBone(), Key.B),
        new(new DeleteBone(), Key.Delete, Shift: true),
        new(new RenameBone(), Key.F2),
        new(new AddChain(), Key.V),
        new(new DeleteChain(), Key.Delete, Ctrl: true),
        new(new ToggleChainSheet(), Key.H, Ctrl: true),
        new(new AddCloth(), Key.C),
        new(new DeleteCloth(), Key.C, Shift: true),
        new(new AddEffect(), Key.W),
        new(new DeleteEffect(), Key.W, Shift: true),
        new(new ToggleEffectVisible(), Key.U),
        new(new StepEffectOpacity(-1), Key.Key9),
        new(new StepEffectOpacity(1), Key.Key0),

        // Preview framing.
        new(new FitPreview(), Key.F),
        new(new ResetPreview(), Key.F, Shift: true),

        // Editing modes.
        new(new ToggleMode(AnimatorMode.Onion), Key.O),
        new(new ToggleMode(AnimatorMode.Parts), Key.G),
        new(new ToggleMode(AnimatorMode.Ik), Key.I),
        new(new ToggleMode(AnimatorMode.Pin), Key.H),
        new(new ToggleMode(AnimatorMode.Kites), Key.X),
        new(new ToggleMode(AnimatorMode.Handles), Key.J),
        new(new ToggleMode(AnimatorMode.MotionPaths), Key.N),
        new(new TogglePathWindow(), Key.N, Shift: true),

        // Timeline keys.
        new(new DeleteKey(), Key.Delete),
        new(new DeleteKey(), Key.Backspace),

        // Clips.
        new(new OpenNewClip(Copy: false), Key.N, Ctrl: true, Shift: true),
        new(new OpenClipLength(), Key.L, Ctrl: true),
        new(new DuplicateClip(), Key.D, Ctrl: true),
        new(new DeleteClip(), Key.Delete, Ctrl: true, Shift: true),
        new(new StepClipLength(-1), Key.Minus, Ctrl: true),
        new(new StepClipLength(1), Key.Equal, Ctrl: true),
        new(new ToggleLoop(), Key.L),

        // Easing presets, in the order the timeline's buttons list them.
        new(new ToggleEasingWindow(), Key.E, Ctrl: true),
        new(new SetEase(EasePreset.Step), Key.Key1),
        new(new SetEase(EasePreset.Linear), Key.Key2),
        new(new SetEase(EasePreset.Ease), Key.Key3),
        new(new SetEase(EasePreset.EaseIn), Key.Key4),
        new(new SetEase(EasePreset.EaseOut), Key.Key5),
    ];

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
