using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;
using Xanadu.Ui.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Model.Editor;
using Xanadu.Drawers.Playfields;

namespace Xanadu.Drawers.Editor;

/// <summary>
/// Draws one editor frame: the field, the notes on it, the selection, the
/// ghost, the chrome and the timeline. A group of small systems, so the F3 overlay and the
/// frame autopsy time each part on its own: <c>Frame</c> clears the canvas and
/// reads each editor's frame, then one system per drawer draws it, in stacking order.
/// </summary>
public sealed class EditorPlayfieldDrawSystem : SystemGroup
{
    private readonly IRectCanvas _canvas;
    private readonly Handle _editor;

    /// <summary>This frame's editors (one in practice), read by <see cref="FrameSystem"/> for the drawing systems after it.</summary>
    private readonly List<EditorFrame> _frames = new();

    /// <summary>A clock that runs whether or not the song plays.</summary>
    private float _seconds;

    public EditorPlayfieldDrawSystem(EntityStore entities, IRectCanvas canvas, Handle editor) : base("EditorPlayfieldDraw")
    {
        _canvas = canvas;
        _editor = editor;

        var surface = new EditorDrawSurface(canvas, editor, entities);
        var field = new EditorFieldDrawer(surface);
        var items = new EditorItemsDrawer(surface);
        var ghost = new EditorGhostDrawer(surface);
        var selection = new EditorSelectionDrawer(surface);
        var chrome = new EditorChromeDrawer(surface);
        var timeline = new EditorTimelineDrawer(surface);

        // Stacking order: each system draws over the ones before it.
        Add(new FrameSystem(this));
        Add(new DrawStep("Field", _frames, field.Draw));
        Add(new DrawStep("PreviewTrace", _frames, frame =>
        {
            if (frame.Entity.TryGetComponent(out BodyTrace trace))
                PlayfieldRenderer.DrawBodyTrace(canvas, editor, trace.Points, frame.View);
        }));
        Add(new DrawStep("Items", _frames, frame => items.Draw(frame, null)));
        Add(new DrawStep("Ghost", _frames, ghost.Draw));
        Add(new DrawStep("SelectionOutlines", _frames, frame => selection.DrawOutlines(frame.View, frame.Selection)));
        // The chrome goes over the field, so a panned or zoomed field stays out of it.
        Add(new DrawStep("Chrome", _frames, frame =>
        {
            chrome.DrawMasks(frame.View, EditorViewport.Area(frame.View.ScreenSize));
            chrome.Draw(frame.View);
        }));
        Add(new DrawStep("Timeline", _frames, timeline.Draw));
        Add(new DrawStep("SelectionBox", _frames, frame => selection.DrawBox(frame.Box)));
    }

    /// <summary>Erases what the editor last drew, so its timeline does not stay on top of the next scene.</summary>
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

            // One editor entity in practice.
            foreach (Entity entity in Query.Entities)
            {
                owner._frames.Add(new EditorFrame(
                    entity,
                    entity.GetComponent<PlayfieldView>(),
                    entity.GetComponent<Playfield>(),
                    entity.GetComponent<Playhead>(),
                    entity.GetComponent<NoteSelection>(),
                    entity.TryGetComponent(out TimelineView timelineView) ? timelineView : new TimelineView(1f),
                    entity.TryGetComponent(out WaveformPreview waveform) ? waveform : default,
                    entity.TryGetComponent(out UiMarqueeBox marquee) ? marquee : default,
                    owner._seconds));
            }
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
