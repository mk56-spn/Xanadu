using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Lights the editor button that is currently on and mutes the rest. Every
/// exclusive row uses it: the placement tools, dash strengths and block sizes.
/// </summary>
public static class EditorButtons
{
    /// <summary>
    /// Runs one exclusive button row: a held button sets the brush value, then
    /// every button is tinted by whether it is the chosen one. The placement,
    /// dash-strength and block-size rows all work exactly this way.
    /// </summary>
    public static void Exclusive(
        Entity editor,
        EntityStore entities,
        CommandBuffer buffer,
        EditorButtonKind kind,
        Func<EditorPlacement, int> current,
        Func<EditorPlacement, int, EditorPlacement> choose)
    {
        ref EditorPlacement placement = ref editor.GetComponent<EditorPlacement>();
        foreach (Entity button in EditorButton.All(entities, kind))
        {
            int arg = button.GetComponent<EditorButton>().Arg;
            if (button.Tags.Has<Pressed>())
                placement = choose(placement, arg);

            Tint(button, arg == current(placement), buffer);
        }
    }

    /// <summary>Colours a button as the current choice or not; a no-op when it has no visual.</summary>
    public static void Tint(Entity button, bool selected, CommandBuffer buffer)
    {
        if (!button.HasComponent<ButtonVisual>())
            return;

        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        Color normal = selected ? Palette.Primary : Palette.Muted;
        if (visual.Normal == normal)
            return;

        visual.Normal = normal;
        visual.Hover = normal.Lerp(Colors.White, 0.16f);
        visual.Flash = Colors.White;
        buffer.AddTag<Dirty>(button.Id);
    }
}
