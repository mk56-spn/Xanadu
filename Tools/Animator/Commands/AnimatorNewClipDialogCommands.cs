using System.Globalization;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Opens, closes and creates from the NEW CLIP dialog. Creation reads the
/// dialog's fields, so an invalid name or length keeps the dialog open with
/// the reason shown in it.
/// </summary>
public static class AnimatorNewClipDialogCommands
{
    public static void Open(Entity session, bool duplicate)
    {
        ref AnimatorNewClipDialog dialog = ref session.GetComponent<AnimatorNewClipDialog>();
        dialog.Open = true;

        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (!ui.NewClipDuplicate.IsNull && ui.NewClipDuplicate.HasComponent<Toggle>())
            ui.NewClipDuplicate.GetComponent<Toggle>().Value = duplicate;

        AnimatorSession.SetStatus(session, "NAME AND LENGTH THE NEW CLIP");
    }

    public static void Close(Entity session) =>
        session.GetComponent<AnimatorNewClipDialog>().Open = false;

    public static void Create(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        string name = FieldValue(ui.NewClipName).Trim();
        string lengthText = FieldValue(ui.NewClipLength).Trim();
        bool duplicate = !ui.NewClipDuplicate.IsNull &&
                         ui.NewClipDuplicate.HasComponent<Toggle>() &&
                         ui.NewClipDuplicate.GetComponent<Toggle>().Value;

        if (name.Length == 0)
        {
            Error(session, ui, "NAME IS EMPTY");
            return;
        }

        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (package.Clips.Exists(clip => clip.Id == name))
        {
            Error(session, ui, "NAME ALREADY USED");
            return;
        }

        if (!float.TryParse(lengthText, NumberStyles.Float, CultureInfo.InvariantCulture,
                out float length))
        {
            Error(session, ui, "LENGTH MUST BE A NUMBER");
            return;
        }

        if (!float.IsFinite(length))
        {
            Error(session, ui, "LENGTH MUST BE A NUMBER");
            return;
        }

        if (length < XafClipAuthoring.MinDuration)
        {
            Error(session, ui, $"LENGTH AT LEAST {XafClipAuthoring.MinDuration:0.00}s");
            return;
        }

        if (!AnimatorClipCommands.Create(session, name, length, duplicate))
        {
            Error(session, ui, "COULD NOT CREATE THE CLIP");
            return;
        }

        Close(session);
    }

    private static string FieldValue(Entity field) =>
        field.IsNull || !field.HasComponent<TextField>()
            ? ""
            : field.GetComponent<TextField>().Value;

    private static void Error(Entity session, AnimatorUi ui, string message)
    {
        AnimatorSession.SetStatus(session, message);
        if (ui.NewClipError.IsNull || !ui.NewClipError.HasComponent<Text>())
            return;

        ref Text text = ref ui.NewClipError.GetComponent<Text>();
        if (text.Value == message)
            return;

        text.Value = message;
        ui.NewClipError.AddTag<Dirty>();
    }
}
