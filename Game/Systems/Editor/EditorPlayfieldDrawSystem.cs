using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
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
/// applied at the playhead), the selection, the ghost, the group marks, the
/// chrome and the timeline. A group of small systems, so the F3 overlay and the
/// frame autopsy time each part on its own: <c>Frame</c> clears the canvas and
/// reads each editor's frame, then one system per drawer draws it, in stacking order.
/// </summary>
public sealed class EditorPlayfieldDrawSystem : SystemGroup
{
    private readonly ShaderRoutingCanvas _canvas;

    /// <summary>The rendering-server items shaded draws go to; null when tests hand in their own.</summary>
    private readonly ServerShaderCanvasItems? _serverItems;
    private readonly EntityStore _entities;
    private readonly Handle _editor;
    private readonly IPassSurfaces? _passes;

    /// <summary>This frame's editors (one in practice), read by <see cref="FrameSystem"/> for the drawing systems after it.</summary>
    private readonly List<EditorFrame> _frames = new();

    /// <summary>The clock grass sways by; it runs whether or not the song plays.</summary>
    private float _seconds;

    public EditorPlayfieldDrawSystem(
        EntityStore entities,
        IRectCanvas canvas,
        Handle editor,
        IPassSurfaces? passes = null,
        Func<Rect2, Rect2>? toPixels = null,
        IShaderCanvasItems? shaderItems = null) : base("EditorPlayfieldDraw")
    {
        _entities = entities;

        // Tests have no rendering server: calling it outside the engine jumps to a
        // null native pointer and kills the whole test host, so they pass a fake.
        _canvas = new ShaderRoutingCanvas(canvas, editor, shaderItems ?? (_serverItems = new ServerShaderCanvasItems()), toPixels);
        _editor = editor;
        _passes = passes;

        var surface = new EditorDrawSurface(_canvas, editor, entities);
        var field = new EditorFieldDrawer(surface);
        var items = new EditorItemsDrawer(surface, passes);
        var path = new EditorPathDrawer(surface);
        var ghost = new EditorGhostDrawer(surface);
        var groups = new EditorGroupsDrawer(surface);
        var tags = new EditorGroupTagsDrawer(surface);
        var selection = new EditorSelectionDrawer(surface);
        var chrome = new EditorChromeDrawer(surface);
        var timeline = new EditorTimelineDrawer(surface);

        // Stacking order: each system draws over the ones before it.
        Add(new FrameSystem(this));
        Add(new DrawStep("Field", _frames, field.Draw));
        Add(new DrawStep("Items", _frames, frame =>
        {
            // The player's path stands in for the player, so it sits in the wedge.
            Action? wedge = frame.Path is { } playerPath && PlayerPathSwitch.IsVisible(_entities)
                ? () => path.Draw(frame.View, frame.Playhead, playerPath, frame.Playfield.Width)
                : null;
            items.Draw(frame, wedge);
        }));
        Add(new DrawStep("SelectionRegions", _frames, frame => selection.DrawRegions(frame.View, frame.Selection)));
        Add(new DrawStep("Ghost", _frames, ghost.Draw));
        Add(new DrawStep("GroupMoves", _frames, frame => groups.DrawMoved(frame.View)));
        Add(new DrawStep("SelectionOutlines", _frames, frame => selection.DrawOutlines(frame.View, frame.Selection)));
        Add(new DrawStep("GroupTarget", _frames, frame =>
        {
            if (frame.GroupState.TargetEdit)
                groups.DrawTarget(frame.View, frame.Playfield, frame.Groups, frame.GroupState, frame.Selection);
        }));
        Add(new DrawStep("SelectionHandles", _frames, frame => selection.DrawHandles(frame.View, frame.Selection)));
        Add(new DrawStep("GroupTags", _frames, frame => tags.Draw(frame.Entity, frame.View)));
        // The chrome goes over the field, so a panned or zoomed field stays out of it.
        Add(new DrawStep("Chrome", _frames, frame =>
        {
            chrome.DrawMasks(frame.View, EditorViewport.Area(frame.View.ScreenSize));
            chrome.Draw(frame.View);
        }));
        Add(new DrawStep("Timeline", _frames, timeline.Draw));
        Add(new DrawStep("SelectionBox", _frames, frame => selection.DrawBox(frame.Box)));
    }

    /// <summary>Frees the editor's self-contained layer surfaces; called when leaving the editor.</summary>
    public void ReleasePasses() => _passes?.ReleaseAll();

    /// <summary>
    /// Erases what the editor last drew. Shaded draws live in child canvas items of
    /// the shared playfield, which nothing else clears, so leaving them alone lets
    /// the last frame's timeline stay on top of the next scene.
    /// </summary>
    public void Clear() => _canvas.Clear(_editor);

    /// <summary>Clears the editor's canvas and reads each editor entity's frame for the drawing systems.</summary>
    private sealed class FrameSystem(EditorPlayfieldDrawSystem owner) :
        QuerySystem<PlayfieldView, Playfield, Playhead, NoteSelection>
    {
        public override string Name => "Frame";

        protected override void OnUpdate()
        {
            owner._canvas.Clear(owner._editor);
            owner._seconds += Tick.deltaTime;
            owner._frames.Clear();

            Query.ForEachEntity((
                ref view,
                ref playfield,
                ref playhead,
                ref selection,
                entity) =>
            {
                if (owner._serverItems is not null && entity.TryGetComponent(out LevelShaders levelShaders))
                    owner._serverItems.SlotMaterialOf = (slot, blend) => LevelSlotMaterials.MaterialOf(levelShaders, slot, blend);

                Color? background = null;
                Color? outside = null;
                Color? frameColor = null;
                if (entity.TryGetComponent(out LevelInfo info))
                {
                    background = info.Background;
                    outside = info.Outside;
                    frameColor = info.Frame;
                }

                owner._frames.Add(new EditorFrame(
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
                    owner._seconds));
            });
        }
    }

    /// <summary>One drawer as a system of its own, run on every editor frame <see cref="FrameSystem"/> read.</summary>
    private sealed class DrawStep(string name, List<EditorFrame> frames, Action<EditorFrame> draw) : BaseSystem
    {
        public override string Name => name;

        protected override void OnUpdateGroup()
        {
            foreach (EditorFrame frame in frames)
                draw(frame);
        }
    }
}
