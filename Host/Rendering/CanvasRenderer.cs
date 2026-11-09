using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Godot;
using MoonWorks.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = Godot.Color;
using Rect = MoonWorks.Graphics.Rect;
using Texture = MoonWorks.Graphics.Texture;
using Shader = MoonWorks.Graphics.Shader;

namespace Xanadu.Host.Rendering;

/// <summary>
/// Draws a <see cref="CanvasTree"/>: walks it once a frame in Godot's order,
/// puts every visible item's triangles into one vertex buffer in pixel space,
/// and draws them in as few calls as the textures and clip rects allow.
/// </summary>
internal sealed class CanvasRenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ViewUniforms { public float Width, Height, Pad0, Pad1; }

    private readonly record struct Entry(int Z, int Sequence, CanvasItem Item, Transform2D Transform, Color Color, Rect Clip);

    private readonly record struct Batch(uint Start, uint Count, ulong Texture, Rect Clip);

    private readonly GraphicsDevice _device;
    private readonly CanvasTree _tree;
    private readonly GraphicsPipeline _pipeline;
    private readonly Texture _white;
    private readonly Sampler _sampler;
    private readonly uint _width, _height;

    private readonly List<Entry> _entries = new();
    private readonly List<Batch> _batches = new();
    private CanvasVertex[] _vertices = new CanvasVertex[1 << 16];
    private int _vertexCount;
    private Buffer? _buffer;
    private TransferBuffer? _transfer;
    private uint _capacity;

    /// <summary>Textures the game can draw by id, minted from the tree so they never clash with items.</summary>
    public Dictionary<ulong, Texture> Textures { get; } = new();

    /// <summary>Draw calls in the last frame, for the debug overlay and the step's sanity check.</summary>
    public int LastBatchCount => _batches.Count;

    public CanvasRenderer(GraphicsDevice device, CanvasTree tree, TextureFormat format, uint width, uint height)
    {
        _device = device;
        _tree = tree;
        _width = width;
        _height = height;

        string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "canvas.slang");
        using Shader vertex = Shader.Create(device, SlangShaders.Compile(module, "item", "vertex"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex, NumUniformBuffers = 1 });
        using Shader fragment = Shader.Create(device, SlangShaders.Compile(module, "shade", "fragment"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Fragment, NumSamplers = 1 });

        _pipeline = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            VertexShader = vertex,
            FragmentShader = fragment,
            VertexInputState = VertexInputState.CreateSingleBinding<CanvasVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    // Godot's "mix": colour by source alpha, alpha accumulated as coverage.
                    new ColorTargetDescription
                    {
                        Format = format,
                        BlendState = new ColorTargetBlendState
                        {
                            EnableBlend = true,
                            ColorBlendOp = BlendOp.Add,
                            AlphaBlendOp = BlendOp.Add,
                            SrcColorBlendFactor = BlendFactor.SrcAlpha,
                            DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                            SrcAlphaBlendFactor = BlendFactor.One,
                            DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                        },
                    },
                ],
            },
        });

        var uploader = new ResourceUploader(device);
        _white = uploader.CreateTexture2D<uint>("white", [0xFFFFFFFFu], TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler, 1, 1);
        uploader.UploadAndWait();
        uploader.Dispose();
        _sampler = Sampler.Create(device, SamplerCreateInfo.LinearClamp);
    }

    /// <summary>Turns the tree into this frame's vertices and batches, and copies them to the GPU.</summary>
    public void Prepare(CommandBuffer commands)
    {
        Collect();
        Build();
        if (_vertexCount == 0)
            return;

        EnsureCapacity((uint)_vertexCount);
        Span<CanvasVertex> mapped = _transfer!.Map<CanvasVertex>(cycle: true);
        _vertices.AsSpan(0, _vertexCount).CopyTo(mapped);
        _transfer.Unmap();

        CopyPass copy = commands.BeginCopyPass();
        copy.UploadToBuffer<CanvasVertex>(_transfer, _buffer!, 0, 0, (uint)_vertexCount, cycle: true);
        commands.EndCopyPass(copy);
    }

    /// <summary>Draws what <see cref="Prepare"/> built into the open pass.</summary>
    public void Draw(CommandBuffer commands, RenderPass pass)
    {
        if (_vertexCount == 0)
            return;

        commands.PushVertexUniformData(new ViewUniforms { Width = _width, Height = _height });
        pass.BindGraphicsPipeline(_pipeline);
        pass.BindVertexBuffers(_buffer!);
        foreach (Batch batch in _batches)
        {
            Texture texture = Textures.GetValueOrDefault(batch.Texture) ?? _white;
            pass.SetScissor(batch.Clip);
            pass.BindFragmentSamplers(new TextureSamplerBinding(texture, _sampler));
            pass.DrawPrimitives(batch.Count, 1, batch.Start, 0);
        }
    }

    private Rect FullClip => new() { X = 0, Y = 0, W = (int)_width, H = (int)_height };

    private void Collect()
    {
        _entries.Clear();
        int sequence = 0;
        Visit(_tree.Root, Transform2D.Identity, Colors.White, 0, FullClip, ref sequence);
        _entries.Sort(static (a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.Sequence.CompareTo(b.Sequence));
    }

    private void Visit(CanvasItem item, Transform2D parent, Color parentModulate, int parentZ, Rect clip, ref int sequence)
    {
        if (!item.Visible)
            return;

        Transform2D transform = parent * item.Transform;
        Color modulate = parentModulate * item.Modulate;
        int z = Math.Clamp(parentZ + item.Z, -4096, 4096);
        if (item.Clip && item.UseCustomRect)
            clip = Intersect(clip, Bounds(transform, item.CustomRect));

        if (item.Vertices.Count > 0 && modulate.A > 0f && clip.W > 0 && clip.H > 0)
            _entries.Add(new Entry(z, sequence++, item, transform, modulate * item.SelfModulate, clip));

        item.SortChildren();
        foreach (CanvasItem child in item.Children)
            Visit(child, transform, modulate, z, clip, ref sequence);
    }

    private void Build()
    {
        _batches.Clear();
        _vertexCount = 0;
        foreach (Entry entry in _entries)
        {
            CanvasItem item = entry.Item;
            List<(int Start, ulong Texture)> runs = item.Runs;
            for (int r = 0; r < runs.Count; r++)
            {
                int start = runs[r].Start;
                int end = r + 1 < runs.Count ? runs[r + 1].Start : item.Vertices.Count;
                AddRun(item, start, end, entry, runs[r].Texture);
            }
        }
    }

    private void AddRun(CanvasItem item, int start, int end, in Entry entry, ulong texture)
    {
        int count = end - start;
        if (count <= 0)
            return;

        if (_vertexCount + count > _vertices.Length)
            Array.Resize(ref _vertices, Math.Max(_vertices.Length * 2, _vertexCount + count));

        Transform2D t = entry.Transform;
        Color tint = entry.Color;
        Span<CanvasVertex> source = CollectionsMarshal.AsSpan(item.Vertices).Slice(start, count);
        Span<CanvasVertex> target = _vertices.AsSpan(_vertexCount, count);
        for (int i = 0; i < count; i++)
        {
            ref readonly CanvasVertex v = ref source[i];
            target[i] = new CanvasVertex(
                new Vector2(
                    t.X.X * v.X + t.Y.X * v.Y + t.Origin.X,
                    t.X.Y * v.X + t.Y.Y * v.Y + t.Origin.Y),
                new Vector2(v.U, v.V),
                new Color(v.R * tint.R, v.G * tint.G, v.B * tint.B, v.A * tint.A));
        }

        if (_batches.Count > 0 && _batches[^1] is { } last &&
            last.Texture == texture && SameRect(last.Clip, entry.Clip) &&
            last.Start + last.Count == (uint)_vertexCount)
            _batches[^1] = last with { Count = last.Count + (uint)count };
        else
            _batches.Add(new Batch((uint)_vertexCount, (uint)count, texture, entry.Clip));

        _vertexCount += count;
    }

    private void EnsureCapacity(uint count)
    {
        if (_buffer is not null && count <= _capacity)
            return;

        _buffer?.Dispose();
        _transfer?.Dispose();
        _capacity = Math.Max(count, Math.Max(_capacity * 2, 1u << 16));
        _buffer = Buffer.Create<CanvasVertex>(_device, "canvas vertices", BufferUsageFlags.Vertex, _capacity);
        _transfer = TransferBuffer.Create<CanvasVertex>(_device, "canvas upload", TransferBufferUsage.Upload, _capacity);
    }

    private static bool SameRect(Rect a, Rect b) => a.X == b.X && a.Y == b.Y && a.W == b.W && a.H == b.H;

    private static Rect Bounds(Transform2D t, Rect2 rect)
    {
        Vector2 a = t * rect.Position;
        Vector2 b = t * new Vector2(rect.End.X, rect.Position.Y);
        Vector2 c = t * rect.End;
        Vector2 d = t * new Vector2(rect.Position.X, rect.End.Y);
        float minX = MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X));
        float minY = MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y));
        float maxX = MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X));
        float maxY = MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y));
        int x = (int)MathF.Floor(minX), y = (int)MathF.Floor(minY);
        return new Rect { X = x, Y = y, W = (int)MathF.Ceiling(maxX) - x, H = (int)MathF.Ceiling(maxY) - y };
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        int x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.X + a.W, b.X + b.W), bottom = Math.Min(a.Y + a.H, b.Y + b.H);
        return new Rect { X = x, Y = y, W = Math.Max(0, right - x), H = Math.Max(0, bottom - y) };
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        _white.Dispose();
        _sampler.Dispose();
        _buffer?.Dispose();
        _transfer?.Dispose();
    }
}
