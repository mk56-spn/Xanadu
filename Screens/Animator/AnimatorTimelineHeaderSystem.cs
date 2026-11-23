using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>
/// The bar over the timeline: hidden while editing the rig (the rest pose has
/// no keys), and the readout beside the transport kept current otherwise.
/// </summary>
public sealed class AnimatorTimelineHeaderSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            bool rest = AnimatorModes.Of(session).Rest;
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            if (!ui.TimelineHeader.IsNull && UiLayout.IsHidden(ui.TimelineHeader) != rest)
                UiLayout.SetHidden(ui.TimelineHeader, rest);
            if (!rest && AnimatorTimelineModel.Create(session) is { } model)
                WriteCaption(ui.TimelineCaption, model.Caption(session.GetComponent<AnimatorDocument>().Runtime.CurrentTime));
        }
    }

    /// <summary>Writes the readout into the caption beside the transport; layout places it.</summary>
    private void WriteCaption(Entity label, string caption)
    {
        if (label.IsNull || !label.HasComponent<Text>())
            return;

        ref Text text = ref label.GetComponent<Text>();
        if (text.Value == caption)
            return;

        text.Value = caption;
        CommandBuffer.AddTag<Dirty>(label.Id);
    }
}
