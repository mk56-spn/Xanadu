using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Shaders;
using Xanadu.Timeline;
using Xanadu.Levels;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>Draws editor chrome, the shared playfield, and the editor timeline.</summary>
public sealed partial class EditorPlayfieldDrawSystem :
    QuerySystem<PlayfieldView, Playfield, Playhead, NoteSelection>
{
    // Softened so the grid guides the eye without outshouting the notes on it.
    public static readonly Color MainSnapColor = Palette.WithAlpha(Colors.White, 0.6f);
    public static readonly Color HalfSnapColor = Palette.WithAlpha(Palette.Danger, 0.55f);
    public static readonly Color QuarterSnapColor = Palette.WithAlpha(Palette.PrimarySoft, 0.55f);

    /// <summary>The least room in pixels between two drawn beat ticks.</summary>
    private const float MinBeatSpacing = 16f;

    private static readonly Color EditorBackground = Palette.Stage;
    private static readonly Color PanelBackground = Palette.Surface;

    private readonly ShaderRoutingCanvas _canvas;
    private readonly GodotShaderCanvasItems _shaderItems = new();
    private readonly EntityStore _entities;
    private readonly Rid _editor;
    private PlayerPath? _path;
    private Entity _drawnEditor;

    /// <summary>The clock grass sways by; it runs whether or not the song plays.</summary>
    private float _seconds;

    public EditorPlayfieldDrawSystem(EntityStore entities, IRectCanvas canvas, Rid editor)
    {
        _entities = entities;
        _canvas = new ShaderRoutingCanvas(canvas, editor, _shaderItems);
        _editor = editor;
    }

    protected override void OnUpdate()
    {
        _canvas.Clear(_editor);
        _seconds += Tick.deltaTime;

        Query.ForEachEntity((
            ref view,
            ref playfield,
            ref playhead,
            ref selection,
            entity) =>
        {
            if (entity.TryGetComponent(out LevelShaders shaders))
            {
                LevelShaderMaterials.Ensure(shaders);
                _shaderItems.MaterialOf = slot => LevelShaderMaterials.MaterialOf(shaders, slot);
                _shaderItems.CommonMaterialOf = CommonShaderMaterials.MaterialOf;
            }

            _analysis = entity.HasComponent<EditorAnalysis>()
                ? entity.GetComponent<EditorAnalysis>()
                : default;
            Color? background = null;
            Color? outside = null;
            Color? frame = null;
            if (entity.TryGetComponent(out LevelInfo info))
            {
                background = info.Background;
                outside = info.Outside;
                frame = info.Frame;
            }
            _path = entity.TryGetComponent(out PlayerPath path) ? path : null;
            _drawnEditor = entity;
            DrawEditor(
                    view,
                    playfield,
                    playhead,
                    selection,
                    entity.HasComponent<TimelineView>()
                        ? entity.GetComponent<TimelineView>()
                        : new TimelineView(1f),
                    entity.HasComponent<WaveformPreview>()
                        ? entity.GetComponent<WaveformPreview>()
                        : default,
                    entity.HasComponent<UiMarqueeBox>()
                        ? entity.GetComponent<UiMarqueeBox>()
                        : default,
                    entity.HasComponent<LevelGroups>()
                        ? entity.GetComponent<LevelGroups>()
                        : default,
                    entity.HasComponent<EditorGroupState>()
                        ? entity.GetComponent<EditorGroupState>()
                        : EditorGroupState.Closed,
                    background,
                    outside,
                    frame);
        });
    }

    private void DrawEditor(
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        NoteSelection selection,
        TimelineView timeline,
        WaveformPreview preview,
        UiMarqueeBox selectionBox,
        LevelGroups groups,
        EditorGroupState groupState,
        Color? background,
        Color? outside,
        Color? frame)
    {
        _canvas.DrawRect(
            _editor,
            new Rect2(Vector2.Zero, view.ScreenSize),
            EditorBackground);

        Rect2 area = EditorViewport.Area(view.ScreenSize);
        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        if (outside is { } around)
            _canvas.DrawRect(_editor, area, around);
        bool grid = ViewportGrid.IsVisible(_entities);
        if (grid)
            PlayfieldRenderer.DrawOuterGrid(_canvas, _editor, playfield, field, area);
        PlayfieldRenderer.DrawFieldFrame(_canvas, _editor, field, view.Scale, frame);
        PlayfieldRenderer.DrawField(_canvas, _editor, field, 1, background, frame);
        if (grid)
            PlayfieldRenderer.DrawGrid(_canvas, _editor, playfield, field, area);
        PlayfieldRenderer.DrawFieldLabels(_canvas, _editor, playfield, field);
        // Every selected block and spike gets the accent edge, not just the primary one.
        Func<Entity, bool> isSelected = selection.Contains;
        PlayfieldRenderer.DrawBlocks(_canvas, _editor, _entities, view, isSelected);
        PlayfieldRenderer.DrawSpikes(_canvas, _editor, _entities, view, isSelected);
        PlayfieldRenderer.DrawGrass(_canvas, _editor, _entities, view, _seconds, isSelected);
        PlayfieldRenderer.DrawTexts(_canvas, _editor, _entities, view, isSelected);
        if (_path is PlayerPath playerPath && PlayerPathSwitch.IsVisible(_entities))
            DrawPlayerPath(view, playhead, playerPath, playfield.Width);
        PlayfieldRenderer.DrawNotes(
            _canvas,
            _editor,
            _entities,
            view,
            playfield,
            playhead,
            selection.Note);
        PlayfieldRenderer.DrawRails(
            _canvas,
            _editor,
            _entities,
            view,
            playfield,
            playhead,
            previewBoth: true);
        PlayfieldRenderer.DrawMines(
            _canvas,
            _editor,
            _entities,
            view,
            playhead);
        DrawStartPoints(view, selection);
        DrawPlacementGhost(_drawnEditor, view, playfield, playhead);
        if (groupState.Preview)
            DrawGroupPreview(view, playhead, groups, groupState);
        DrawSelectionOutlines(view, selection);
        if (groupState.TargetEdit)
            DrawGroupTarget(view, playfield, groups, groupState, selection);
        DrawGridHandles(view, selection);
        DrawRotationHandle(view, selection);

        // The chrome goes over the field, so a panned or zoomed field stays out of it.
        DrawMasks(view, area);
        DrawChrome(view);
        DrawRuler(view, playhead, selection, timeline, preview);
        DrawSelectionBox(selectionBox);
    }

    private void DrawStartPoints(PlayfieldView view, NoteSelection selection)
    {
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            Vector2 center = view.PlayfieldToScreen(start.Position);
            float radius = PlayfieldRenderer.StartPointRadius(view);
            PlayfieldRenderer.DrawStartPointBody(_canvas, _editor, center, radius, PlayfieldRenderer.StartPointColor);

            // Selected and grouped the way a note is.
            if (selection.Contains(entity))
            {
                _canvas.DrawCircleOutline(
                    _editor,
                    center,
                    radius + 4f,
                    PlayfieldRenderer.SelectedNoteColor,
                    2);
            }

            if (entity.TryGetComponent(out ItemGroups groups) && groups.Mask != 0)
            {
                PlayfieldRenderer.DrawGroupBadge(
                    _canvas,
                    _editor,
                    center + new Vector2(radius * 0.78f, -radius * 0.78f),
                    groups.Mask,
                    1f);
            }
        }
    }

    /// <summary>
    /// Outlines the selection whatever the playhead's time, so an item stays
    /// marked after scrolling past the point where its body fades out.
    /// </summary>
    private void DrawSelectionOutlines(PlayfieldView view, NoteSelection selection)
    {
        float size = PlayfieldRenderer.NoteSize * view.Scale;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (!selection.Contains(entity))
                continue;

            Note note = entity.GetComponent<Note>();
            Vector2 center = view.PlayfieldToScreen(note.Position);
            _canvas.DrawCircleOutline(
                _editor,
                center,
                size / 2 + 1.5f,
                PlayfieldRenderer.SelectedNoteColor,
                2);
        }

        float mineSize = PlayfieldRenderer.MineSize * view.Scale;
        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            if (!selection.Contains(entity))
                continue;

            Mine mine = entity.GetComponent<Mine>();
            Vector2 center = view.PlayfieldToScreen(mine.Position);
            _canvas.DrawCircleOutline(
                _editor,
                center,
                mineSize / 2 + 1.5f,
                PlayfieldRenderer.SelectedNoteColor,
                2);
        }
    }

    private void DrawSelectionBox(UiMarqueeBox box) =>
        CanvasMarquee.Draw(_canvas, _editor, box);

    /// <summary>Covers everything above and below the field area with the editor's background.</summary>
    private void DrawMasks(PlayfieldView view, Rect2 area)
    {
        _canvas.DrawRect(_editor, new Rect2(0, 0, view.ScreenSize.X, area.Position.Y), EditorBackground);
        _canvas.DrawRect(
            _editor,
            new Rect2(0, area.End.Y, view.ScreenSize.X, view.ScreenSize.Y - area.End.Y),
            EditorBackground);
    }

    private void DrawChrome(PlayfieldView view)
    {
        Rect2 header = EditorLayout.HeaderRect(view);
        _canvas.DrawRect(_editor, header, PanelBackground);
        _canvas.DrawRect(
            _editor,
            new Rect2(header.Position.X, header.End.Y - 1, header.Size.X, 1),
            PlayfieldRenderer.BorderColor);

        Rect2 footer = EditorLayout.FooterRect(view);
        _canvas.DrawRect(_editor, footer, PanelBackground);
        _canvas.DrawRect(
            _editor,
            new Rect2(footer.Position.X, footer.Position.Y, footer.Size.X, 1),
            PlayfieldRenderer.BorderColor);

        DrawZoomLens(view);
    }

    private void DrawBeatBands(
        Rect2 ruler,
        Playhead playhead,
        long windowStart,
        long windowEnd)
    {
        double beatMs = 60_000.0 / Math.Max(1, playhead.Bpm);
        double first = Math.Floor(
            (windowStart - playhead.OffsetMs) / beatMs);
        double last = Math.Ceiling(
            (windowEnd - playhead.OffsetMs) / beatMs);

        for (int beat = (int)first; beat < last; beat++)
        {
            double start = playhead.OffsetMs + beat * beatMs;
            double end = start + beatMs;
            if (end < windowStart || start > windowEnd)
                continue;

            float x0 = XForTime(
                ruler,
                windowStart,
                windowEnd,
                (long)Math.Max(start, windowStart));
            float x1 = XForTime(
                ruler,
                windowStart,
                windowEnd,
                (long)Math.Min(end, windowEnd));
            bool accent = beat % 4 == 0;
            _canvas.DrawRect(
                _editor,
                new Rect2(
                    x0,
                    ruler.Position.Y + 1,
                    Math.Max(1f, x1 - x0 - 1),
                    ruler.Size.Y - 2),
                new Color(1f, 1f, 1f, accent ? 0.028f : 0.012f));
        }
    }

    private void DrawZoomLens(PlayfieldView view)
    {
        Vector2 center = EditorLayout.ZoomLensCenter(view);
        Color color = PlayfieldRenderer.DimTextColor;
        const float radius = 5f;

        _canvas.DrawLine(
            _editor,
            center + new Vector2(-radius, -radius),
            center + new Vector2(radius, -radius),
            color,
            1);
        _canvas.DrawLine(
            _editor,
            center + new Vector2(radius, -radius),
            center + new Vector2(radius, radius),
            color,
            1);
        _canvas.DrawLine(
            _editor,
            center + new Vector2(radius, radius),
            center + new Vector2(-radius, radius),
            color,
            1);
        _canvas.DrawLine(
            _editor,
            center + new Vector2(-radius, radius),
            center + new Vector2(-radius, -radius),
            color,
            1);
        _canvas.DrawLine(
            _editor,
            center + new Vector2(4, 4),
            center + new Vector2(9, 9),
            color,
            2);
    }

    private void DrawRuler(
        PlayfieldView view,
        Playhead playhead,
        NoteSelection selection,
        TimelineView timeline,
        WaveformPreview preview)
    {
        Rect2 ruler = EditorLayout.RulerRect(view);
        _canvas.DrawRect(_editor, ruler, Palette.SurfaceDeep);
        _canvas.DrawBorder(
            _editor,
            ruler,
            PlayfieldRenderer.BorderColor,
            1);

        if (playhead.DurationMs <= 0)
        {
            _canvas.DrawText(
                _editor,
                ruler,
                "Load a song to enable the timeline",
                PlayfieldRenderer.DimTextColor);
            return;
        }

        (long windowStart, long windowEnd) = EditorLayout.TimelineWindow(playhead, timeline);

        DrawAnalysis(playhead, EditorLayout.AnalysisRect(view), windowStart, windowEnd);
        DrawBeatBands(ruler, playhead, windowStart, windowEnd);
        DrawWaveform(ruler, preview, windowStart, windowEnd);
        DrawBpmMarker(ruler, playhead, windowStart, windowEnd);
        DrawStartMarkers(ruler, selection, windowStart, windowEnd);
        int subdivisions = Math.Max(1, playhead.SnapDivision);
        double tickMs = TimingGrid.TickMs(playhead.Bpm, subdivisions);

        float pixelsPerTick = ruler.Size.X *
                              (float)tickMs /
                              Math.Max(1f, windowEnd - windowStart);
        int skip = 1;
        while (pixelsPerTick / skip < 3f)
            skip *= 2;

        // Zoomed out, the ticks must thin out by importance or they become a solid wall.
        float pixelsPerBeat = pixelsPerTick * subdivisions;
        int beatSkip = 1;
        while (pixelsPerBeat * beatSkip < MinBeatSpacing)
            beatSkip *= 2;

        int firstTick = TimingGrid.FirstTick(
            windowStart, playhead.OffsetMs, tickMs);
        int lastTick = TimingGrid.LastTick(
            windowEnd, playhead.OffsetMs, tickMs);

        for (int tick = firstTick; tick <= lastTick; tick++)
        {
            int sub = tick % subdivisions;
            if (sub < 0)
                sub += subdivisions;

            bool main = sub == 0;
            bool half = sub * 2 == subdivisions;
            bool quarter = sub * 4 == subdivisions ||
                           sub * 4 == subdivisions * 3;

            if (main ? FloorDiv(tick, subdivisions) % beatSkip != 0 : !ShowsTick(half, quarter, sub, skip, pixelsPerBeat))
                continue;

            double tickTime = TimingGrid.TickTime(
                tick, playhead.OffsetMs, tickMs);
            if (tickTime < windowStart - 1 || tickTime > windowEnd + 1)
                continue;

            float x = XForTime(
                ruler,
                windowStart,
                windowEnd,
                (long)Math.Round(tickTime));
            float height = main ? 22f : half ? 15f : quarter ? 10f : 5f;
            Color color = main
                ? MainSnapColor
                : half
                    ? HalfSnapColor
                    : quarter
                        ? QuarterSnapColor
                        : PlayfieldRenderer.GridColor;

            _canvas.DrawLine(
                _editor,
                new Vector2(x, ruler.End.Y - height),
                new Vector2(x, ruler.End.Y),
                color,
                main || half ? 2 : 1);
        }

        foreach ((Entity entity, Rect2 bounds) in EditorTimelineNotes.Slots(
                     _entities, ruler, windowStart, windowEnd, timeline.Zoom))
        {
            _canvas.DrawRect(
                _editor,
                bounds,
                PlayfieldRenderer.ColorFor(entity.GetComponent<Note>()));

            if (selection.Contains(entity))
            {
                _canvas.DrawBorder(
                    _editor,
                    new Rect2(
                        bounds.Position - new Vector2(1.5f, 1.5f),
                        bounds.Size + new Vector2(3, 3)),
                    PlayfieldRenderer.SelectedNoteColor,
                    1.5f);
            }
        }

        float playheadX = XForTime(
            ruler,
            windowStart,
            windowEnd,
            Math.Clamp(playhead.TimeMs, windowStart, windowEnd));

        _canvas.DrawLine(
            _editor,
            new Vector2(playheadX, ruler.Position.Y),
            new Vector2(playheadX, ruler.End.Y),
            Colors.White,
            2);
        _canvas.DrawRect(
            _editor,
            new Rect2(
                playheadX - 5f,
                ruler.Position.Y - 4f,
                10f,
                10f),
            Colors.White);
    }

    private static int FloorDiv(int value, int divisor) =>
        (int)Math.Floor(value / (double)divisor);

    /// <summary>Whether a tick between beats has room to be drawn at this zoom.</summary>
    private static bool ShowsTick(bool half, bool quarter, int sub, int skip, float pixelsPerBeat) =>
        half ? pixelsPerBeat >= MinBeatSpacing * 2f :
        quarter ? pixelsPerBeat >= MinBeatSpacing * 4f :
        pixelsPerBeat >= MinBeatSpacing * 4f && sub % skip == 0;

    private static float XForTime(
        Rect2 ruler,
        long windowStart,
        long windowEnd,
        long timeMs) =>
        new TimeWindow(windowStart, windowEnd).XForTime(ruler, timeMs);
}
