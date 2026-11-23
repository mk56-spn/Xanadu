using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Timeline;
using Xanadu.Logic.Animator;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>Creates the single entity that holds animator editing state, and resets it for a new package.</summary>
public static class AnimatorSessionSetup
{
    public static Entity Create(
        EntityStore store,
        RigPackage package,
        IAnimatorFiles files,
        string path)
    {
        var runtime = new AnimatorRuntime(package);
        AnimatorSession.StartClip(runtime, "idle");
        Entity session = store.CreateEntity(
            new AnimatorDocument
            {
                Package = package,
                Runtime = runtime,
                Playback = new AnimatorPlayback(runtime),
                Files = files,
            },
            AnimatorSelection.None,
            new AnimatorDrag(),
            new AnimatorStatus { Message = "" },
            new AnimatorFile
            {
                Path = path,
                Saved = RigPackageCopy.Clone(package),
                Dirty = false,
            },
            new TimelineView(1f),
            new AnimatorTracks { Expanded = [] },
            AnimatorBoneMenu.Closed,
            new AnimatorSessionState { Event = AnimatorEventSelection.None });
        session.AddTags(AnimatorModeTags.Defaults);
        new AnimatorTimeline(session).Listen();
        session.AddComponent(new AnimatorKeyMenu());
        session.AddComponent(new AnimatorKeyTryout { Clip = "", Targets = [] });
        session.AddComponent(AnimatorHover.None);
        session.AddComponent(AnimatorPreviewView.Default);
        session.AddComponent(new AnimatorSequence());
        session.AddComponent(new AnimatorAttackCycle());
        session.AddComponent(AnimatorMetronome.Default);
        session.AddComponent(new AnimatorFlow { Target = "" });
        session.AddComponent(new AnimatorSnapGuides());
        session.AddComponent(new AnimatorChains { Simulation = new RigChainSimulation() });
        session.AddComponent(new AnimatorCloths { Simulation = new RigClothSimulation() });
        session.AddComponent(new AnimatorPreviewMode());
        session.AddComponent(new AnimatorMeshPick());
        session.AddComponent(AnimatorWeightPaint.Default);
        session.AddComponent(new UiMarqueeBox());
        session.AddComponent(new AnimatorPlants { Held = [] });
        session.GetComponent<AnimatorSessionState>().History.History = AnimatorHistoryCommands.CreateHistory(session);
        AnimatorCommands.Listen(session);
        return session;
    }

    /// <summary>Swaps in a new package and resets selection and playback to match.</summary>
    public static void Replace(Entity session, RigPackage package)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        document.Package = package;
        AnimatorSession.Restart(ref document, "idle");
        session.GetComponent<AnimatorSelection>() = AnimatorSelection.None;
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag();
        session.GetComponent<AnimatorBoneMenu>() = AnimatorBoneMenu.Closed;
        session.GetComponent<AnimatorChains>().Picking = AnimatorChainPick.None;
        session.GetComponent<AnimatorTracks>().Expanded?.Clear();
        session.GetComponent<AnimatorKeyMenu>() = new AnimatorKeyMenu();
        AnimatorUi.Of(session).ClipLengthPanel.SetOpen(false);
        AnimatorUi.Of(session).NewClipDialog.SetOpen(false);
        session.GetComponent<AnimatorSessionState>().Event = AnimatorEventSelection.None;
        session.GetComponent<AnimatorMeshPick>() = new AnimatorMeshPick();
        session.GetComponent<AnimatorPlants>().Held.Clear();
        AnimatorHistoryCommands.Clear(session);
    }
}
