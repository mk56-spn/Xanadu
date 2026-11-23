using System.Globalization;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// Opens, closes and creates from the NEW CLIP dialog. Creation reads the
/// dialog's fields, so an invalid name or length keeps the dialog open with
/// the reason shown in it.
/// </summary>
public static class AnimatorNewClipDialogCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<OpenNewClip>((s, e) => Open(s, duplicate: e.Copy));
        session.On<CreateClip>((s, _) => Create(s));
        session.On<CloseNewClip>((s, _) => Close(s));
    }

    public static void Open(Entity session, bool duplicate)
    {
        Entity dialog = AnimatorUi.Of(session).NewClipDialog;
        dialog.SetOpen(true);

        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (!ui.NewClipDuplicate.IsNull && ui.NewClipDuplicate.HasComponent<Toggle>())
            ui.NewClipDuplicate.GetComponent<Toggle>().Value = duplicate;

        AnimatorSession.SetStatus(session, "NAME AND LENGTH THE NEW CLIP");
    }

    public static void Close(Entity session) =>
        AnimatorUi.Of(session).NewClipDialog.SetOpen(false);

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

        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
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

        if (length < RigClipAuthoring.MinDuration)
        {
            Error(session, ui, $"LENGTH AT LEAST {RigClipAuthoring.MinDuration:0.00}s");
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
