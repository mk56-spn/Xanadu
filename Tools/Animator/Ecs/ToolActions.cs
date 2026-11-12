using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator;

public enum ToolActionKind
{
    Play,
    Stop,
    Save,
    Load,
    Clip,
    RotateLeft,
    RotateRight,
    ScaleUp,
    ScaleDown,
    KeyBone,
    Undo,
    Redo,
    Interpolate,

    /// <summary>Opens or closes the EASING window, where every curve can be picked; no argument.</summary>
    ToggleEasingWindow,
    /// <summary>Duplicates the current clip directly; the NEW CLIP dialog can also copy it.</summary>
    DuplicateClip,
    DeleteClip,
    DurationDown,
    DurationUp,
    ToggleLoop,

    /// <summary>Opens the clip length field and step buttons in the CONFIG menu.</summary>
    OpenClipLength,

    /// <summary>Opens the NEW CLIP dialog; the argument "duplicate" starts it copying the current clip.</summary>
    OpenNewClip,

    /// <summary>Creates the clip described by the NEW CLIP dialog.</summary>
    CreateClip,

    /// <summary>Closes the NEW CLIP dialog without creating a clip.</summary>
    CloseNewClip,
    SelectBone,
    SelectAttachment,
    ToggleMovable,
    LockBend,

    /// <summary>Traces the selected SVG into a skin mesh and weights it to its bones by heat diffusion.</summary>
    AutoWeights,

    /// <summary>Starts or stops painting the selected skin's weights in the preview.</summary>
    PaintWeights,
    BrushSmaller,
    BrushLarger,
    BrushWeaker,
    BrushStronger,

    /// <summary>Moves the selected joints away from the viewer, out of the screen's plane.</summary>
    DepthAway,

    /// <summary>Moves the selected joints toward the viewer.</summary>
    DepthToward,

    /// <summary>Limits the selected bones' turn from rest, or frees them.</summary>
    ToggleLimit,
    LimitMinDown,
    LimitMinUp,
    LimitMaxDown,
    LimitMaxUp,
    AddEvent,
    DeleteEvent,
    AddArt,
    DropArt,
    PreviousArt,
    NextArt,
    LayerDownKey,
    LayerUpKey,
    MoveLayerForward,
    MoveLayerBack,
    ToggleArtVisible,

    /// <summary>Asks the system to open the SVG file the selected attachment is showing.</summary>
    OpenArtFile,
    ToggleMesh,
    PickMeshBones,
    MeshDetailDown,
    MeshDetailUp,
    MeshBlendDown,
    MeshBlendUp,
    AddBone,
    DeleteBone,
    NewSkeleton,
    RenameBone,
    ReparentBone,
    KeyPose,
    CopyPose,
    PastePose,
    MirrorPose,
    AddKey,
    DeleteKey,

    /// <summary>Opens the ADD SVG picker on the selected bone; no argument.</summary>
    AddSvg,

    /// <summary>Attaches one picker choice, by its index in the open picker.</summary>
    AttachSvg,

    /// <summary>Plays if stopped, stops if playing, as the PLAY and STOP buttons do.</summary>
    TogglePlay,

    /// <summary>Flips one editing mode; the argument names the <see cref="AnimatorMode"/>.</summary>
    ToggleMode,

    /// <summary>Frames the whole rig in the preview.</summary>
    FitPreview,

    /// <summary>Puts the preview back at one unit per pixel, centred.</summary>
    ResetPreview,

    /// <summary>Clears the selected bone's or attachment's pose back to its rest transform.</summary>
    ResetTransform,

    /// <summary>Copies the selected key sets, for pasting at another time or in another clip.</summary>
    CopyKeys,

    /// <summary>Pastes the copied key sets at the playhead.</summary>
    PasteKeys,

    /// <summary>Selects every key set in the current clip.</summary>
    SelectAllKeys,

    /// <summary>Removes the right-clicked override and the keys it baked.</summary>
    RemoveOverride,

    /// <summary>Removes the right-clicked override, keeping its keys as ordinary keys.</summary>
    DetachOverride,

    /// <summary>Hangs a new Verlet chain off the selected bone; no argument.</summary>
    AddChain,

    /// <summary>Selects a chain, by its index in the package.</summary>
    SelectChain,

    /// <summary>Deletes the selected chain; no argument.</summary>
    DeleteChain,

    /// <summary>Draws the selected chain as one sheet along it, or as a piece per segment again.</summary>
    ToggleChainSheet,

    /// <summary>Opens the SVG picker to choose the art of the selected chain; no argument.</summary>
    PickChainArt,

    /// <summary>The next bone clicked becomes the one the selected chain hangs from; no argument.</summary>
    PickChainBone,

    /// <summary>The next bone clicked becomes the one the selected chain's collider follows; no argument.</summary>
    PickColliderBone,

    /// <summary>Adds a dress hanging from the selected bone; no argument.</summary>
    AddCloth,

    /// <summary>Selects a cloth, by its index in the package.</summary>
    SelectCloth,

    /// <summary>Deletes the selected cloth; no argument.</summary>
    DeleteCloth,

    /// <summary>The next bone clicked becomes the one the selected cloth is laid out from; no argument.</summary>
    PickClothBone,

    /// <summary>Starts or stops pinning: each point clicked is pinned to the nearest bone, or freed.</summary>
    PinClothPoints,

    /// <summary>Starts or stops choosing colliders: each bone clicked starts or stops being one.</summary>
    PickClothColliders,

    /// <summary>Plays jump, fall, land and idle into each other, as the game does.</summary>
    SequenceGame,

    /// <summary>Plays every clip in the file into the next.</summary>
    SequenceAll,

    /// <summary>Starts or stops cycling the attack clips, each layered over the clip the game plays under it.</summary>
    CycleAttacks,

    /// <summary>Slows the attack preview's metronome by one step.</summary>
    MetronomeSlower,

    /// <summary>Speeds the attack preview's metronome up by one step.</summary>
    MetronomeFaster,

    /// <summary>Fires an attack on more of the metronome's beats.</summary>
    AttackMoreOften,

    /// <summary>Fires an attack on fewer of the metronome's beats.</summary>
    AttackLessOften,

    /// <summary>Allows or forbids the current clip fading into the clip the argument names, and selects it.</summary>
    AllowTransition,

    /// <summary>Selects the hand-over from the current clip into the clip the argument names, for the FLOW tab to edit.</summary>
    SelectTransition,

    /// <summary>Shortens the selected transition's fade by one step.</summary>
    TransitionFadeDown,

    /// <summary>Lengthens the selected transition's fade by one step.</summary>
    TransitionFadeUp,

    /// <summary>Sets the selected transition's fade curve; the argument names the <see cref="XafFadeCurve"/>.</summary>
    TransitionCurve,

    /// <summary>Plays the current clip into the selected one with its fade, as the game would.</summary>
    PreviewTransition,

    /// <summary>Makes the transition preview repeat until stopped.</summary>
    ToggleFlowLoop,

    /// <summary>Plays every preview at a quarter of its speed, to watch a fade closely.</summary>
    ToggleSlow,

    /// <summary>Hangs a speed streak off the selected bone, shown from the playhead; no argument.</summary>
    AddEffect,

    /// <summary>Hangs a particle stream off the selected bone, flowing from the playhead; no argument.</summary>
    AddParticles,

    /// <summary>Selects an effect, by its index in the package.</summary>
    SelectEffect,

    /// <summary>Deletes the selected effect and its keys; no argument.</summary>
    DeleteEffect,

    /// <summary>Keys the selected effect shown, or hidden if it is showing, at the playhead.</summary>
    ToggleEffectVisible,

    /// <summary>Keys the selected effect's opacity one step lower at the playhead.</summary>
    EffectOpacityDown,

    /// <summary>Keys the selected effect's opacity one step higher at the playhead.</summary>
    EffectOpacityUp,

    /// <summary>A limb target command; the argument is its verb, see <see cref="Commands.AnimatorTargetCommands"/>.</summary>
    LimbTarget,

    /// <summary>Holds the selected foot or hand at its spot on the rig, or releases and keys it.</summary>
    PlantBone,

    /// <summary>Enters preview mode, or leaves it: the editing panels give way to the viewport.</summary>
    TogglePreviewMode,

    /// <summary>Leaves preview mode; does nothing while editing.</summary>
    ExitPreviewMode,

    /// <summary>Opens the file info popup: the file's path and what it holds.</summary>
    OpenInfo,

    /// <summary>Closes the file info popup.</summary>
    CloseInfo,

    /// <summary>Quits the animator; with unsaved changes the first press only warns.</summary>
    CloseEditor,

    /// <summary>Shows motion paths for the whole clip, or only around the playhead.</summary>
    TogglePathWindow,
}

/// <summary>
/// An animator command, run by keys, menus and buttons. On a pressable entity it
/// rides as a <see cref="UiCommand"/> (see <see cref="Of"/>), its argument as the
/// command's <see cref="UiCommand.Text"/>; <see cref="ToolActions"/> reads it back.
/// </summary>
public struct ToolAction(ToolActionKind kind, string argument = "")
{
    public ToolActionKind Kind = kind;
    public string Argument = argument;

    /// <summary>The command a button carries to run this action when pressed.</summary>
    public static UiCommand Of(ToolActionKind kind, string argument = "") => UiCommand.Of(kind, text: argument);

    /// <summary>The action a command carries, if it is an animator one.</summary>
    public static bool From(in UiCommand command, out ToolAction action)
    {
        if (!command.Is(out ToolActionKind kind))
        {
            action = default;
            return false;
        }

        action = new ToolAction(kind, command.Text ?? "");
        return true;
    }
}

/// <summary>Reads <see cref="ToolAction"/>s off entities, where they ride as <see cref="UiCommand"/>s.</summary>
public static class ToolActions
{
    public static bool TryGetToolAction(this Entity entity, out ToolAction action)
    {
        action = default;
        return entity.TryGetComponent(out UiCommand command) && ToolAction.From(command, out action);
    }

    public static bool HasToolAction(this Entity entity) => entity.TryGetToolAction(out _);

    /// <summary>The entity's action; throws when it has none.</summary>
    public static ToolAction GetToolAction(this Entity entity) =>
        entity.TryGetToolAction(out ToolAction action)
            ? action
            : throw new System.InvalidOperationException($"{entity} carries no animator action.");

    /// <summary>Every entity carrying an animator action, hidden ones included.</summary>
    public static List<Entity> All(EntityStore store)
    {
        var found = new List<Entity>();
        foreach ((Entity entity, ToolActionKind _, UiCommand _) in UiCommand.All<ToolActionKind>(store))
            found.Add(entity);
        return found;
    }
}
