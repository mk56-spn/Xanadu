using System;
using System.IO;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A text perf toy: a grid of CJK ideographs built from ordinary ECS UI, one
/// text node per cell, from 200 to 20,000 of them. Every cell is a different
/// character (U+4E00 on), so the glyph atlas fills page after page. CHURN
/// re-rolls that many random cells a second, each a fresh shape and layout.
/// The characters come from the system CJK font HostText falls back to.
/// GPU BUFFER swaps the nodes for the same grid kept on the GPU
/// (<see cref="CjkGridGpu"/>): a buffer of glyph references, patched in place,
/// drawn in one call; only this lab draws text that way so far.
/// </summary>
internal sealed class CjkGridLab : ILabExperiment
{
    private static readonly int[] Counts = [200, 500, 1_000, 2_000, 5_000, 10_000, 20_000];

    /// <summary>The CJK Unified Ideographs block: 20,992 characters, enough for every cell to differ.</summary>
    private const int FirstIdeograph = 0x4E00, Ideographs = 0x9FFF - 0x4E00 + 1;

    private readonly LabPanel _ui = new();
    private int _count;
    private Entity _churn, _readout, _gpu;

    public string Name => "CJK GRID";

    public string[] Notes =>
    [
        "A grid of CJK ideographs, one UI text node per cell,",
        "every cell a different character, so the glyph atlas",
        "keeps opening pages. CHURN re-rolls cells a second.",
        "GPU BUFFER keeps the grid on the GPU instead: one uint",
        "per cell pointing into a glyph table, patched in place.",
    ];

    public void BuildPanel(UiNode panel)
    {
        _ui.Stepper(panel, "CHARACTERS", () => _count, i => _count = i, Counts.Length, i => Counts[i].ToString("N0"));
        _churn = LabPanel.Slider(panel, "CHURN / S", 0, 5000, 0, "Random cells given a new character each second", "0");
        _gpu = LabPanel.Toggle(LabPanel.Row(panel), "GPU BUFFER", "UI NODES", false, 220,
            "UI NODES: one UI text node per cell. GPU BUFFER: the grid as a GPU buffer of glyph references, patched in place, one draw");
        _readout = panel.Text("", UiType.Small, Palette.Text).Size(LabPanel.Inner, 60).Entity;
    }

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    private sealed class Stage(CjkGridLab lab, LabContext context, Handle parent, Rect2 area) : IToolStage
    {
        private const MoonWorks.Graphics.TextureFormat Format = MoonWorks.Graphics.TextureFormat.R8G8B8A8Unorm;

        private CjkGridGpu? _gpu;
        private MoonWorks.Graphics.Texture? _target;
        private Handle _shown, _item;
        private bool _onGpu;
        private readonly Random _random = new(42);
        private UiNode _body;
        private Entity _grid;
        private Entity[] _cells = [];
        private int _built = -1;
        private int _next;
        private float _churnDebt;
        private float _frameMs = 16f;
        private float _buildMs;
        private Rect2 _area = area;

        /// <summary>The shell only shows a stage's UI when it has some right after this, so the first grid is built here.</summary>
        public void Build(UiNode body)
        {
            _body = body;
            Rebuild();
        }

        public void Resized(Rect2 next)
        {
            _area = next;
            _built = -1;
            DropTarget();
        }

        /// <summary>Lets the GPU grid's target go; the next GPU build makes one the stage's new size.</summary>
        private void DropTarget()
        {
            if (_target is null)
                return;

            CanvasServer.Current.Free(_item);
            context.Hide(_shown);
            _target.Dispose();
            _target = null;
        }

        public void Update(in ToolFrame frame)
        {
            lab._ui.PollSteppers();
            lab._ui.ShowSteppers();

            bool gpu = LabPanel.On(lab._gpu);
            if (_built != lab._count || gpu != _onGpu)
            {
                _onGpu = gpu;
                Rebuild();
            }

            Churn(frame.Delta);

            _frameMs += (frame.Delta * 1000f - _frameMs) * 0.05f;
            string sent = _onGpu && _gpu is { } g
                ? $" · sent {g.LastBytes:N0} B ({g.LastPatches} patches)"
                : "";
            UiText.Set(lab._readout,
                $"{Counts[_built]:N0} cells · {_frameMs:0.0} ms ({1000f / MathF.Max(_frameMs, 0.01f):0} fps) · " +
                $"last build {_buildMs:0} ms · atlas pages {context.Text.Atlas.PageCount}{sent}");
        }

        /// <summary>Throws the old grid away and lays out a new one, square-ish cells filling the stage.</summary>
        private void Rebuild()
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            _built = lab._count;
            if (!_grid.IsNull)
                UiLayout.DeleteSubtree(_grid);

            int count = Counts[_built];
            float aspect = _area.Size.X / MathF.Max(1f, _area.Size.Y);
            int columns = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(count * aspect)));
            int rows = (count + columns - 1) / columns;
            float cell = MathF.Floor(MathF.Min(_area.Size.X / columns, _area.Size.Y / rows));
            int size = Math.Max(4, (int)(cell * 0.8f));

            _next = count;
            _cells = [];
            _grid = default;
            if (_onGpu)
            {
                ShowGpu(true);
                _gpu!.Layout(count, columns, cell, size, Vector2.Zero, _area.Size);
                _buildMs = (float)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                return;
            }

            ShowGpu(false);
            UiNode grid = _body.Grid(columns);
            _grid = grid.Entity;
            _cells = new Entity[count];
            for (int i = 0; i < count; i++)
            {
                _cells[i] = grid.Text(Character(i), size, Tint(i), TextAlign.Center).Size(cell, cell).Entity;
            }

            _buildMs = (float)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        /// <summary>Gives random cells the next unused character, as many a second as CHURN asks.</summary>
        private void Churn(float delta)
        {
            int cells = Counts[_built];
            _churnDebt += LabPanel.Value(lab._churn) * delta;
            int changes = Math.Min((int)_churnDebt, cells);
            _churnDebt -= changes;
            if (_onGpu)
            {
                for (int i = 0; i < changes; i++)
                    _gpu!.Set(_random.Next(cells), _next++);
                return;
            }

            for (int i = 0; i < changes; i++)
            {
                Entity cell = _cells[_random.Next(_cells.Length)];
                UiText.Set(cell, Character(_next));
                UiText.SetColor(cell, Tint(_next));
                _next++;
            }
        }

        private static string Character(int index) => char.ConvertFromUtf32(FirstIdeograph + index % Ideographs);

        /// <summary>A slow walk round the hue wheel, so neighbours differ a little and the grid reads as a sweep.</summary>
        private static Color Tint(int index) => Color.FromHsv(index * 0.0007f % 1f, 0.45f, 1f);

        /// <summary>
        /// Makes the GPU grid, its target and the canvas item showing it, the first
        /// time; then shows or hides that item, so the two modes never draw at once.
        /// </summary>
        private void ShowGpu(bool on)
        {
            if (on && _gpu is null)
            {
                string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "cjk_grid_lab.slang");
                _gpu = new CjkGridGpu(context.Device, context.Text, module, Format, FirstIdeograph, Ideographs);
            }

            if (on && _target is null)
            {
                _target = MoonWorks.Graphics.Texture.Create2D(context.Device, "cjk grid", (uint)_area.Size.X, (uint)_area.Size.Y, Format,
                    MoonWorks.Graphics.TextureUsageFlags.ColorTarget | MoonWorks.Graphics.TextureUsageFlags.Sampler);
                _shown = context.Show(_target);
                _item = CanvasServer.Current.CreateItem();
                CanvasServer.Current.SetParent(_item, parent);
                CanvasServer.Current.AddTextureRect(_item, _area, _shown);
            }

            if (_target is not null)
                CanvasServer.Current.SetVisible(_item, on);
        }

        /// <summary>The GPU grid's frame: patches, then the one draw, into the texture the stage shows.</summary>
        public void Render(MoonWorks.Graphics.CommandBuffer commands)
        {
            if (_onGpu && _gpu is not null && _target is not null)
                _gpu.Render(commands, _target, context.Text.Atlas, Palette.Background);
        }

        public void Dispose()
        {
            if (!_grid.IsNull)
                UiLayout.DeleteSubtree(_grid);
            DropTarget();
            _gpu?.Dispose();
        }
    }
}
