using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>The animator actions that need more than one call: playing, selecting, menus and rig edits.</summary>
public static class AnimatorSessionCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<StartPlaying>((s, _) =>
        {
            s.GetComponent<AnimatorDocument>().Playback.Play();
            AnimatorSession.SetStatus(s, "PLAYING");
        });
        session.On<StopPlaying>((s, _) => Stop(s));
        session.On<TogglePlay>((s, _) => TogglePlay(s));
        session.On<PlayClip>((s, e) => PlayClip(s, e.Clip));
        session.On<SelectBone>((s, e) => SelectBone(s, e.Bone));
        session.On<SelectPart>((s, e) => SelectAttachment(s, e.Part));
        session.On<RenameBone>((s, _) => BeginBoneRename(s));
        session.On<ReparentBone>((s, _) => BeginBoneReparent(s));
        session.On<AddKey>((s, _) => AddKeyFromMenu(s));
        session.On<ToggleMode>((s, e) => ToggleMode(s, e.Mode));
    }

    /// <summary>Says what the mode change means, the same way a click on its toggle does.</summary>
    public static void AnnounceMode(Entity session, AnimatorMode mode, bool value)
    {
        if (mode == AnimatorMode.Rest)
        {
            session.GetComponent<AnimatorDocument>().Runtime.SetPaused(true);
            AnimatorSession.SetStatus(session, value ? "EDITING THE RIG: CHANGES MOVE EVERY CLIP" : "ANIMATING");
        }
        else
        {
            if (mode == AnimatorMode.Parts)
            {
                AnimatorSession.SetStatus(session, value ? "PART PLACEMENT" : "BONE MOVEMENT");
                return;
            }

            string name = mode switch
            {
                AnimatorMode.AutoKey => "AUTO KEY",
                AnimatorMode.Ik => "IK DRAG",
                AnimatorMode.Pin => "PIN CHILDREN",
                AnimatorMode.Kites => "BONE KITES",
                AnimatorMode.Handles => "HANDLES",
                AnimatorMode.Align => "ALIGN TO JOINTS",
                AnimatorMode.FullKeys => "FULL KEY VIEW",
                _ => "ONION",
            };
            AnimatorSession.SetStatus(session, $"{name} {(value ? "ON" : "OFF")}");
        }
    }

    /// <summary>
    /// Switches to a clip at once. Crossfading is for the game; here the
    /// timeline, name field and auto-key must all act on the clip you chose.
    /// </summary>
    public static void PlayClip(Entity session, string clipId)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        if (!document.Runtime.Play(clipId, 0f))
        {
            AnimatorSession.SetStatus(session, $"NO CLIP {clipId}");
            return;
        }

        session.GetComponent<AnimatorSelection>().HasKey = false;
        document.Runtime.SetPaused(false);
    }

    public static void SelectBone(Entity session, int index)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (index < 0 || index >= package.Skeleton.Bones.Count)
            return;

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyBone(index);
        AnimatorSession.SetStatus(session, $"SELECTED {package.Skeleton.Bones[index].Id}");
    }

    /// <summary>Adds a key at the row and time the key menu was opened on.</summary>
    public static void AddKeyFromMenu(Entity session)
    {
        AnimatorKeyMenu menu = session.GetComponent<AnimatorKeyMenu>();
        AnimatorKeyCommands.AddKeyAt(
            session, new AnimatorTrackRow(menu.Track, menu.Property), menu.Time);
    }

    /// <summary>Stops playback; pressed again once stopped, it returns to the clip's start.</summary>
    public static void Stop(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        bool alreadyStopped = !document.Playback.IsPlaying &&
                              !session.GetComponent<AnimatorSequence>().Active &&
                              !session.GetComponent<AnimatorAttackCycle>().Active;
        AnimatorSequenceCommands.Finish(session);
        AnimatorAttackCycleCommands.Finish(session);
        document.Playback.Stop();
        if (alreadyStopped)
            document.Runtime.Seek(0f);
        AnimatorSession.SetStatus(session, alreadyStopped ? "BACK TO START" : "STOPPED");
    }

    /// <summary>Plays if stopped and stops if playing, the one action behind Space.</summary>
    public static void TogglePlay(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (document.Playback.IsPlaying)
        {
            document.Playback.Stop();
            AnimatorSession.SetStatus(session, "STOPPED");
        }
        else
        {
            document.Playback.Play();
            AnimatorSession.SetStatus(session, "PLAYING");
        }
    }

    /// <summary>Flips one editing mode, as clicking its toggle does.</summary>
    public static void ToggleMode(Entity session, AnimatorMode mode)
    {
        AnimatorModes modes = AnimatorModes.Of(session);
        bool preview = AnimatorPreviewMode.IsOn(session);
        if (!modes.Applies(mode, preview))
        {
            AnimatorSession.SetStatus(session, preview ? "PREVIEW IS FOR WATCHING: ANIMATE TO EDIT"
                : modes.Rest ? "THAT SHOWS MOTION: LEAVE EDIT RIG FOR IT"
                : "PIN CHILDREN WORKS WHILE EDITING THE RIG");
            return;
        }

        bool value = !session.Tags.HasAll(AnimatorModeTags.Of(mode));
        AnimatorModeTags.Set(session, mode, value);
        AnimatorSessionCommands.AnnounceMode(session, mode, value);
    }

    /// <summary>Shows the name field over the selected bone; the menu system focuses it.</summary>
    public static void BeginBoneRename(Entity session)
    {
        int bone = SelectedBone(session);
        if (bone < 0)
            return;

        session.GetComponent<AnimatorBoneMenu>().Renaming = bone;
        AnimatorSession.SetStatus(session, "TYPE A NAME, ENTER TO RENAME");
    }

    /// <summary>The next bone clicked in the preview becomes the selected bone's parent.</summary>
    public static void BeginBoneReparent(Entity session)
    {
        int bone = SelectedBone(session);
        if (bone < 0)
            return;

        RigBone rig = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones[bone];
        if (rig.Parent < 0)
        {
            AnimatorSession.SetStatus(session, "ROOT CANNOT BE REPARENTED");
            return;
        }

        session.GetComponent<AnimatorBoneMenu>().Reparenting = bone;
        AnimatorSession.SetStatus(session, $"CLICK THE NEW PARENT OF {rig.Id}");
    }

    public static int SelectedBone(Entity session)
    {
        int bone = session.GetComponent<AnimatorSelection>().Bone;
        int count = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones.Count;
        return bone >= 0 && bone < count ? bone : -1;
    }

    public static void SelectAttachment(Entity session, int index)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (index < 0 || index >= package.Attachments.Count)
            return;

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyAttachment(index);
        AnimatorSession.SetStatus(
            session,
            $"SELECTED {package.Attachments[index].Id}");
    }
}
