using System.Collections.Generic;
using Godot;
using Xanadu.Particles;
using Xanadu.Systems.Particles;

namespace Xanadu.Animation;

/// <summary>
/// The effects that stream a particle effect: one trail per effect, placed on
/// its bone each frame and flowing while the pose shows the effect. The
/// trails are remade when the particle library changes, so edits in the
/// particle editor show at once.
/// </summary>
public sealed partial class SvgAttachmentVisuals
{
    private readonly List<ParticleEffect> _particles = new();
    private XafPackage? _particlePackage;
    private int _particleVersion = -1;

    /// <summary>Whether this set of visuals streams particles at all; afterimages do not.</summary>
    public bool Particles { get; init; } = true;

    private void ReloadParticles(XafPackage package)
    {
        ReleaseParticles();
        _particlePackage = package;
        _particleVersion = ParticleLibrary.Version;
        if (!Particles)
            return;

        for (int i = 0; i < package.Effects.Count; i++)
        {
            XafEffect effect = package.Effects[i];
            if (effect.Particle.Length == 0 || ParticleLibrary.Find(effect.Particle) is not { } particle)
                continue;

            IParticleFlow instance = Xanadu.Systems.HostPlatform.Particles is { } server
                ? new ParticleStream(server, canvas, particle)
                : new ParticleInstance(ParticleKit.Shared, canvas, particle, trail: true);
            SetLayer(instance.Item, effect.ZOrder, AnimatorDrawOrder.Effect(i, effect.Item.Primitives.Count));
            _particles.Add(new ParticleEffect(i, instance));
        }
    }

    private void ApplyParticles(XafPose pose, Transform2D[] effects, Transform2D placement)
    {
        if (_particlePackage is { } package && _particleVersion != ParticleLibrary.Version)
            ReloadParticles(package);

        foreach (ParticleEffect effect in _particles)
        {
            bool live = effect.Index < pose.Effects.Length && effect.Index < effects.Length;
            float alpha = live ? pose.Effects[effect.Index].Alpha : 0f;
            if (live)
                effect.Instance.Place(placement * effects[effect.Index], new Color(1f, 1f, 1f, alpha));
            effect.Instance.Stream(alpha > 0f);
        }
    }

    private void ReleaseParticles()
    {
        foreach (ParticleEffect effect in _particles)
            effect.Instance.Dispose();
        _particles.Clear();
    }

    private readonly record struct ParticleEffect(int Index, IParticleFlow Instance);
}
