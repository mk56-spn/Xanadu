using System.Collections.Generic;
using Xanadu.Core.Items;
using Xanadu.Core.Particles;
using Xanadu.Model.Particles;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Model.Animation;

/// <summary>
/// The parts that stream particles: one stream per part with a
/// <see cref="XifParticles"/> module, playing the node's own settings, placed
/// on its bone each frame and flowing while the pose shows the part. The host's
/// particle server simulates them on the GPU; edits to the settings in place
/// show at once (<see cref="XpfParticle.Version"/>). They are mounted over
/// <paramref name="canvas"/>, except the part an editor holds, whose stream is
/// mounted over <paramref name="held"/> so it draws with the held part.
/// </summary>
/// <remarks>
/// A particle node with geometry gives every particle its look: the geometry,
/// through the node's shader, drawn once per particle on the GPU
/// (<see cref="ICanvasServer.SetParticleMesh"/>), at its own size when the
/// particle's scale is 1. Without geometry the particles are soft dots.
/// </remarks>
public sealed class RigParticleStreams(Handle canvas, Handle held = default) : ICanvasMount
{
    private readonly List<Stream> _streams = [];
    private readonly HeldMount _heldMount = new();
    private readonly XifDrawer _drawer = new();
    private readonly ServerRectCanvas _looks = new();
    private bool _mounted;
    private int _held = -1;

    private sealed class Stream(int part, IParticleFlow flow, int layer, Xif node)
    {
        public readonly int Part = part;
        public readonly IParticleFlow Flow = flow;
        public readonly int Layer = layer;
        public readonly Xif Node = node;

        /// <summary>The rig's zoom the look was last drawn at; NaN until drawn.</summary>
        public float LookScale = float.NaN;
    }

    public void Reload(RigPackage package)
    {
        Release();
        _drawer.Forget();
        for (int i = 0; i < package.Attachments.Count; i++)
        {
            RigAttachment part = package.Attachments[i];
            // Without the host's particles (tests) rig parts have nothing to stream into.
            if (part.Particles is not { } particle || HostPlatform.Particles is not { } server)
                continue;

            if (!_mounted)
            {
                CanvasServer.Current.Mount(canvas, this);
                if (held.IsValid)
                    CanvasServer.Current.Mount(held, _heldMount);
                _mounted = true;
            }
            _streams.Add(new Stream(i, new ParticleStream(server, default, particle), part.ZOrder, part.Node));
        }
        _heldMount.Streams = _streams;
    }

    /// <summary>
    /// Places each stream on its part and lets it flow while the part shows.
    /// <paramref name="heldPart"/> is the part an editor holds (its stream draws
    /// over the held canvas), or -1.
    /// </summary>
    public void Apply(RigPose pose, Transform2D[] attachments, Transform2D placement, int heldPart = -1)
    {
        _held = held.IsValid ? heldPart : -1;
        _heldMount.Part = _held;
        float scale = placement.X.Length();
        foreach (Stream stream in _streams)
        {
            if (!(Math.Abs(stream.LookScale - scale) <= 0.0001f))
                DrawLook(stream, scale);

            bool live = stream.Part < pose.AttachmentStates.Length && stream.Part < attachments.Length;
            float alpha = live ? pose.AttachmentStates[stream.Part].Alpha : 0f;
            if (live)
                stream.Flow.Place(placement * attachments[stream.Part], new Color(1f, 1f, 1f, alpha));
            stream.Flow.Stream(alpha > 0f);
        }
    }

    /// <summary>
    /// Draws the node's geometry into the stream's item as its particles' look,
    /// sized so a particle of scale 1 is as big as the geometry on the rig at
    /// <paramref name="scale"/>; with no geometry the particles stay dots.
    /// </summary>
    private void DrawLook(Stream stream, float scale)
    {
        stream.LookScale = scale;
        Handle item = stream.Flow.Item;
        _looks.Clear(item);
        bool shaped = stream.Node.Get<XifGeometry>() is { } geometry && _drawer.MeshOf(geometry).Triangles.Length > 0;
        CanvasServer.Current.SetParticleMesh(item, shaped);
        if (!shaped)
            return;

        // The GPU sizes each copy by half the particle's size (ParticleSpawn.TextureSize at scale 1).
        float k = scale / (0.5f * ParticleSpawn.TextureSize);
        _drawer.DrawNode(_looks, item, stream.Node, new Transform2D(new Vector2(k, 0f), new Vector2(0f, k), Vector2.Zero), scale, XifTint.Plain);
    }

    public void Collect(ICanvasCollector into)
    {
        foreach (Stream stream in _streams)
        {
            if (stream.Part != _held)
                into.Item(stream.Flow.Item, Transform2D.Identity, Colors.White, stream.Layer, null);
        }
    }

    public void Release()
    {
        foreach (Stream stream in _streams)
            stream.Flow.Dispose();
        _streams.Clear();
    }

    /// <summary>Lists only the held part's stream, over the held canvas.</summary>
    private sealed class HeldMount : ICanvasMount
    {
        public List<Stream> Streams = [];
        public int Part = -1;

        public void Collect(ICanvasCollector into)
        {
            foreach (Stream stream in Streams)
            {
                if (stream.Part == Part)
                    into.Item(stream.Flow.Item, Transform2D.Identity, Colors.White, stream.Layer, null);
            }
        }
    }
}
