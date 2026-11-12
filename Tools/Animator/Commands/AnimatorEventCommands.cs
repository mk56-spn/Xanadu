using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>Clip events: adding one at the playhead, picking, retiming, renaming and deleting.</summary>
public static class AnimatorEventCommands
{
    /// <summary>The selected event's index in the open clip, or -1.</summary>
    public static int Selected(Entity session, out XafClip? clip)
    {
        clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        AnimatorEventSelection selection = session.GetComponent<AnimatorSessionState>().Event;
        return clip is not null && selection.Clip == clip.Id && selection.Index >= 0 &&
               selection.Index < clip.Events.Count
            ? selection.Index
            : -1;
    }

    public static void Select(Entity session, string clip, int index) =>
        session.GetComponent<AnimatorSessionState>().Event = new AnimatorEventSelection { Clip = clip, Index = index };

    /// <summary>Adds an event at the playhead, selects it and pauses, as one undo step.</summary>
    public static void Add(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            if (AnimatorSession.CurrentClip(document) is not { } clip)
                return;

            document.Runtime.SetPaused(true);
            int index = XafEventAuthoring.Add(clip, document.Runtime.CurrentTime);
            if (index < 0)
                return;
            Select(session, clip.Id, index);
            AnimatorSession.SetStatus(session,
                $"EVENT {clip.Events[index].Name.ToUpperInvariant()} AT {clip.Events[index].Time:0.00} S · NAME IT IN THE PANEL");
        });

    public static void Delete(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = Selected(session, out XafClip? clip);
            if (index < 0 || clip is null)
            {
                AnimatorSession.SetStatus(session, "PICK AN EVENT ON THE RULER FIRST");
                return;
            }

            string name = clip.Events[index].Name;
            XafEventAuthoring.Remove(clip, index);
            session.GetComponent<AnimatorSessionState>().Event = AnimatorEventSelection.None;
            AnimatorSession.SetStatus(session, $"DELETED EVENT {name.ToUpperInvariant()}");
        });

    public static void Rename(Entity session, string name) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = Selected(session, out XafClip? clip);
            if (index >= 0 && clip is not null && XafEventAuthoring.Rename(clip, index, name))
                AnimatorSession.SetStatus(session, $"EVENT NAMED {clip.Events[index].Name.ToUpperInvariant()}");
            else
                AnimatorSession.SetStatus(session, "AN EVENT NEEDS A NAME");
        });

    /// <summary>Moves the selected event to a time, keeping it selected; one undo step per gesture.</summary>
    public static void Retime(Entity session, float time) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = Selected(session, out XafClip? clip);
            if (index < 0 || clip is null)
                return;

            int moved = XafEventAuthoring.Move(clip, index, time);
            Select(session, clip.Id, moved);
            AnimatorSession.SetStatus(session,
                $"EVENT {clip.Events[moved].Name.ToUpperInvariant()} AT {clip.Events[moved].Time:0.00} S");
        });
}
