using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Core.Particles;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// Owns the shared particle editor in the animator: it shows while the
/// selected part streams particles and edits that part's own settings (its
/// node's <see cref="XifParticles"/>), each change a step on the animator's
/// undo history. The preview's streams play the settings in place, so edits
/// show at once.
/// </summary>
public sealed class AnimatorParticles(EntityStore store)
{
    /// <summary>Builds the editor (see <see cref="ParticleEditorPanel.Build"/>) and has a new owner answer it for the animator in <paramref name="store"/>.</summary>
    public static Entity Build(UiNode layer, Vector2 home, EntityStore store)
    {
        Entity panel = ParticleEditorPanel.Build(layer, home);
        var owner = new AnimatorParticles(store);
        panel.AddSignalHandler<ParticleEditorFrameDue>(signal =>
        {
            ref ParticleEditorFrame frame = ref signal.Entity.GetComponent<ParticleEditorFrame>();
            RigAttachment? effect = owner.Effect;
            frame.Particle = effect?.Particles;
            frame.Title = effect is null ? "" : $"{effect.Id}  particles";
        });
        panel.AddSignalHandler<ParticleUseAsked>(signal => owner.Use(signal.Event.Settings));
        panel.AddSignalHandler<EditAsked>(signal => owner.Edit(signal.Event.Change));
        panel.AddSignalHandler<EditGestureStarted>(_ => AnimatorHistoryCommands.BeginGesture(owner.Session));
        // The streams play the settings in place, so a change inside a gesture has nothing to rebuild.
        panel.AddSignalHandler<EditGestureEnded>(_ => AnimatorHistoryCommands.EndGesture(owner.Session));
        return panel;
    }

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

    private void Use(XpfParticle settings)
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

    private void Edit(Action change)
    {
        Entity session = Session;
        if (!session.IsNull)
            AnimatorHistoryCommands.Edit(session, change);
    }
}
