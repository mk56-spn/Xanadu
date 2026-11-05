using System.Globalization;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>The animator actions that need more than one call: playing, selecting, menus and rig edits.</summary>
public static class AnimatorSessionCommands
{
    /// <summary>
    /// Switches to a clip at once. Crossfading is for the game; here the
    /// timeline, name field and auto-key must all act on the clip you chose.
    /// </summary>
    internal static void PlayClip(Entity session, string clipId)
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

    internal static void SelectBone(Entity session, string boneId)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        int index = package.Skeleton.Bones.FindIndex(bone => bone.Id == boneId);
        if (index < 0)
            return;

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyBone(index);
        AnimatorSession.SetStatus(session, $"SELECTED {boneId}");
    }

    /// <summary>Adds a key at the row and time the key menu was opened on.</summary>
    internal static void AddKeyFromMenu(Entity session)
    {
        AnimatorKeyMenu menu = session.GetComponent<AnimatorKeyMenu>();
        AnimatorKeyCommands.AddKeyAt(
            session, new AnimatorTrackRow(menu.Track, menu.Property), menu.Time);
    }

    /// <summary>
    /// Opens the ADD SVG picker beside the bone menu, on the selected bone.
    /// The picker system lists the project's groups and places the panel.
    /// </summary>
    internal static void OpenSvgMenu(Entity session)
    {
        int bone = SelectedBone(session);
        if (bone < 0)
        {
            AnimatorSession.SetStatus(session, "SELECT A BONE FIRST");
            return;
        }

        ref AnimatorSvgMenu menu = ref session.GetComponent<AnimatorSvgMenu>();
        menu.Open = true;
        menu.ForChain = false;
        menu.ForArt = false;
        menu.Bone = bone;
        menu.Anchor = OpenAnchor(session);
    }

    /// <summary>Where the picker starts: just right of the bone menu that opened it.</summary>
    internal static Vector2 OpenAnchor(Entity session)
    {
        Vector2 boneMenu = session.GetComponent<AnimatorBoneMenu>().Anchor;
        var offset = new Vector2(AnimatorLayout.BoneMenuSize.X + 4, 0);
        return boneMenu + offset;
    }

    /// <summary>Adds the picker row the argument indexes, keeping the picker open.</summary>
    internal static void AttachSvgFromMenu(Entity session, string argument)
    {
        AnimatorSvgMenu menu = session.GetComponent<AnimatorSvgMenu>();
        if (menu.Choices is null ||
            !int.TryParse(argument, out int index) ||
            index < 0 ||
            index >= menu.Choices.Count)
        {
            return;
        }

        AnimatorSvgChoice choice = menu.Choices[index];
        if (menu.ForArt)
            AnimatorArtCommands.AddArt(session, choice);
        else if (menu.ForChain)
            AnimatorChainPlacementCommands.SetArt(session, choice);
        else
            AnimatorAttachmentCommands.Add(session, choice.Path, choice.Group, menu.Bone);
    }

    /// <summary>Stops playback; pressed again once stopped, it returns to the clip's start.</summary>
    internal static void Stop(Entity session)
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
    internal static void TogglePlay(Entity session)
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

    /// <summary>Flips one editing mode by name, as clicking its toggle does.</summary>
    internal static void ToggleMode(Entity session, string argument)
    {
        if (!System.Enum.TryParse(argument, out AnimatorMode mode))
            return;

        ref AnimatorModes modes = ref session.GetComponent<AnimatorModes>();
        bool value = !modes.Get(mode);
        modes.Set(mode, value);
        AnimatorModeSystem.Announce(session, mode, value);
    }

    /// <summary>Shows the name field over the selected bone; the menu system focuses it.</summary>
    internal static void BeginBoneRename(Entity session)
    {
        int bone = SelectedBone(session);
        if (bone < 0)
            return;

        session.GetComponent<AnimatorBoneMenu>().Renaming = bone;
        AnimatorSession.SetStatus(session, "TYPE A NAME, ENTER TO RENAME");
    }

    /// <summary>The next bone clicked in the preview becomes the selected bone's parent.</summary>
    internal static void BeginBoneReparent(Entity session)
    {
        int bone = SelectedBone(session);
        if (bone < 0)
            return;

        XafBone rig = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones[bone];
        if (rig.Parent < 0)
        {
            AnimatorSession.SetStatus(session, "ROOT CANNOT BE REPARENTED");
            return;
        }

        session.GetComponent<AnimatorBoneMenu>().Reparenting = bone;
        AnimatorSession.SetStatus(session, $"CLICK THE NEW PARENT OF {rig.Id}");
    }

    /// <summary>The step a length arrow asks for, or the default 0.1 s for a keybinding.</summary>
    internal static float DurationStep(string argument) =>
        float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture,
            out float seconds) && seconds > 0f
            ? seconds
            : AnimatorClipCommands.DurationStep;

    internal static int SelectedBone(Entity session)
    {
        int bone = session.GetComponent<AnimatorSelection>().Bone;
        int count = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones.Count;
        return bone >= 0 && bone < count ? bone : -1;
    }

    internal static void SelectAttachment(Entity session, string argument)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (!int.TryParse(argument, out int index) ||
            index < 0 ||
            index >= package.Attachments.Count)
        {
            return;
        }

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyAttachment(index);
        AnimatorSession.SetStatus(
            session,
            $"SELECTED {package.Attachments[index].Group}");
    }
}
