using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Slang;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The top of the column under the preview: the clips in the code on their timeline (<see cref="ClipTimeline"/>), then
/// the motion the graph's probes measured (<see cref="ProbeLanes"/>) over the same window, each growing with its rows.
/// Made in two steps: the nodes where the column wants them, the canvas items once everything they draw over exists.
/// </summary>
internal sealed class TimelineColumn(UiNode side) : IDisposable
{
    /// <summary>The window the probes sample while the clips give none, in seconds.</summary>
    private const double ProbeSpan = 8;

    private readonly UiNode _clipsNode = side.Column().Height(ClipTimeline.MinHeight);
    private readonly UiNode _motionNode = side.Column().Height(ClipTimeline.MinHeight);
    private ProbeLanes? _motion;

    /// <summary>The code's clip sites (names and lines for the reported clips), and the clip report last used.</summary>
    private List<ClipSite> _sites = [];
    private byte[] _usedReport = [];

    public ClipTimeline Clips { get; private set; } = null!;

    /// <summary>Makes the timeline and the lanes, their items under <paramref name="parent"/>.</summary>
    public ClipTimeline Make(EntityStore entities, Handle parent, bool scrubs)
    {
        Clips = new ClipTimeline(entities, HostPlatform.NewCanvas(), Child(parent)) { Scrubs = scrubs };
        _motion = new ProbeLanes(HostPlatform.NewCanvas(), Child(parent), Child(parent));
        return Clips;
    }

    /// <summary>The window the probes sample (<see cref="ProbeNodes.Window"/>): the turn the clips show, else <see cref="ProbeSpan"/> round the clock.</summary>
    private (double Start, double Length) Window => Clips.Shown is { Length: > 0 } shown
        ? shown
        : (Math.Floor(Clips.Clock.Seconds / ProbeSpan) * ProbeSpan, ProbeSpan);

    /// <summary>The window as the probes' uniform: from, span, beats a second.</summary>
    public Vector4 ProbeWindow => new((float)Window.Start, (float)Window.Length, (float)(ClipTimeline.LabBpm / 60), 0);

    public void Place()
    {
        Clips.Area = UiLayout.Box(_clipsNode.Entity);
        _motion!.Area = UiLayout.Box(_motionNode.Entity);
    }

    /// <summary>The names of the code as typed (joined into one text for a stack): the clips' sites and the probed lanes.</summary>
    public void Use(string joined)
    {
        _sites = ClipProbes.Sites(joined);
        _usedReport = [];
        _motion!.Use(ProbeNodes.Labels(joined), ColourProbes.Labels(joined), RecordProbes.Labels(joined));
    }

    public void Draw()
    {
        // The clips are whatever the probe last saw the code read, once it reports something new.
        ReadOnlySpan<byte> report = _motion!.ClipReport;
        if (!report.SequenceEqual(_usedReport))
        {
            _usedReport = report.ToArray();
            float height = Clips.Height;
            Clips.Use(ClipProbes.Read(report, _sites));
            // More clips, more rows: the column under the preview makes room.
            if (Clips.Height != height)
                _clipsNode.Height(Clips.Height);
        }

        Clips.Draw();
        _motion!.Draw(Window, Clips.Clock.Seconds);
        if (Math.Abs(UiLayout.Box(_motionNode.Entity).Size.Y - _motion.Height) > 0.5f)
            _motionNode.Height(_motion.Height);
    }

    private static Handle Child(Handle parent)
    {
        Handle item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(item, parent);
        return item;
    }

    public void Dispose()
    {
        Clips.Dispose();
        _motion?.Dispose();
        UiLayout.DeleteSubtree(_clipsNode.Entity);
        UiLayout.DeleteSubtree(_motionNode.Entity);
    }
}
