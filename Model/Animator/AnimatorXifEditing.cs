using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Animator;

/// <summary>What the XIF editor is holding for the animator, as the preview draws it.</summary>
public static class AnimatorXifEditing
{
    /// <summary>
    /// The part the XIF editor holds on its bone, if it is open on one: the
    /// preview draws it above the editor's see-through backing instead of
    /// under it, so the order is the character, the backing, then the part. -1 otherwise.
    /// </summary>
    public static int HeldOnBone(Entity session)
    {
        Entity editor = session.GetComponent<AnimatorUi>().XifEditor;
        if (!Xanadu.Ui.Systems.Ui.XifEditorSystem.IsOpen(editor) ||
            editor.GetComponent<XifEditorFrame>().Placement is null)
            return -1;
        return AnimatorSelected.Part(session);
    }

    /// <summary>
    /// The item the XIF editor holds in its shape box (chain or cloth art, a
    /// skinned part), with where the box shows it: the preview draws it there,
    /// through its shader, above the editor's backing. Null otherwise.
    /// </summary>
    public static (Xif Item, Transform2D Shown)? Boxed(Entity session)
    {
        Entity editor = session.GetComponent<AnimatorUi>().XifEditor;
        if (!Xanadu.Ui.Systems.Ui.XifEditorSystem.IsOpen(editor))
            return null;
        XifEditorFrame frame = editor.GetComponent<XifEditorFrame>();
        if (frame.Placement is not null || frame.Item is not { } item)
            return null;
        return (item, editor.GetComponent<XifEditor>().Shown);
    }
}
