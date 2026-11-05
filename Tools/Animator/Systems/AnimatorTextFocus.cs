using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Takes the typing focus off a text field, wherever a dialog is done with it.</summary>
internal static class AnimatorTextFocus
{
    public static void Clear(Entity field)
    {
        if (!field.IsNull && field.Tags.Has<TextFieldFocused>())
            field.RemoveTag<TextFieldFocused>();
    }
}
