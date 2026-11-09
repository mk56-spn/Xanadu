using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xanadu.Particles;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Every effect in the particle library going off once a second, side by
/// side, through the host's GPU particles (docs/moonworks-port.md step 7).
/// Each column sets off one effect, in the order the panel lists them.
/// </summary>
internal sealed class ParticleBursts : ILabExperiment
{
    public string Name => "PARTICLES";

    public string[] Notes =>
    [
        "Each library effect bursts once a second,",
        "left to right in this order:",
        .. Effects().Select(effect => "  " + effect.Name),
    ];

    private static List<XpfParticle> Effects() => ParticleLibrary.Shared.Particles;

    public ILabStage CreateStage(LabContext context, Rid parent, Rect2 area) => new Stage(parent, area);

    private sealed class Stage : ILabStage
    {
        private const float Every = 1f;

        private static readonly GlyphAtlas Notes = new(["♪", "♫"], cellSize: 64, mipmaps: true);

        private readonly Rid _item;
        private readonly Rect2 _area;
        private readonly Random _random = new(7);
        private readonly ParticleSeed[] _seeds = new ParticleSeed[XpfParticle.MaxAmount];
        private float _next;

        public Stage(Rid parent, Rect2 area)
        {
            _area = area;
            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
        }

        public void Update(in LabFrame frame)
        {
            if (frame.Time < _next || HostPlatform.Particles is not { } server)
                return;

            _next = frame.Time + Every;
            List<XpfParticle> effects = Effects();
            for (int i = 0; i < effects.Count; i++)
            {
                XpfParticle effect = effects[i];
                float x = _area.Position.X + _area.Size.X * (i + 0.5f) / effects.Count;
                Vector2 at = new(x, _area.Position.Y + _area.Size.Y * 0.6f);
                bool notes = effect.Texture == XpfTexture.NoteGlyphs;
                int count = ParticleSpawn.Burst(effect, at, Colors.White, new Vector2(120, 40), 0f, default,
                    Notes.Count, _random, _seeds);
                server.Emit(_item, effect, _seeds.AsSpan(0, count), notes ? Notes.TextureId : default, Notes.Count);
            }
        }

        public void Dispose() => CanvasServer.Current.Free(_item);
    }
}
