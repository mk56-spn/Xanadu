using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Animator;

/// <summary>Takes the typing focus off a text field, wherever a dialog is done with it.</summary>
public static class AnimatorTextFocus
{
    public static void Clear(Entity field)
    {
        if (!field.IsNull && field.Tags.Has<TextFieldFocused>())
            field.RemoveTag<TextFieldFocused>();
    }
}
