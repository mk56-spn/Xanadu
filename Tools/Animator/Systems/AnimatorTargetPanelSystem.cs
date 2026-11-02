using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Says what the selected bone's override is in the POSE tab's OVERRIDES section.</summary>
public sealed class AnimatorTargetPanelSystem : QuerySystem<AnimatorTargetCaption, Text>
{
    protected override void OnUpdate()
    {
        Entity session = AnimatorSession.Find(Query.Store);
        if (session.IsNull)
            return;

        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        XafLimbTarget? target = AnimatorTargetCommands.Selected(session);
        foreach (Entity caption in Query.ToEntityList())
            UiText.Set(caption, Line(package, target, caption.GetComponent<AnimatorTargetCaption>().Line));
    }

    private static string Line(XafPackage package, XafLimbTarget? target, int line)
    {
        if (target is null)
            return line == 0 ? "NO OVERRIDE ON THIS BONE" : "";
        if (!XafTargets.IsValid(package, target))
            return line == 0 ? $"MISSING BONE: {target.BoneId}" : "DETACH OR RENAME IT BACK";

        string anchor = target.AnchorId.Length > 0 ? target.AnchorId : "RIG";
        if (line == 0)
            return $"{target.BoneId} · ON {anchor}";
        string span = $"{target.From:0.00}-{target.To:0.00} S ({target.To - target.From:0.00} S)";
        return target.Shape == XafTargetShape.Spot
            ? $"PIN · {span}"
            : $"ELLIPSE · {target.Sweep / Mathf.Tau:0.##} TURN · {span}";
    }
}
