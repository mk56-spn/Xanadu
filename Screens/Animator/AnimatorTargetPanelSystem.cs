using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>Says what the selected bone's override is in the POSE tab's OVERRIDES section.</summary>
public sealed class AnimatorTargetPanelSystem : QuerySystem<ListIndex, Text>
{
    public AnimatorTargetPanelSystem() => Filter.AllTags(Tags.Get<AnimatorTargetCaption>());

    protected override void OnUpdate()
    {
        Entity session = AnimatorSession.Find(Query.Store);
        if (session.IsNull)
            return;

        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        RigLimbTarget? target = AnimatorSelected.Target(session);
        foreach (Entity caption in Query.ToEntityList())
            UiText.Set(caption, Line(package, target, caption.GetComponent<ListIndex>().Value));
    }

    private static string Line(RigPackage package, RigLimbTarget? target, int line)
    {
        if (target is null)
            return line == 0 ? "NO OVERRIDE ON THIS BONE" : "";
        if (!RigTargets.IsValid(package, target))
            return line == 0 ? $"MISSING BONE: {target.BoneId}" : "DETACH OR RENAME IT BACK";

        string anchor = target.AnchorId.Length > 0 ? target.AnchorId : "RIG";
        if (line == 0)
            return $"{target.BoneId} · ON {anchor}";
        string span = $"{target.From:0.00}-{target.To:0.00} S ({target.To - target.From:0.00} S)";
        return target.Shape == RigTargetShape.Spot
            ? $"PIN · {span}"
            : $"ELLIPSE · {target.Sweep / float.Tau:0.##} TURN · {span}";
    }
}
