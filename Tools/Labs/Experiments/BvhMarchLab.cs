using System.Collections.Generic;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Host.Rendering;
using Xanadu.Host.Tools;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// The fourth raymarcher (Shaders/bvh_march_lab.slang): up to 262,144 melting
/// blobs marched in compute through a BVH the GPU rebuilds every frame (Morton
/// codes, radix sort, Karras build, atomic refit). Tiles cull against it and
/// the raster depth, rays march only what they meet, shadows read a
/// light-space grid (or walk the BVH), and
/// depth goes both ways between the march and the raster floor, pillars and
/// sparks. The tab owns the panel, the camera and the GPU times;
/// <see cref="BvhMarchStage"/> owns the GPU.
/// </summary>
internal sealed class BvhMarchLab : ILabExperiment
{
    public static readonly int[] Counts = [1_024, 4_096, 16_384, 65_536, 262_144];
    public const int MaxCount = 262_144;

    private static readonly string[] ShadowModes = ["OFF", "BVH WALK", "LIGHT GRID"];
    private static readonly string[] Culls = ["TILES → BVH", "BVH PER PIXEL"];
    private static readonly string[] Views = ["LIT", "MARCH STEPS", "SPHERES TESTED", "TILE LISTS", "SHADOW NODES", "DEPTH", "NORMALS", "GATHER ROUNDS"];

    /// <summary>The GPU groups this tab records, in the order the readout lists them.</summary>
    public static readonly string[] Passes = ["bvh animate", "bvh sort", "bvh build", "bvh refit", "bvh light", "bvh raster", "bvh tiles", "bvh march", "bvh sparks"];

    private readonly LabPanel _ui = new();
    private readonly Dictionary<string, double> _ms = [];
    private int _count = 2, _cull, _view, _shadowMode = 2;
    private readonly Entity[] _times = new Entity[Passes.Length + 1];
    private Entity _blend, _softness, _speed, _sparks, _orbit, _readout;

    public string Name => "BVH MARCH";

    public string[] Notes =>
    [
        "Raymarcher four: blobs marched in compute through a BVH",
        "rebuilt on the GPU each frame. Tiles cull through it, rays",
        "march only what they meet, shadows read a sun-space grid,",
        "and depth goes both ways with the raster floor, pillars and",
        "sparks. Drag orbits, the wheel zooms.",
    ];

    public int Count => Counts[_count];
    public bool Tiles => _cull == 0;
    public uint View => (uint)_view;
    public float Blend => LabPanel.Value(_blend);
    public float Softness => LabPanel.Value(_softness);
    public float Speed => LabPanel.Value(_speed);
    /// <summary>0 off, 1 a shadow ray walks the BVH, 2 it reads its cell of the light-space grid.</summary>
    public int Shadows => _shadowMode;
    public bool Sparks => LabPanel.On(_sparks);
    public bool Orbit => LabPanel.On(_orbit);

    public void BuildPanel(UiNode panel)
    {
        _ui.Stepper(panel, "SPHERES", () => _count, i => _count = i, Counts.Length, i => Counts[i].ToString("N0"));
        _ui.Stepper(panel, "CULL", () => _cull, i => _cull = i, Culls.Length, i => Culls[i]);
        _ui.Stepper(panel, "SHADOWS", () => _shadowMode, i => _shadowMode = i, ShadowModes.Length, i => ShadowModes[i]);
        _ui.Stepper(panel, "VIEW", () => _view, i => _view = i, Views.Length, i => Views[i]);
        _blend = LabPanel.Slider(panel, "MELT", 0, 1.5f, 0.6f, "How far blobs melt into each other, in sphere radii");
        _softness = LabPanel.Slider(panel, "SHADOW EDGE", 2, 40, 10, "Higher is a harder shadow edge", "0");
        _speed = LabPanel.Slider(panel, "SPEED", 0, 3, 1, "Galaxy seconds per second; 0 freezes it", "0.0");
        UiNode toggles = LabPanel.Row(panel);
        _sparks = LabPanel.Toggle(toggles, "SPARKS ON", "SPARKS OFF", true, 190, "Raster sparks depth tested against the marched blobs");
        _orbit = LabPanel.Toggle(toggles, "ORBIT ON", "ORBIT OFF", true, 190, "Turn the camera slowly round the galaxy");
        _readout = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(LabPanel.Inner, LabPanel.RowHeight);
        for (int i = 0; i < _times.Length; i++)
            _times[i] = panel.Text("", UiType.Small, Palette.TextMuted).Mono().Size(LabPanel.Inner, LabPanel.RowHeight);
    }

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new BvhMarchStage(this, context, parent, area);

    public void Poll()
    {
        _ui.PollSteppers();
        _ui.ShowSteppers();
    }

    /// <summary>One frame's GPU groups: each pass's time, smoothed.</summary>
    public void Timed(long number, ReadOnlySpan<(int Depth, string Name, double Ms)> ranges)
    {
        foreach (string pass in Passes)
        {
            double sum = 0;
            foreach ((int _, string name, double ms) in ranges)
                if (name == pass)
                    sum += ms;
            _ms[pass] = _ms.TryGetValue(pass, out double old) ? old + (sum - old) * 0.1 : sum;
        }
    }

    public void ShowReadout(uint width, uint height)
    {
        int nodes = 2 * Count - 1;
        int tiles = (int)(((width + 7) / 8) * ((height + 7) / 8));
        UiText.Set(_readout, $"{Count:N0} spheres · {nodes:N0} nodes · {width}×{height} px · {tiles:N0} tiles");
        if (!GpuTimes.Enabled)
        {
            UiText.Set(_times[0], "GPU times off: build the layer (External/fetch-vulkan-headers.sh)");
            return;
        }

        double total = 0;
        for (int i = 0; i < Passes.Length; i++)
        {
            double ms = _ms.GetValueOrDefault(Passes[i]);
            total += ms;
            UiText.Set(_times[i], $"{Passes[i][4..],-8}{ms,7:0.000} ms");
        }
        UiText.Set(_times[^1], $"{"total",-8}{total,7:0.000} ms");
    }
}

/// <summary>The uniforms every pass reads (Frame in bvh_march_lab.slang).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BvhMarchFrame
{
    public Vector4 Eye, Right, Up, Forward, Size;
    public uint Count, Blocks, Pass, View;
    public Vector4 Look, Sun;
}
