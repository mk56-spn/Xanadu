using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using MoonWorks.Graphics;
using Xanadu.Rendering.Gpu;
using Xanadu.Ui.Systems.Canvas;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Xanadu.Rendering.Kept;

/// <summary>The mesh whose copies an entity keeps the vertices of.</summary>
internal struct CopiedMeshOf(CanvasMesh mesh) : IIndexedComponent<CanvasMesh>
{
    public CanvasMesh Mesh = mesh;

    public readonly CanvasMesh GetIndexedValue() => Mesh;
}

/// <summary>A copied mesh's vertices in its own space, the version they were cut from, and when it was last drawn.</summary>
internal struct CopiedMesh(KeptVertices vertices) : IComponent
{
    public KeptVertices Vertices = vertices;
    public int Version;
    public bool Pending;
    public long LastFrame;
}

/// <summary>
/// The meshes copies are drawn of (<see cref="CanvasMesh"/>), kept on the GPU as canvas vertices in their own space:
/// cut when first drawn and again only when the mesh is edited; one undrawn for <see cref="KeptMeshes.KeptFrames"/> is let go.
/// </summary>
internal sealed class CopiedMeshes(GraphicsDevice device, EntityStore store) : IDisposable
{
    private readonly ArchetypeQuery<CopiedMesh> _meshes = store.Query<CopiedMesh>();
    private readonly GpuStaging<CanvasVertex> _staging = new(device, "copied mesh upload");

    /// <summary>The mesh's vertices for this frame, cut again if it was edited since.</summary>
    public KeptVertices Of(CanvasMesh mesh, long frame)
    {
        Entities found = store.ComponentIndex<CopiedMeshOf, CanvasMesh>()[mesh];
        bool made = found.Count == 0;
        Entity entity = made ? store.CreateEntity(new CopiedMeshOf(mesh), new CopiedMesh(new KeptVertices(device))) : found[0];
        ref CopiedMesh kept = ref entity.GetComponent<CopiedMesh>();
        kept.LastFrame = frame;
        if (!made && kept.Version == mesh.Version)
            return kept.Vertices;

        kept.Version = mesh.Version;
        kept.Pending = true;
        Cut(mesh, kept.Vertices);
        return kept.Vertices;
    }

    /// <summary>Every triangle as three canvas vertices, its rest quad from how its uv spans it; a bulging curve's is dropped (its chord stays).</summary>
    private static void Cut(CanvasMesh mesh, KeptVertices into)
    {
        into.Vertices.Clear();
        into.Pieces.Clear();
        int corners = Math.Min(mesh.Points.Length, Math.Min(mesh.Colors.Length, mesh.Uvs.Length)) / 3 * 3;
        for (int i = 0; i < corners; i += 3)
        {
            byte curve = mesh.Curves is { } curves && i / 3 < curves.Length ? curves[i / 3] : CanvasCurves.None;
            if (curve != CanvasCurves.None && curve != CanvasCurves.Concave)
                continue;

            Vector2 a = mesh.Points[i], b = mesh.Points[i + 1], c = mesh.Points[i + 2];
            Vector2 ua = mesh.Uvs[i], ub = mesh.Uvs[i + 1], uc = mesh.Uvs[i + 2];
            RestFrame rest = RestFrame.Of(a, b, c, ua, ub, uc);
            into.Vertices.Add(new CanvasVertex(a, ua, mesh.Colors[i], a, rest, (uint)i));
            into.Vertices.Add(new CanvasVertex(b, ub, mesh.Colors[i + 1], b, rest, (uint)i + 1));
            into.Vertices.Add(new CanvasVertex(c, uc, mesh.Colors[i + 2], c, rest, (uint)i + 2));
        }

        into.Pieces.Add((0, (uint)into.Vertices.Count, 0));
    }

    /// <summary>Sends the meshes cut this frame, and lets go of those no longer drawn.</summary>
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
        : IEachEntity<CopiedMesh>
    {
        public CopyPass? Copy;

        public void Execute(ref CopiedMesh kept, int id)
        {
            if (frame - kept.LastFrame > KeptMeshes.KeptFrames)
            {
                kept.Vertices.Gpu.Dispose();
                deletes.DeleteEntity(id);
                return;
            }

            if (!kept.Pending || kept.Vertices.Vertices.Count == 0)
                return;

            kept.Pending = false;
            Copy ??= commands.BeginCopyPass();
            kept.Vertices.Gpu.Upload(Copy, CollectionsMarshal.AsSpan(kept.Vertices.Vertices), staging);
        }
    }

    private struct DisposeMeshes : IEach<CopiedMesh>
    {
        public void Execute(ref CopiedMesh kept) => kept.Vertices.Gpu.Dispose();
    }
}
