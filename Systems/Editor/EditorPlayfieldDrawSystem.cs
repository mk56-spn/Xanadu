using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Systems;
using Xanadu.Ecs;
using Xanadu.Editor.Analysis;
using Xanadu.Levels;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Shaders;
using Xanadu.Timeline;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Draws one editor frame: the field, what is on it (live, with the groups
/// applied at the playhead), the selection, the ghost, the group marks, the chrome and the timeline. Each of those is a drawer of
/// its own; this system only reads the frame and calls them in stacking order.
/// </summary>
public sealed class EditorPlayfieldDrawSystem :
    QuerySystem<PlayfieldView, Playfield, Playhead, NoteSelection>
{
    private readonly ShaderRoutingCanvas _canvas;
    /// <summary>The rendering-server items shaded draws go to; null when tests hand in their own.</summary>
    private readonly GodotShaderCanvasItems? _shaderItems;
    private readonly EntityStore _entities;
    private readonly Rid _editor;
    private readonly IPassSurfaces? _passes;
    private readonly EditorFieldDrawer _field;
    private readonly EditorItemsDrawer _items;
    private readonly EditorPathDrawer _path;
    private readonly EditorGhostDrawer _ghost;
    private readonly EditorGroupsDrawer _groups;
    private readonly EditorGroupTagsDrawer _tags;
    private readonly EditorSelectionDrawer _selection;
    private readonly EditorChromeDrawer _chrome;
    private readonly EditorTimelineDrawer _timeline;

    /// <summary>The clock grass sways by; it runs whether or not the song plays.</summary>
    private float _seconds;

    public EditorPlayfieldDrawSystem(
        EntityStore entities,
        IRectCanvas canvas,
        Rid editor,
        IPassSurfaces? passes = null,
        Func<Rect2, Rect2>? toPixels = null,
        IShaderCanvasItems? shaderItems = null)
    {
        _entities = entities;

        // Tests have no rendering server: calling it outside the engine jumps to a
        // null native pointer and kills the whole test host, so they pass a fake.
        _shaderItems = shaderItems is null && HostPlatform.IsGodot ? new GodotShaderCanvasItems() : null;
        _canvas = new ShaderRoutingCanvas(canvas, editor, shaderItems ?? _shaderItems ?? new ServerShaderCanvasItems(), toPixels);
        _editor = editor;
        _passes = passes;

        var surface = new EditorDrawSurface(_canvas, editor, entities);
        _field = new EditorFieldDrawer(surface);
        _items = new EditorItemsDrawer(surface, passes);
        _path = new EditorPathDrawer(surface);
        _ghost = new EditorGhostDrawer(surface);
        _groups = new EditorGroupsDrawer(surface);
        _tags = new EditorGroupTagsDrawer(surface);
        _selection = new EditorSelectionDrawer(surface);
        _chrome = new EditorChromeDrawer(surface);
        _timeline = new EditorTimelineDrawer(surface);
    }

    /// <summary>Frees the editor's self-contained layer surfaces; called when leaving the editor.</summary>
    public void ReleasePasses() => _passes?.ReleaseAll();

    /// <summary>
    /// Erases what the editor last drew. Shaded draws live in child canvas items of
    /// the shared playfield, which nothing else clears, so leaving them alone lets
    /// the last frame's timeline stay on top of the next scene.
    /// </summary>
    public void Clear() => _canvas.Clear(_editor);

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
            if (_shaderItems is not null && entity.TryGetComponent(out LevelShaders shaders))
            {
                LevelShaderMaterials.Ensure(shaders);
                _shaderItems.MaterialOf = (slot, blend) => LevelShaderMaterials.MaterialOf(shaders, slot, blend);
                _shaderItems.CommonMaterialOf = (path, blend) => CommonShaderMaterials.MaterialOf(path, blend);
            }

            Color? background = null;
            Color? outside = null;
            Color? frameColor = null;
            if (entity.TryGetComponent(out LevelInfo info))
            {
                background = info.Background;
                outside = info.Outside;
                frameColor = info.Frame;
            }

            var frame = new EditorFrame(
                entity,
                view,
                playfield,
                playhead,
                selection,
                entity.TryGetComponent(out TimelineView timelineView) ? timelineView : new TimelineView(1f),
                entity.TryGetComponent(out WaveformPreview waveform) ? waveform : default,
                entity.TryGetComponent(out UiMarqueeBox marquee) ? marquee : default,
                entity.TryGetComponent(out LevelGroups groups) ? groups : default,
                entity.TryGetComponent(out EditorGroupState groupState) ? groupState : EditorGroupState.Closed,
                entity.TryGetComponent(out EditorAnalysis analysis) ? analysis : default,
                background,
                outside,
                frameColor,
                entity.TryGetComponent(out PlayerPath path) ? path : null,
                _seconds);

            Draw(frame);
        });
    }

    private void Draw(EditorFrame frame)
    {
        _field.Draw(frame);

        // The player's path stands in for the player, so it sits in the wedge.
        Action? wedge = frame.Path is { } path && PlayerPathSwitch.IsVisible(_entities)
            ? () => _path.Draw(frame.View, frame.Playhead, path, frame.Playfield.Width)
            : null;
        _items.Draw(frame, wedge);
        _selection.DrawRegions(frame.View, frame.Selection);
        _ghost.Draw(frame);
        _groups.DrawMoved(frame.View);
        _selection.DrawOutlines(frame.View, frame.Selection);
        if (frame.GroupState.TargetEdit)
            _groups.DrawTarget(frame.View, frame.Playfield, frame.Groups, frame.GroupState, frame.Selection);
        _selection.DrawHandles(frame.View, frame.Selection);
        _tags.Draw(frame.Entity, frame.View);

        // The chrome goes over the field, so a panned or zoomed field stays out of it.
        _chrome.DrawMasks(frame.View, EditorViewport.Area(frame.View.ScreenSize));
        _chrome.Draw(frame.View);
        _timeline.Draw(frame);
        _selection.DrawBox(frame.Box);
    }
}
