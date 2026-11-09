using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Hosts the shared particle editor in the animator: it shows while the
/// selected effect streams particles, and choosing another effect for it is a
/// step on the animator's undo history. Edits to the effect itself go to the
/// particle library, which the preview picks up by itself.
/// </summary>
public sealed class AnimatorParticleHost(EntityStore store) : IParticleEditorHost
{
    private Entity Session => AnimatorSession.Find(store);

    private XafEffect? Effect
    {
        get
        {
            Entity session = Session;
            if (session.IsNull || AnimatorPreviewModeCommands.IsOn(session))
                return null;

            int index = AnimatorEffectCommands.Selected(session);
            return index < 0 ? null : session.GetComponent<AnimatorDocument>().Package.Effects[index];
        }
    }

    public string? Particle => Effect is { Particle.Length: > 0 } effect ? effect.Particle : null;

    public string Title => Effect is { } effect ? $"{effect.Id}  particles" : "";

    public void Use(string name)
    {
        Entity session = Session;
        int index = session.IsNull ? -1 : AnimatorEffectCommands.Selected(session);
        if (index < 0)
            return;

        AnimatorHistoryCommands.Edit(session, () =>
        {
            ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
            document.Package.Effects[index].Particle = name;
            document.EffectsVersion++;
        });
    }
}
