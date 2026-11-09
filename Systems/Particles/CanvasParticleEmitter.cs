using System;
using Godot;
using Xanadu.Particles;
using Xanadu.Systems.Shaders;

namespace Xanadu.Systems.Particles;

/// <summary>
/// Plays one-off bursts of the library's particles through the host's
/// <see cref="IParticleServer"/>: each burst becomes seeds that live in one
/// canvas item, so bursts overlap without cutting each other off.
/// </summary>
public sealed class CanvasParticleEmitter(Rid canvas, IParticleServer server) : IParticleEmitter
{
    private readonly Random _random = new();
    private readonly ParticleSeed[] _seeds = new ParticleSeed[XpfParticle.MaxAmount];

    public void Burst(
        string particle,
        Vector2 position,
        Color tint,
        Vector2 area = default,
        float angle = 0f,
        Vector2 heading = default)
    {
        if (ParticleLibrary.Find(particle) is not { } effect)
            return;

        bool notes = effect.Texture == XpfTexture.NoteGlyphs;
        int cells = notes ? NoteAtlas.Particles.Count : 1;
        int count = ParticleSpawn.Burst(effect, position, tint, area, angle, heading, cells, _random, _seeds);
        server.Emit(canvas, effect, _seeds.AsSpan(0, count), notes ? NoteAtlas.Particles.TextureId : default, cells);
    }
}
