using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Places the clip length panel under the CONFIG button, shows the current
/// clip's length in its numeric field, and commits a typed length when the
/// field loses focus. A click outside the panel closes it.
/// </summary>
public sealed class AnimatorClipConfigSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private bool _primaryHeld;
    private bool _wasOpen;
    private bool _wasEditing;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        bool primary = input.Pressed && !_primaryHeld;
        _primaryHeld = input.Pressed;

        foreach (Entity session in Query.ToEntityList())
            Update(session, primary, input.Position);
    }

    private void Update(Entity session, bool primary, Vector2 point)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.ClipLengthPanel.IsNull)
            return;

        ref AnimatorClipLength state = ref session.GetComponent<AnimatorClipLength>();
        if (!state.Open)
        {
            if (_wasOpen)
                Hide(ui);
            _wasOpen = false;
            return;
        }

        bool justOpened = !_wasOpen;
        _wasOpen = true;
        Place(ui);
        SyncLength(session, ui.ClipLengthField);

        if (justOpened || !primary)
            return;

        if (!AnimatorGeometry.PanelArea(ui.ClipLengthPanel).HasPoint(point))
        {
            state.Open = false;
            _wasOpen = false;
            Hide(ui);
        }
    }

    /// <summary>Drops the panel just under the CONFIG button, kept on screen.</summary>
    private static void Place(AnimatorUi ui)
    {
        Vector2 size = AnimatorLayout.ClipLengthSize;
        Rect2 button = AnimatorGeometry.PanelArea(ui.ConfigButton);
        Vector2 topLeft = new(
            Mathf.Clamp(
                button.End.X - size.X,
                0f,
                Mathf.Max(0f, ui.ScreenSize.X - size.X)),
            Mathf.Clamp(
                button.End.Y + AnimatorLayout.ClipLengthGap,
                0f,
                Mathf.Max(0f, ui.ScreenSize.Y - size.Y)));
        AnimatorMenuPlacement.Place(ui.ClipLengthPanel, topLeft, size);
    }

    private void Hide(AnimatorUi ui)
    {
        AnimatorMenuPlacement.Place(ui.ClipLengthPanel, UiLayout.Hidden);
        ClearFocus(ui.ClipLengthField);
        _wasEditing = false;
    }

    private void SyncLength(Entity session, Entity field)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        XafClip? clip = AnimatorSession.CurrentClip(
            session.GetComponent<AnimatorDocument>());
        bool editing = field.Tags.Has<TextFieldFocused>();
        ref TextField text = ref field.GetComponent<TextField>();

        if (!editing && _wasEditing)
            Commit(session, text.Value);

        if (!editing)
        {
            string value = clip is null
                ? ""
                : clip.Duration.ToString("0.00", CultureInfo.InvariantCulture);
            if (text.Value != value)
            {
                text.Value = value;
                field.AddTag<Dirty>();
            }
        }

        _wasEditing = editing;
    }

    private static void Commit(Entity session, string typed)
    {
        if (!float.TryParse(typed.Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out float seconds) ||
            !float.IsFinite(seconds))
        {
            AnimatorSession.SetStatus(session, "LENGTH MUST BE A NUMBER");
            return;
        }

        AnimatorClipCommands.SetDuration(session, seconds);
    }

    private static void ClearFocus(Entity field)
    {
        if (!field.IsNull && field.Tags.Has<TextFieldFocused>())
            field.RemoveTag<TextFieldFocused>();
    }
}
