using System;
using System.Diagnostics;
using Godot;
using Xanadu.Particles;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Shaders;

namespace Xanadu.Systems.Particles;

/// <summary>A particle effect that follows something and flows while it is on, as a rig effect does.</summary>
public interface IParticleFlow : IDisposable
{
    /// <summary>The canvas item the particles draw in, for a host that orders it among its own.</summary>
    Rid Item { get; }

    /// <summary>Places the emitter by a whole transform and tints what it throws from now on.</summary>
    void Place(Transform2D transform, Color tint, Vector2 area = default, Vector2 heading = default);

    /// <summary>Flows while <paramref name="on"/>; switching off lets the particles already out live their lives.</summary>
    void Stream(bool on);
}

/// <summary>
/// A flowing effect through the host's <see cref="IParticleServer"/>: while
/// on, each <see cref="Place"/> throws however many particles came due since
/// the last, born where the emitter is in its parent's space, so they stay
/// behind as it moves on (a trail).
/// </summary>
public sealed class ParticleStream : IParticleFlow
{
    private const float MaxStep = 0.1f;

    private readonly IParticleServer _server;
    private readonly XpfParticle _effect;
    private readonly Random _random = new();
    private readonly ParticleSeed[] _seeds = new ParticleSeed[XpfParticle.MaxAmount];
    private readonly Stopwatch _clock = new();
    private bool _on;
    private float _owed;

    public ParticleStream(IParticleServer server, Rid parent, XpfParticle effect)
    {
        _server = server;
        _effect = effect;
        Item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(Item, parent);
    }

    public Rid Item { get; }

    public void Place(Transform2D transform, Color tint, Vector2 area = default, Vector2 heading = default)
    {
        if (!_on)
            return;

        float step = Math.Min(MaxStep, (float)_clock.Elapsed.TotalSeconds);
        _clock.Restart();
        _owed += step * ParticleSpawn.StreamRate(_effect);
        int count = (int)_owed;
        if (count == 0)
            return;

        _owed -= count;
        bool notes = _effect.Texture == XpfTexture.NoteGlyphs;
        int cells = notes ? NoteAtlas.Particles.Count : 1;
        int made = ParticleSpawn.Stream(_effect, count, transform.Origin, tint, transform.Rotation, cells, _random, _seeds);
        _server.Emit(Item, _effect, _seeds.AsSpan(0, made), notes ? NoteAtlas.Particles.TextureId : default, cells);
    }

    public void Stream(bool on)
    {
        if (on == _on)
            return;

        _on = on;
        _owed = 0f;
        _clock.Restart();
    }

    public void Dispose() => CanvasServer.Current.Free(Item);
}
