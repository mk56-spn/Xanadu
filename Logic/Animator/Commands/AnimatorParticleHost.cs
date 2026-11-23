using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Core.Particles;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// Hosts the shared particle editor in the animator: it shows while the
/// selected part streams particles and edits that part's own settings (its
/// node's <see cref="XifParticles"/>), each change a step on the animator's
/// undo history. The preview's streams play the settings in place, so edits
/// show at once.
/// </summary>
public sealed class AnimatorParticleHost(EntityStore store) : IParticleEditorHost
{
    private Entity Session => AnimatorSession.Find(store);

    private RigAttachment? Effect
    {
        get
        {
            Entity session = Session;
            if (session.IsNull || AnimatorPreviewMode.IsOn(session))
                return null;

            int index = AnimatorSelected.Part(session);
            return index < 0 ? null : session.GetComponent<AnimatorDocument>().Package.Attachments[index];
        }
    }

    public XpfParticle? Particle => Effect?.Particles;

    public string Title => Effect is { } effect ? $"{effect.Id}  particles" : "";

    public void Use(XpfParticle settings)
    {
        Entity session = Session;
        int index = session.IsNull ? -1 : AnimatorSelected.Part(session);
        if (index < 0)
            return;

        AnimatorHistoryCommands.Edit(session, () =>
        {
            ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
            document.Package.Attachments[index].Particles = settings;
            document.EffectsVersion++;
        });
    }

    public void Edit(Action change)
    {
        Entity session = Session;
        if (!session.IsNull)
            AnimatorHistoryCommands.Edit(session, change);
    }

    public void BeginGesture() => AnimatorHistoryCommands.BeginGesture(Session);

    public void EndGesture() => AnimatorHistoryCommands.EndGesture(Session);

    /// <summary>The streams play the settings in place; nothing to rebuild.</summary>
    public void Changed() { }
}
