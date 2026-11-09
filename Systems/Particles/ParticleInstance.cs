using System;
using Godot;
using Xanadu.Particles;

namespace Xanadu.Systems.Particles;

/// <summary>
/// One running particle system drawn straight through the RenderingServer, set
/// up the way GPUParticles2D does it: a particles RID in 2D mode, a quad mesh
/// as its draw pass, a process material, and the particles added to a canvas
/// item that sits where they go off. It plays an <see cref="XpfParticle"/>
/// either once (<see cref="Burst"/>) or as a flow (<see cref="Stream"/>).
/// </summary>
public sealed class ParticleInstance : IDisposable
{
    private readonly ParticleKit _kit;
    private readonly Rid _particles;
    private readonly Rid _item;
    private XpfParticle _particle;
    private ParticleProcessMaterial _material;
    private readonly bool _trail;
    private bool _streaming;

    /// <param name="trail">
    /// Leave particles where they were born as the emitter moves, as sparks
    /// behind a sliding foot do; otherwise they move with the emitter, as a
    /// burst set off at one spot can.
    /// </param>
    public ParticleInstance(ParticleKit kit, Rid parentCanvas, XpfParticle particle, bool trail = false)
    {
        _kit = kit;
        _particle = particle;
        _trail = trail;
        _particles = RenderingServer.ParticlesCreate();
        RenderingServer.ParticlesSetMode(_particles, RenderingServer.ParticlesMode.Mode2D);
        RenderingServer.ParticlesSetDrawPasses(_particles, 1);
        RenderingServer.ParticlesSetUseLocalCoordinates(_particles, !trail);
        RenderingServer.ParticlesSetFixedFps(_particles, 60);
        RenderingServer.ParticlesSetFractionalDelta(_particles, true);

        _item = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_item, parentCanvas);
        _material = XpfMaterials.Build(particle);
        Apply();
    }

    public XpfParticle Particle => _particle;

    /// <summary>The canvas item the particles draw in, for a host that orders it among its own.</summary>
    public Rid Item => _item;

    /// <summary>Replaces the effect, as when it is edited; a stream keeps flowing with the new one.</summary>
    public void Configure(XpfParticle particle)
    {
        _particle = particle;
        _material.Dispose();
        _material = XpfMaterials.Build(particle);
        Apply();
    }

    /// <summary>Goes off once at a screen position, cutting off whatever this instance was playing.</summary>
    public void Burst(Vector2 position, Color tint, Vector2 area = default, float angle = 0f, Vector2 heading = default)
    {
        _streaming = false;
        RenderingServer.ParticlesSetOneShot(_particles, true);
        Place(position, tint, area, angle, heading);
        RenderingServer.ParticlesRestart(_particles);
        RenderingServer.ParticlesSetEmitting(_particles, true);
    }

    /// <summary>
    /// Flows while <paramref name="on"/>, from wherever it was last placed.
    /// Switching off lets the particles already out live their lives.
    /// </summary>
    public void Stream(bool on)
    {
        if (on == _streaming)
            return;

        _streaming = on;
        RenderingServer.ParticlesSetOneShot(_particles, false);
        RenderingServer.ParticlesSetEmitting(_particles, on);
    }

    /// <summary>Moves, turns and tints the emitter, for a stream that follows something.</summary>
    public void Place(Vector2 position, Color tint, Vector2 area = default, float angle = 0f, Vector2 heading = default)
    {
        Place(new Transform2D(angle, position), tint, area, heading);
    }

    /// <summary>
    /// Places the emitter by a whole transform, turn and scale included, as a
    /// bone carries it. A trail is born in the parent's space, so its
    /// particles stay put as the emitter moves on.
    /// </summary>
    public void Place(Transform2D transform, Color tint, Vector2 area = default, Vector2 heading = default)
    {
        XpfMaterials.Aim(_material, _particle, area, transform.Rotation, heading);
        RenderingServer.CanvasItemSetTransform(_item, transform);
        RenderingServer.CanvasItemSetModulate(_item, tint);
        if (_trail)
            RenderingServer.ParticlesSetEmissionTransform(_particles, Emission(transform));
    }

    /// <summary>The 3D transform the particle server takes for a 2D one.</summary>
    private static Transform3D Emission(Transform2D transform) =>
        new(
            new Basis(
                new Vector3(transform.X.X, transform.X.Y, 0),
                new Vector3(transform.Y.X, transform.Y.Y, 0),
                new Vector3(0, 0, 1)),
            new Vector3(transform.Origin.X, transform.Origin.Y, 0));

    /// <summary>Draws above or below other items under the same parent.</summary>
    public void SetZIndex(int z) => RenderingServer.CanvasItemSetZIndex(_item, z);

    public void Dispose()
    {
        RenderingServer.FreeRid(_item);
        RenderingServer.FreeRid(_particles);
        _material.Dispose();
    }

    private void Apply()
    {
        XpfParticle particle = _particle;
        RenderingServer.ParticlesSetAmount(_particles, particle.Amount);
        RenderingServer.ParticlesSetLifetime(_particles, particle.Lifetime);
        RenderingServer.ParticlesSetExplosivenessRatio(_particles, particle.Explosiveness);
        RenderingServer.ParticlesSetRandomnessRatio(_particles, particle.Randomness);
        RenderingServer.ParticlesSetDrawPassMesh(_particles, 0, _kit.Mesh(particle.Texture));
        RenderingServer.ParticlesSetProcessMaterial(_particles, _material.GetRid());

        // The server culls by this box, around the emitter.
        float reach = particle.Reach;
        RenderingServer.ParticlesSetCustomAabb(
            _particles,
            new Aabb(new Vector3(-reach, -reach, -1), new Vector3(reach * 2, reach * 2, 2)));

        // Not emitting until played: a one-shot system starts out running.
        RenderingServer.ParticlesSetOneShot(_particles, !_streaming);
        RenderingServer.ParticlesSetEmitting(_particles, _streaming);

        RenderingServer.CanvasItemClear(_item);
        RenderingServer.CanvasItemAddParticles(_item, _particles, _kit.Texture(particle.Texture).GetRid());
        CanvasItemMaterial? frames = _kit.Frames(particle.Texture);
        RenderingServer.CanvasItemSetMaterial(_item, frames?.GetRid() ?? default);
        RenderingServer.CanvasItemSetDefaultTextureFilter(
            _item,
            frames is null
                ? RenderingServer.CanvasItemTextureFilter.Default
                : RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
    }
}
