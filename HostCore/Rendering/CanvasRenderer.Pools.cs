using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Host.Rendering;

/// <summary>
/// One material's pool step: where its items start in the buffer, whether they are made afresh
/// this run, and for a pool born at a rate, how many are born this frame, the number of the
/// first, and which of the two birth counters this frame uses.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PoolJob
{
    public uint First, Reset, Births, Born, Parity, Pad0, Pad1, Pad2;
}

/// <summary>
/// Pooled stacks (a STEP brick): each material drawn this frame keeps its own
/// buffer of items, stepped once a frame by the effect's <c>ComputeOperation</c>
/// before any pass draws, then drawn once per item (the vertex and fragment
/// entries read the one they draw by the instance). A material whose effect
/// compiled again gets a fresh buffer, made by the stack's <c>start</c>. A pool
/// born at a rate is told each frame how many births are due: the rate times
/// the frame's time, the fraction carried to the next, so the rate comes out exact.
/// </summary>
internal sealed partial class CanvasRenderer
{
    /// <summary>A material's pool: its effect, its buffer, the births owed in part, the births so far, the frame's counter.</summary>
    private sealed class PoolRun(CanvasEffect effect, Buffer items)
    {
        public readonly CanvasEffect Effect = effect;
        public readonly Buffer Items = items;
        public double Owed;
        public uint Born;
        public uint Parity;
    }

    private readonly Dictionary<CanvasMaterial, PoolRun> _pools = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CanvasMaterial> _stepped = new(ReferenceEqualityComparer.Instance);

    private void StepPools(CommandBuffer commands, float dt, float time)
    {
        _stepped.Clear();
        foreach (Step step in _steps)
        {
            if (step.Material is not { } material || !_stepped.Add(material))
                continue;
            CanvasEffect effect = _pipelines.Effect(material);
            if (effect.Pool is not { } operation || effect.Compiled.Pool is not { } compiled)
                continue;

            bool fresh = !_pools.TryGetValue(material, out PoolRun? pool) || pool.Effect != effect;
            if (fresh)
            {
                pool?.Items.Dispose();
                // Born at a rate: two slots more, the births' counters.
                int slots = compiled.ItemCount + (compiled.PoolRate > 0 ? 2 : 0);
                pool = new PoolRun(effect, Buffer.Create<byte>(_device, "shader pool",
                    BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead,
                    (uint)(compiled.ItemBytes * slots)));
                _pools[material] = pool;
            }

            var job = new PoolJob { Reset = fresh ? 1u : 0u, Born = pool!.Born, Parity = pool.Parity };
            if (compiled.PoolRate > 0 && !fresh)
            {
                // More births than slots can't all find one; the rest are dropped, as a full pool drops them.
                pool.Owed += compiled.PoolRate * dt;
                uint births = (uint)Math.Min(Math.Floor(pool.Owed), compiled.ItemCount);
                pool.Owed = Math.Min(pool.Owed - births, 1);
                job.Births = births;
                pool.Born += births;
                pool.Parity ^= 1;
            }

            if (compiled.ParamsSize > 0)
                PushParams(commands, compiled, material, null, stage: ParamsStage.Compute);
            // Its inputs after the jobs: the globals, then kept state when the stack keeps some.
            Buffer? state = _states.GetValueOrDefault(material);
            operation.Submit(job);
            operation.Run(commands, pool.Items, Bound(compiled.StorageBuffers - 1, state), new UpdateUniforms { Dt = dt, Time = time });
        }
    }

    /// <summary>A material's pool items, or null when it keeps none (yet).</summary>
    private Buffer? PoolItems(CanvasMaterial? material) =>
        material is not null && _pools.TryGetValue(material, out PoolRun? pool) ? pool.Items : null;

    private void ForgetPool(CanvasMaterial material)
    {
        if (_pools.Remove(material, out PoolRun? pool))
            pool.Items.Dispose();
    }

    private void DisposePools()
    {
        foreach (PoolRun pool in _pools.Values)
            pool.Items.Dispose();
        _pools.Clear();
    }
}
