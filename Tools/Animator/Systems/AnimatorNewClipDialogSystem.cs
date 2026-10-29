using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Centres the NEW CLIP dialog over a dimmer, seeds its name and length from
/// the current clip, keeps those defaults in step with the duplicate toggle,
/// and cancels on a click outside it.
/// </summary>
public sealed class AnimatorNewClipDialogSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private bool _primaryHeld;
    private bool _wasOpen;
    private bool _lastDuplicate;

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
        if (ui.NewClipDialog.IsNull || ui.NewClipBackdrop.IsNull)
            return;

        ref AnimatorNewClipDialog dialog = ref session.GetComponent<AnimatorNewClipDialog>();
        if (!dialog.Open)
        {
            if (_wasOpen)
                Hide(ui);
            _wasOpen = false;
            return;
        }

        bool justOpened = !_wasOpen;
        _wasOpen = true;
        if (justOpened)
        {
            Seed(session, ui);
            Focus(ui.NewClipName);
        }

        Place(ui);
        SyncToggle(session, ui);

        if (justOpened || !primary)
            return;

        if (!AnimatorGeometry.PanelArea(ui.NewClipDialog).HasPoint(point))
        {
            dialog.Open = false;
            _wasOpen = false;
            Hide(ui);
        }
    }

    private static void Place(AnimatorUi ui)
    {
        Vector2 size = AnimatorLayout.NewClipDialogSize;
        AnimatorMenuPlacement.Place(ui.NewClipBackdrop, ui.ScreenSize / 2);
        AnimatorMenuPlacement.Place(
            ui.NewClipDialog,
            (ui.ScreenSize - size) / 2,
            size);
    }

    private static void Hide(AnimatorUi ui)
    {
        AnimatorMenuPlacement.Place(ui.NewClipBackdrop, UiLayout.Hidden);
        AnimatorMenuPlacement.Place(ui.NewClipDialog, UiLayout.Hidden);
        ClearFocus(ui.NewClipName);
        ClearFocus(ui.NewClipLength);
    }

    /// <summary>Fills the fields with the defaults for the duplicate toggle's current value.</summary>
    private void Seed(Entity session, AnimatorUi ui)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        XafClip? current = AnimatorSession.CurrentClip(
            session.GetComponent<AnimatorDocument>());
        bool duplicate = Duplicate(ui);

        string name = duplicate && current is not null
            ? XafClipAuthoring.UniqueId(package, current.Id)
            : XafClipAuthoring.UniqueId(package, "clip");
        float length = duplicate && current is not null
            ? current.Duration
            : XafClipAuthoring.DefaultDuration;

        SetField(ui.NewClipName, name);
        SetField(ui.NewClipLength,
            length.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(ui.NewClipError, "");
        _lastDuplicate = duplicate;
    }

    /// <summary>Flipping the duplicate toggle gives the fields that mode's defaults.</summary>
    private void SyncToggle(Entity session, AnimatorUi ui)
    {
        if (Duplicate(ui) == _lastDuplicate)
            return;

        Seed(session, ui);
    }

    private static bool Duplicate(AnimatorUi ui) =>
        !ui.NewClipDuplicate.IsNull &&
        ui.NewClipDuplicate.HasComponent<Toggle>() &&
        ui.NewClipDuplicate.GetComponent<Toggle>().Value;

    private static void Focus(Entity field)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        field.AddTag<TextFieldFocused>();
    }

    private static void ClearFocus(Entity field)
    {
        if (!field.IsNull && field.Tags.Has<TextFieldFocused>())
            field.RemoveTag<TextFieldFocused>();
    }

    private static void SetField(Entity field, string value)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        ref TextField text = ref field.GetComponent<TextField>();
        if (text.Value == value)
            return;

        text.Value = value;
        field.AddTag<Dirty>();
    }

    private static void SetText(Entity label, string value)
    {
        if (label.IsNull || !label.HasComponent<Text>())
            return;

        ref Text text = ref label.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        label.AddTag<Dirty>();
    }
}
