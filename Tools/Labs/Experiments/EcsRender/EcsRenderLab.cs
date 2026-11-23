using System;
using System.IO;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using MoonWorks.Graphics;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Labs.Experiments.EcsRender.Systems;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using Mw = MoonWorks.Graphics;

namespace Xanadu.Labs.Experiments.EcsRender;

/// <summary>
/// Rendering organised as ECS instead of a canvas server. Shapes and text are
/// entities with plain components, and everything done to them is a system:
/// logic systems write the components, render systems read them into one
/// <see cref="DrawList"/> (an instance per shape and per glyph), sort it by
/// layer and z and send it, and <see cref="InstancePass"/> draws it as
/// instanced quads, shapes and glyphs in the same call. The stage only makes
/// the world, runs its system root and shows the numbers.
/// </summary>
internal sealed class EcsRenderLab : ILabExperiment
{
    private static readonly int[] ShapeCounts = [0, 1_000, 10_000, 50_000, 100_000];
    private static readonly int[] RowCounts = [0, 60, 180, 600];

    private readonly LabPanel _ui = new();
    private int _shapes = 2, _rows = 2;
    private Entity _moving, _counts, _draws, _times;

    public string Name => "ECS RENDER";

    public string[] Notes =>
    [
        "No canvas items: every shape and line of text is an entity",
        "with Placed, Depth, Tint and a Shape or a Label. Systems read",
        "them into one draw list, sort it by layer and z, and it draws",
        "as one instanced quad per shape and per glyph, in one call.",
    ];

    public void BuildPanel(UiNode panel)
    {
        _ui.Stepper(panel, "SHAPES", () => _shapes, i => _shapes = i, ShapeCounts.Length, i => ShapeCounts[i].ToString("N0"));
        _ui.Stepper(panel, "TEXT ROWS", () => _rows, i => _rows = i, RowCounts.Length, i => RowCounts[i].ToString("N0"));
        UiNode flags = LabPanel.Row(panel);
        _moving = LabPanel.Toggle(flags, "MOVING", "STILL", true, 196,
            "MOVING: the drift system writes every shape's place each frame. STILL: it is switched off; the render systems still read everything");
        _counts = Readout(panel);
        _draws = Readout(panel);
        _times = Readout(panel);

        panel.Text("Layers, back to front: shapes, the text's panels, the text,", UiType.Small, Palette.TextMuted);
        panel.Text("then every 40th shape as a ring in front of it all.", UiType.Small, Palette.TextMuted);
        panel.Text("The frame counter is a label retyped every frame: the one", UiType.Small, Palette.TextMuted);
        panel.Text("label laid out again. Rows off the stage are culled.", UiType.Small, Palette.TextMuted);
    }

    private static Entity Readout(UiNode panel) =>
        panel.Text("", UiType.Small, Palette.Accent).Mono().Size(LabPanel.Inner, LabPanel.RowHeight);

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    private sealed class Stage : IToolStage
    {
        private const Mw.TextureFormat Format = Mw.TextureFormat.R8G8B8A8Unorm;
        private const short ShapeLayer = 0, PanelLayer = 1, TextLayer = 2, FrontLayer = 3, CounterLayer = 4;
        private const int TextSize = 11;
        private const float RowHeight = 14, ColumnWidth = 470, Margin = 24;

        private readonly EcsRenderLab _lab;
        private readonly LabContext _context;
        private readonly uint _width, _height;
        private readonly Texture _target;
        private readonly Handle _shown, _item;
        private readonly string[] _source;
        private readonly DrawList _list;
        private readonly InstancePass _pass;

        // The world and its systems: made whole again when a count changes.
        private EntityStore _store = null!;
        private SystemRoot _root = null!;
        private DriftSystem _drift = null!;
        private SystemGroup _logic = null!, _extract = null!;
        private BaseSystem _layout = null!, _sort = null!, _write = null!;
        private int _builtShapes = -1, _builtRows = -1;

        public Stage(EcsRenderLab lab, LabContext context, Handle parent, Rect2 area)
        {
            _lab = lab;
            _context = context;
            _width = (uint)area.Size.X;
            _height = (uint)area.Size.Y;

            // The lab host shows a stage through the canvas; the one canvas item here is that picture frame.
            _target = Texture.Create2D(context.Device, "ecs render", _width, _height, Format,
                Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler);
            _shown = context.Show(_target);
            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            CanvasServer.Current.AddTextureRect(_item, new Rect2(area.Position, new Vector2(_width, _height)), _shown);

            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "ecs_render_lab.slang");
            _source = Array.FindAll(File.ReadAllLines(module), line => line.Trim().Length > 0);
            _list = new DrawList(new Vector2(_width, _height));
            _pass = new InstancePass(context.Device, context.Text.Atlas, module, Format);
        }

        private void BuildWorld(int shapes, int rows)
        {
            _store = new EntityStore();
            (_builtShapes, _builtRows) = (shapes, rows);

            _logic = new SystemGroup("Logic")
            {
                (_drift = new DriftSystem()),
                new FrameCounterSystem(),
            };
            _extract = new SystemGroup("Extract")
            {
                new DrawBeginSystem(_list),
                new ShapeExtractSystem(_list),
                new LabelExtractSystem(_list),
            };
            _root = new SystemRoot(_store, "EcsRender")
            {
                _logic,
                new SystemGroup("Render")
                {
                    (_layout = new LabelLayoutSystem(_context.Text, _list)),
                    _extract,
                    (_sort = new DrawSortSystem(_list)),
                    (_write = new DrawWriteSystem(_list, _pass)),
                },
            };
            _root.SetMonitorPerf(true);

            var random = new Random(7);
            for (int i = 0; i < shapes; i++)
            {
                var home = new Vector2(random.NextSingle() * _width, random.NextSingle() * _height);
                bool front = i % 40 == 0;
                ShapeKind kind = front ? ShapeKind.Ring : (ShapeKind)random.Next(2);
                float size = front ? 30 + random.NextSingle() * 30 : 6 + random.NextSingle() * 22;
                _store.CreateEntity(
                    new Placed { Position = home },
                    new Depth { Layer = front ? FrontLayer : ShapeLayer, Z = (short)random.Next(-100, 101) },
                    new Tint { Value = Color.FromHsv(random.NextSingle(), front ? 0.35f : 0.7f, 1, front ? 0.9f : 0.65f) },
                    new Shape { Kind = kind, Size = new Vector2(size, kind == ShapeKind.Box ? size * 0.6f : size), Edge = front ? 2.5f : 3 },
                    new Drift
                    {
                        Home = home, Radius = 10 + random.NextSingle() * 50,
                        Speed = (random.NextSingle() - 0.5f) * 3, Phase = random.NextSingle() * MathF.Tau,
                    });
            }

            // Text in columns, each on a dark panel; rows past the stage's right edge are still entities.
            int perColumn = Math.Max(1, (int)((_height - Margin * 2 - 40) / RowHeight));
            int columns = (rows + perColumn - 1) / perColumn;
            for (int column = 0; column < columns; column++)
            {
                float left = Margin + column * (ColumnWidth + Margin);
                var size = new Vector2(ColumnWidth, perColumn * RowHeight + 16);
                _store.CreateEntity(
                    new Placed { Position = new Vector2(left + size.X / 2, Margin + size.Y / 2) },
                    new Depth { Layer = PanelLayer },
                    new Tint { Value = new Color(0.03f, 0.045f, 0.09f, 0.86f) },
                    new Shape { Kind = ShapeKind.Box, Size = size, Edge = 8 });
            }

            for (int row = 0; row < rows; row++)
            {
                string line = _source[row % _source.Length];
                if (line.Length > 64)
                    line = line[..64];
                bool comment = line.TrimStart().StartsWith("//", StringComparison.Ordinal);
                float left = Margin + row / perColumn * (ColumnWidth + Margin) + 10;
                _store.CreateEntity(
                    new Placed { Position = new Vector2(left, Margin + 8 + row % perColumn * RowHeight) },
                    new Depth { Layer = TextLayer },
                    new Tint { Value = comment ? new Color("#6f8f78") : new Color("#d8def0") },
                    new Label { Text = line, Size = TextSize },
                    new LabelGlyphs());
            }

            _store.CreateEntity(
                new Placed { Position = new Vector2(Margin, _height - Margin - 22) },
                new Depth { Layer = CounterLayer },
                new Tint { Value = Palette.Accent },
                new Label { Text = "", Size = 18 },
                new LabelGlyphs(),
                new FrameCounter());
        }

        public void Update(in ToolFrame frame)
        {
            _lab._ui.PollSteppers();
            _lab._ui.ShowSteppers();
            int shapes = ShapeCounts[_lab._shapes], rows = RowCounts[_lab._rows];
            if (shapes != _builtShapes || rows != _builtRows)
                BuildWorld(shapes, rows);

            _drift.Enabled = LabPanel.On(_lab._moving);
            _root.Update(new UpdateTick(frame.Delta, frame.Time));

            float megabytes = _list.Count * (float)Marshal.SizeOf<DrawInstance>() / (1 << 20);
            UiText.Set(_lab._counts, $"{_store.Count:N0} entities → {_list.Count:N0} instances · {_list.Culled:N0} culled");
            UiText.Set(_lab._draws, $"{_list.Draws.Count} draw · {megabytes:0.00} MB uploaded · {_list.LaidOut} label laid out");
            UiText.Set(_lab._times,
                $"ms: logic {Ms(_logic)} · layout {Ms(_layout)} · extract {Ms(_extract)} · sort {Ms(_sort)} · write {Ms(_write)}");
        }

        /// <summary>A system's own timing, as Friflo measures it, over its last frames.</summary>
        private static string Ms(BaseSystem system) => system.Perf.LastAvgMs(30).ToString("0.00");

        public void Render(Mw.CommandBuffer commands) => _pass.Draw(commands, _target, _list);

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            _context.Hide(_shown);
            _pass.Dispose();
            _target.Dispose();
        }
    }
}
