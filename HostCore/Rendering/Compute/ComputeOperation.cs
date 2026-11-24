using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Slang;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Host.Rendering.Compute;

/// <summary>
/// One kind of GPU job: a compute pipeline that runs every job submitted this
/// frame in <b>one dispatch</b>, so work of the same kind runs back to back
/// instead of a pass per job. The engine's own compute goes through it, as
/// player compute will.
/// </summary>
/// <remarks>
/// The shader's contract: <c>StructuredBuffer&lt;TJob&gt;</c> jobs at read-only
/// binding 0, its other read-only buffers after it, the arena the jobs share as
/// read-write binding 0, and one uniform block. It runs
/// <c>[numthreads(Threads, 1, 1)]</c> over (<see cref="GroupsPerJob"/>, jobs, 1)
/// groups: <c>SV_GroupID.y</c> is the job, <c>SV_GroupID.x</c> the group within
/// it. SDL_GPU fixes a pass's writable buffers when it begins, so the jobs of one
/// run share one arena (<see cref="ComputeArena{T}"/>), each working its own range.
/// </remarks>
internal sealed class ComputeOperation<TJob> : IDisposable where TJob : unmanaged
{
    public const uint Threads = 64;

    private readonly GraphicsDevice _device;
    private readonly ComputePipeline _pipeline;
    private readonly List<TJob> _jobs = [];
    private Buffer? _jobBuffer;
    private TransferBuffer? _jobUpload;
    private uint _capacity;
    private Buffer[] _read = [];

    /// <param name="inputs">Read-only buffers the shader takes after its jobs.</param>
    public ComputeOperation(GraphicsDevice device, string module, string entry, uint inputs, uint groupsPerJob)
    {
        _device = device;
        GroupsPerJob = groupsPerJob;
        _pipeline = ComputePipeline.Create(device,
            SlangShaders.Compile(Path.Combine(AppContext.BaseDirectory, "Shaders", module), entry, "compute"),
            "main", new ComputePipelineCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                NumReadonlyStorageBuffers = 1 + inputs,
                NumReadWriteStorageBuffers = 1,
                NumUniformBuffers = 1,
                ThreadCountX = Threads,
                ThreadCountY = 1,
                ThreadCountZ = 1,
            });
    }

    /// <summary>Groups each job gets: enough for its largest job's items, a thread an item.</summary>
    public uint GroupsPerJob { get; }

    /// <summary>Jobs waiting for the next <see cref="Run{TUniforms}"/>.</summary>
    public int Count => _jobs.Count;

    public void Submit(in TJob job) => _jobs.Add(job);

    /// <summary>Forgets the jobs submitted so far (work being planned again).</summary>
    public void Clear() => _jobs.Clear();

    /// <summary>
    /// Sends the jobs and runs them all in one dispatch over <paramref name="arena"/>, then forgets them.
    /// <paramref name="groupsPerJob"/> overrides <see cref="GroupsPerJob"/> for this run, when job sizes vary
    /// (enough groups for the biggest; a smaller job's extra threads find nothing to do and return).
    /// </summary>
    public void Run<TUniforms>(CommandBuffer commands, Buffer arena, ReadOnlySpan<Buffer> inputs, in TUniforms uniforms,
        uint? groupsPerJob = null)
        where TUniforms : unmanaged
    {
        if (_jobs.Count == 0)
            return;

        if (_jobBuffer is null || _jobs.Count > _capacity)
        {
            _jobBuffer?.Dispose();
            _jobUpload?.Dispose();
            _capacity = (uint)Math.Max(_jobs.Count, Math.Max(_capacity * 2, 16));
            _jobBuffer = Buffer.Create<TJob>(_device, "compute jobs", BufferUsageFlags.ComputeStorageRead, _capacity);
            _jobUpload = TransferBuffer.Create<TJob>(_device, "compute job upload", TransferBufferUsage.Upload, _capacity);
        }

        CollectionsMarshal.AsSpan(_jobs).CopyTo(_jobUpload!.Map<TJob>(cycle: true));
        _jobUpload.Unmap();
        CopyPass copy = commands.BeginCopyPass();
        copy.UploadToBuffer<TJob>(_jobUpload, _jobBuffer, 0, 0, (uint)_jobs.Count, cycle: true);
        commands.EndCopyPass(copy);

        ComputePass pass = commands.BeginComputePass(new StorageBufferReadWriteBinding(arena, cycle: false));
        pass.BindComputePipeline(_pipeline);
        if (_read.Length != 1 + inputs.Length)
            _read = new Buffer[1 + inputs.Length];
        _read[0] = _jobBuffer;
        inputs.CopyTo(_read.AsSpan(1));
        pass.BindStorageBuffers(_read);
        commands.PushComputeUniformData(uniforms);
        pass.Dispatch(Math.Max(1, groupsPerJob ?? GroupsPerJob), (uint)_jobs.Count, 1);
        commands.EndComputePass(pass);
        _jobs.Clear();
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        _jobBuffer?.Dispose();
        _jobUpload?.Dispose();
    }
}
