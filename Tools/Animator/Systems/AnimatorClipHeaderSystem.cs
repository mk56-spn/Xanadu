using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// The clip controls in the timeline header: the name field shows the current
/// clip and renames it when editing ends, and the loop button shows its state.
/// </summary>
public sealed class AnimatorClipHeaderSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color LoopOn = Palette.Selected;
    private static readonly Color LoopOff = Palette.Muted;

    private bool _wasEditing;
    private string _editedClip = "";

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            SyncName(session, ui.ClipName);
            SyncLoop(session, ui.LoopButton);
        }
    }

    private void SyncName(Entity session, Entity field)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        string current = document.Runtime.CurrentClip;
        bool editing = field.Tags.Has<TextFieldFocused>();
        ref TextField text = ref field.GetComponent<TextField>();

        if (editing && !_wasEditing)
            _editedClip = current;

        if (!editing && _wasEditing)
            Commit(session, text.Value);

        if (!editing)
            text.Value = session.GetComponent<AnimatorDocument>().Runtime.CurrentClip;

        _wasEditing = editing;
    }

    /// <summary>Applies the typed name to the clip that was being edited, even if another was selected since.</summary>
    private void Commit(Entity session, string typed)
    {
        string name = typed.Trim();
        if (name.Length == 0 || name == _editedClip || _editedClip.Length == 0)
            return;

        AnimatorClipCommands.Rename(session, _editedClip, name);
    }

    private static void SyncLoop(Entity session, Entity button)
    {
        if (button.IsNull || !button.HasComponent<Text>() || !button.HasComponent<ButtonVisual>())
            return;

        XafClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        bool loop = clip?.Loop ?? false;
        string label = "LOOP";
        ref Text text = ref button.GetComponent<Text>();
        if (text.Value != label)
        {
            text.Value = label;
            button.AddTag<Dirty>();
        }

        Color target = loop ? LoopOn : LoopOff;
        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == target)
            return;

        visual.Normal = target;
        visual.Hover = target.Lerp(Colors.White, 0.16f);
    }
}
