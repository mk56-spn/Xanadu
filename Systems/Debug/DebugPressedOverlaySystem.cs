using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Input;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Debug;

/// <summary>F3 debug overlay with pressed-entity highlighting and ECS metrics.</summary>
public sealed class DebugPressedOverlaySystem : QuerySystem<Rectangle, Position>
{
    private const float BorderWidth = 2;
    private const float PanelWidth = 470;
    private const float PanelHeight = 360;
    private const float LineHeight = 18;

    private static readonly Color Fill = new(1f, 0.85f, 0.2f, 0.3f);
    private static readonly Color Border = Colors.Yellow;
    private static readonly Color PanelBackground = new(0.02f, 0.03f, 0.06f, 0.9f);
    private static readonly Color PanelBorder = new(0.45f, 0.6f, 0.85f, 0.8f);
    private static readonly Color TitleColor = new(1f, 0.85f, 0.3f);
    private static readonly Color TextColor = new(0.82f, 0.88f, 1f);
    private static readonly Color HeaderColor = new(0.55f, 0.7f, 1f);

    private readonly IRectCanvas _canvas;
    private readonly Rid _overlay;
    private readonly IOverlayToggleSource _toggle;
    private readonly EntityStore? _store;
    private readonly SystemRoot? _systems;
    private readonly IFpsSource? _fpsSource;

    private bool _visible;
    private bool _wasTogglePressed;

    public DebugPressedOverlaySystem(
        IRectCanvas canvas,
        Rid overlay,
        IOverlayToggleSource toggle,
        EntityStore? store = null,
        SystemRoot? systems = null,
        IFpsSource? fpsSource = null)
    {
        _canvas = canvas;
        _overlay = overlay;
        _toggle = toggle;
        _store = store;
        _systems = systems;
        _fpsSource = fpsSource;
        Filter.AllTags(Tags.Get<Pressed>());
    }

    protected override void OnUpdate()
    {
        bool togglePressed = _toggle.IsTogglePressed();
        if (togglePressed && !_wasTogglePressed)
        {
            _visible = !_visible;
            _canvas.SetVisible(_overlay, _visible);
        }

        _wasTogglePressed = togglePressed;
        _canvas.Clear(_overlay);

        if (!_visible)
            return;

        Query.ForEachEntity((ref rect, ref position, entity) =>
        {
            Rect2 local = rect.Bounds;
            if (local.Size.X <= 0 || local.Size.Y <= 0)
                return;

            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            Rect2 bounds = new(world + local.Position, local.Size);
            DrawHighlight(bounds);
        });

        if (_store is not null)
            DrawMetricsPanel();
    }

    private void DrawHighlight(Rect2 bounds)
    {
        _canvas.DrawRect(_overlay, bounds, Fill);
        DrawBorder(bounds, Border, BorderWidth);
    }

    private void DrawBorder(Rect2 bounds, Color color, float width)
    {
        _canvas.DrawRect(_overlay,
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)), color);
        _canvas.DrawRect(_overlay,
            new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)), color);
        _canvas.DrawRect(_overlay,
            new Rect2(bounds.Position, new Vector2(width, bounds.Size.Y)), color);
        _canvas.DrawRect(_overlay,
            new Rect2(new Vector2(bounds.End.X - width, bounds.Position.Y),
                new Vector2(width, bounds.Size.Y)), color);
    }

    private void DrawMetricsPanel()
    {
        if (_store is not EntityStore store)
            return;

        var lines = new List<string>();
        double fps = _fpsSource?.GetFps() ?? 60;
        float frameMs = 1000f / Mathf.Max(1f, (float)fps);

        float totalMs = 0;
        long totalMemory = 0;
        var systemRows = new List<SystemRow>();

        if (_systems is not null)
        {
            foreach (BaseSystem system in _systems.ChildSystems)
            {
                SystemPerf perf = system.Perf;
                totalMs += perf.LastMs;
                totalMemory += perf.LastMemory;

                string name = ShortName(system);
                if (name.Length > 26)
                    name = name[..26];

                systemRows.Add(new SystemRow(
                    name,
                    perf.LastMs.ToString("0.00", CultureInfo.InvariantCulture),
                    perf.LastAvgMs(60).ToString("0.00", CultureInfo.InvariantCulture),
                    perf.UpdateCount.ToString(CultureInfo.InvariantCulture)));
            }
        }

        int dirty = store.Query<Rectangle>().AllTags(Tags.Get<Dirty>()).Count;
        int moved = store.Query<VisualRid>().AllTags(Tags.Get<Moved>()).Count;
        int input = store.Query<Rectangle>().AllTags(Tags.Get<TakesInput>()).Count;
        int pressed = store.Query<Rectangle>().AllTags(Tags.Get<Pressed>()).Count;
        int containers = store.Query<UiContainer>().Count;
        int layoutDirty = store.Query<UiContainer>().AllTags(Tags.Get<LayoutDirty>()).Count;

        lines.Add(
            $"FPS {fps,3:0}   frame {frameMs,5:0.0} ms   systems {totalMs,5:0.00} ms   " +
            $"alloc {totalMemory / 1024f,5:0.0} KB");
        lines.Add(
            $"Entities {store.Count,5}   capacity {store.Capacity,5}   " +
            $"archetypes {store.ArchetypeCount,3}");
        lines.Add(
            $"Dirty {dirty,3}   Moved {moved,3}   Input {input,3}   Pressed {pressed,2}   " +
            $"LayoutDirty {layoutDirty,2}");
        lines.Add($"Containers {containers,4}");
        lines.Add("");

        var panel = new Rect2(16, 16, PanelWidth, PanelHeight);
        _canvas.DrawRect(_overlay, panel, PanelBackground);
        DrawBorder(panel, PanelBorder, 1);

        float x = panel.Position.X + 10;
        float y = panel.Position.Y + 8;

        DrawPanelLine("F3 ECS DEBUG", x, y, TitleColor);
        y += LineHeight;

        foreach (string line in lines)
        {
            DrawPanelLine(line, x, y, TextColor);
            y += LineHeight;
        }

        const float nameWidth = 205;
        const float numericWidth = 62;
        float lastX = x + nameWidth;
        float avgX = lastX + numericWidth;
        float updX = avgX + numericWidth;

        DrawPanelLeft("System", x, y, nameWidth, HeaderColor);
        DrawPanelRight("last ms", lastX, y, numericWidth, HeaderColor);
        DrawPanelRight("avg ms", avgX, y, numericWidth, HeaderColor);
        DrawPanelRight("upd", updX, y, numericWidth, HeaderColor);
        y += LineHeight;

        foreach (SystemRow row in systemRows)
        {
            DrawPanelLeft(row.Name, x, y, nameWidth, TextColor);
            DrawPanelRight(row.Last, lastX, y, numericWidth, TextColor);
            DrawPanelRight(row.Avg, avgX, y, numericWidth, TextColor);
            DrawPanelRight(row.Updates, updX, y, numericWidth, TextColor);
            y += LineHeight;
        }
    }

    private void DrawPanelLine(string text, float x, float y, Color color) =>
        DrawPanelLeft(text, x, y, PanelWidth - 20, color);

    private void DrawPanelLeft(string text, float x, float y, float width, Color color) =>
        _canvas.DrawTextLeft(_overlay, new Rect2(x, y, width, LineHeight), text, color);

    private void DrawPanelRight(string text, float x, float y, float width, Color color) =>
        _canvas.DrawTextRight(_overlay, new Rect2(x, y, width, LineHeight), text, color);

    private static string ShortName(BaseSystem system)
    {
        string name = system.Name;
        return name.EndsWith("System", StringComparison.Ordinal)
            ? name[..^6]
            : name;
    }

    private readonly record struct SystemRow(string Name, string Last, string Avg, string Updates);
}
