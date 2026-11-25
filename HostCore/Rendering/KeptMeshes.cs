using System.Collections.Generic;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using MoonWorks.Graphics;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Xanadu.Host.Rendering;

/// <summary>A kept effect range's vertices: on the CPU (a trace reads its corners) and on the GPU, in a piece per texture.</summary>
internal sealed class KeptVertices(GraphicsDevice device)
{
    public readonly List<CanvasVertex> Vertices = [];
    public readonly List<(uint Start, uint Count, ulong Texture)> Pieces = [];
    public readonly GpuArray<CanvasVertex> Gpu = new(device, "kept effect vertices", BufferUsageFlags.Vertex, 1 << 10);
}

/// <summary>The item and first primitive of the effect range an entity keeps the vertices of.</summary>
internal struct MeshOf(CanvasItem item, int from) : IIndexedComponent<(CanvasItem Item, int From)>
{
    public (CanvasItem Item, int From) Range = (item, from);

    public readonly (CanvasItem Item, int From) GetIndexedValue() => Range;
}

/// <summary>A kept range's vertices and what they were cut from, so they are cut again only when that changes.</summary>
internal struct KeptMesh(KeptVertices mesh) : IComponent
{
    public KeptVertices Mesh = mesh;
    public int Hash;
    public Transform2D Transform;
    public Color Color;

    /// <summary>Cut again since last sent.</summary>
    public bool Pending;

    /// <summary>The frame it was last drawn; one undrawn for <see cref="KeptMeshes.KeptFrames"/> is let go.</summary>
    public long LastFrame;
}

/// <summary>
/// Effect ranges' vertices, kept on the GPU between frames: cut again only when the item's
/// primitives, its place or its tint change. Shaded items mostly look the same frame after frame
/// (a [Grid] of 96 × 54 alone costs milliseconds to cut), so none is cut every frame for nothing.
/// </summary>
internal sealed class KeptMeshes(GraphicsDevice device, EntityStore store) : IDisposable
{
    /// <summary>Frames a kept mesh may go undrawn before its buffer is let go.</summary>
    public const int KeptFrames = 120;

    private readonly ArchetypeQuery<KeptMesh> _meshes = store.Query<KeptMesh>();
    private readonly GpuStaging<CanvasVertex> _staging = new(device, "kept effect upload");

    /// <summary>The range's vertices for this frame, cut again only when what they come from changed.</summary>
    public KeptVertices Cut(CanvasItem item, Transform2D transform, Color color, int from, int to, int columns, int rows, long frame)
    {
        var hash = new HashCode();
        hash.AddBytes(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(item.Primitives).Slice(from, to - from)));
        hash.Add(columns);
        hash.Add(rows);
        int content = hash.ToHashCode();

        Entities found = store.ComponentIndex<MeshOf, (CanvasItem, int)>()[(item, from)];
        Entity entity = found.Count > 0
            ? found[0]
            : store.CreateEntity(new MeshOf(item, from), new KeptMesh(new KeptVertices(device)));
        ref KeptMesh kept = ref entity.GetComponent<KeptMesh>();
        kept.LastFrame = frame;
        KeptVertices mesh = kept.Mesh;
        if (mesh.Gpu.Buffer is not null && kept.Hash == content && kept.Transform == transform && kept.Color == color)
            return mesh;

        kept.Hash = content;
        kept.Transform = transform;
        kept.Color = color;
        kept.Pending = true;
        mesh.Vertices.Clear();
        mesh.Pieces.Clear();

        int points = 0, start = 0;
        ulong texture = item.Primitives[from].Texture;
        for (int i = from; i < to; i++)
        {
            Primitive p = item.Primitives[i];
            if (p.Texture != texture && mesh.Vertices.Count > start)
            {
                mesh.Pieces.Add(((uint)start, (uint)(mesh.Vertices.Count - start), texture));
                start = mesh.Vertices.Count;
            }

            texture = p.Texture;
            p.Tessellate(mesh.Vertices, transform, color, ref points, columns, rows);
        }

        if (mesh.Vertices.Count > start)
            mesh.Pieces.Add(((uint)start, (uint)(mesh.Vertices.Count - start), texture));
        return mesh;
    }

    /// <summary>Sends the meshes cut again this frame, and lets go of those no longer drawn.</summary>
    public void Upload(CommandBuffer commands, long frame)
    {
        Friflo.Engine.ECS.CommandBuffer deletes = store.GetCommandBuffer();
        SendOrDrop each = _meshes.EachEntity(new SendOrDrop(commands, _staging, deletes, frame));
        if (each.Copy is { } copy)
            commands.EndCopyPass(copy);
        deletes.Playback();
    }

    public void Dispose()
    {
        _meshes.Each(new DisposeMeshes());
        _staging.Dispose();
    }

    private struct SendOrDrop(CommandBuffer commands, GpuStaging<CanvasVertex> staging, Friflo.Engine.ECS.CommandBuffer deletes, long frame)
        : IEachEntity<KeptMesh>
    {
        public CopyPass? Copy;

        public void Execute(ref KeptMesh kept, int id)
        {
            if (frame - kept.LastFrame > KeptFrames)
            {
                kept.Mesh.Gpu.Dispose();
                deletes.DeleteEntity(id);
                return;
            }

            if (!kept.Pending || kept.Mesh.Vertices.Count == 0)
                return;

            kept.Pending = false;
            Copy ??= commands.BeginCopyPass();
            kept.Mesh.Gpu.Upload(Copy, CollectionsMarshal.AsSpan(kept.Mesh.Vertices), staging);
        }
    }

    private struct DisposeMeshes : IEach<KeptMesh>
    {
        public void Execute(ref KeptMesh kept) => kept.Mesh.Gpu.Dispose();
    }
}
