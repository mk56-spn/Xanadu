using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Tools.Animator.Commands;
using Position = Writer.Ecs.Position;

namespace Writer.Tools.Animator.Systems;

/// <summary>
/// Right-clicking a track row in the timeline opens a menu to add a key at
/// the clicked time or delete the key under the pointer. The options are
/// <see cref="ToolAction"/> buttons; this system picks what was clicked,
/// places the menu, closes it on the next click and dims the option that has
/// nothing to act on.
/// </summary>
public sealed class AnimatorKeyMenuSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color Dimmed = new("#334155");

    private bool _primaryHeld;
    private bool _secondaryHeld;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        bool primary = input.Pressed && !_primaryHeld;
        bool secondary = input.SecondaryPressed && !_secondaryHeld;
        _primaryHeld = input.Pressed;
        _secondaryHeld = input.SecondaryPressed;

        foreach (Entity session in Query.ToEntityList())
        {
            if (secondary)
                RightClick(session, input);
            else if (primary)
                session.GetComponent<AnimatorKeyMenu>().Open = false;

            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            AnimatorKeyMenu menu = session.GetComponent<AnimatorKeyMenu>();
            AnimatorMenuPlacement.Place(ui.KeyMenu, menu.Open ? menu.Anchor + AnimatorLayout.KeyMenuSize / 2 : UiLayout.Hidden);
            if (menu.Open)
            {
                bool hasKey = session.GetComponent<AnimatorSelection>().HasKey;
                Tint(ui.KeyMenuAdd, !hasKey);
                Tint(ui.KeyMenuDelete, hasKey);
            }
        }
    }

    private static void RightClick(Entity session, PointerInput input)
    {
        ref AnimatorKeyMenu menu = ref session.GetComponent<AnimatorKeyMenu>();
        menu.Open = false;

        AnimatorTimelineModel? model = AnimatorTimelineModel.Create(session);
        if (model is null || !model.Rows.HasPoint(input.Position))
            return;

        int row = model.ScreenRowAt(input.Position);
        if (row < 0)
            return;

        // A key under the pointer is selected, so DELETE KEY acts on it; empty
        // space keeps the playhead where it is and remembers the time to key.
        if (model.TryPickKey(input.Position, out XafTrack picked, out float keyTime))
        {
            // A key in a selected group keeps the group, so DELETE KEY removes them all.
            AnimatorKeyCommands.MakePrimary(session, picked, keyTime);
            menu.Track = picked;
            menu.Time = keyTime;
        }
        else
        {
            XafTrack track = model.TrackAt(row);
            AnimatorKeyCommands.SelectTrack(session, track);
            session.GetComponent<AnimatorSelection>().HasKey = false;
            menu.Track = track;
            menu.Time = Mathf.Clamp(
                (float)model.Snap(model.SecondsAtX(input.Position.X), input.Shift),
                0f,
                model.Clip.Duration);
        }

        menu.Open = true;
        menu.Anchor = AnimatorMenuPlacement.Anchor(
            input.Position,
            AnimatorLayout.KeyMenuSize,
            session.GetComponent<AnimatorUi>().ScreenSize);
    }

    private static void Tint(Entity button, bool enabled)
    {
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        Color target = enabled ? UiTheme.Button : Dimmed;
        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == target)
            return;

        visual.Normal = target;
        visual.Hover = target.Lerp(Colors.White, 0.16f);
    }

}
