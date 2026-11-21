using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Xanadu.Storage.Settings;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Rendering;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Play;

// Reads the pointer outside UI (BannedSymbols.txt): the HUD editor draws its own
// panels on the overlay canvas; still to port onto the spec tree.
#pragma warning disable RS0030

/// <summary>
/// Lays out the play HUD from the saved layout, and edits it in play (F7), as
/// osu!'s skin editor does:
/// <list type="bullet">
/// <item>an element list to show and hide elements and pick them;</item>
/// <item>click, shift-click, box and Ctrl+A selection, Esc to clear;</item>
/// <item>dragging moves the selection, snapping to the screen's edges and
/// centre and to other elements (Alt to place freely); arrows nudge (Shift 10 px);</item>
/// <item>a corner handle scales and a top handle turns the selection about
/// each element's origin (Shift turns in 15° steps);</item>
/// <item>a settings panel for the selection: anchor and origin (nine points
/// each), scale and turn sliders, mirroring across and down (H, V), shown, reset;</item>
/// <item>Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z) undo and redo; Revert goes back to
/// how it was when editing began, Reset all to the built-in layout.</item>
/// </list>
/// The layout saves when editing ends (F7 or Done) or on Ctrl+S.
/// </summary>
public sealed class HudLayoutSystem(
    IRectCanvas canvas,
    IPointerInputSource pointer,
    IKeyboardState keyboard,
    HudLayoutSettings settings,
    Handle overlay = default) : QuerySystem<HudLayout, PlayfieldView>
{
    private const float SnapPixels = 8f;
    private const float HandleSize = 12f;
    private const float RotateReach = 30f;
    private const float MinScale = 0.25f, MaxScale = 4f;

    private enum Drag { None, Move, Box, Scale, Rotate, ScaleSlider, TurnSlider }

    private readonly KeyPress _keys = new();
    private readonly List<HudItemLayout> _selected = new();
    private readonly List<HudItemState[]> _undo = new();
    private readonly List<HudItemState[]> _redo = new();
    private HudItemState[]? _opened;
    private readonly List<Button> _buttons = new();
    private readonly List<(Vector2 From, Vector2 To)> _guides = new();

    private bool _wasEditPressed;
    private bool _wasPressed;
    private bool _wasSecondary;
    private Drag _drag;
    private Vector2 _dragStart;
    private Vector2 _pointer;
    private Vector2 _pivot;
    private HudItemState[] _dragFrom = [];
    private bool _dragChanged;

    /// <summary>A panel button: its box, its label, whether it is on, and what it does.</summary>
    private readonly record struct Button(Rect2 Box, string Label, bool On, Action Do, bool Small = false);

    protected override void OnUpdate()
    {
        bool editPressed = keyboard.IsPressed(GameDebugKeys.HudLayout);
        bool toggle = editPressed && !_wasEditPressed;
        _wasEditPressed = editPressed;
        canvas.Clear(overlay);
        Query.EachEntity(new LayOut(this, Query.Store, CommandBuffer, toggle));
    }

    private readonly record struct LayOut(HudLayoutSystem System, EntityStore Store, CommandBuffer Buffer, bool Toggle) : IEachEntity<HudLayout, PlayfieldView>
    {
        public void Execute(ref HudLayout layout, ref PlayfieldView view, int id)
        {
            Entity entity = Store.GetEntityById(id);
            System.Load(ref layout, view);
            if (Toggle)
                System.SetEditing(ref layout, view, !layout.Editing);

            if (layout.Editing)
                System.Edit(ref layout, view);
            if (System._doneRequested)
            {
                System._doneRequested = false;
                System.SetEditing(ref layout, view, false);
            }

            foreach (HudItemLayout item in layout.Items)
                System.ApplyMembers(item, layout.Editing, entity, Buffer);

            if (layout.Editing)
                System.Draw(layout, view);
        }
    }

    // --- the saved layout ----------------------------------------------------

    /// <summary>Each element's built-in anchor and origin, then its saved layout over them, once.</summary>
    private void Load(ref HudLayout layout, PlayfieldView view)
    {
        foreach (HudItemLayout item in layout.Items)
        {
            if (item.Initialized)
                continue;

            item.Initialized = true;
            item.Anchor = HudPoints.Closest(item.Bounds, view.ScreenSize);
            item.Origin = item.Anchor;
            if (!settings.TryGet(item.Id, out HudLayoutSettings.HudLayoutEntry entry))
                continue;

            item.Anchor = entry.Anchor;
            item.Origin = entry.Origin;
            item.Scale = entry.Scale;
            item.Rotation = entry.Rotation;
            item.FlipX = entry.FlipX;
            item.FlipY = entry.FlipY;
            item.Visible = entry.Visible;
            Vector2 at = HudPoints.On(Screen(view), item.Anchor) + new Vector2(entry.X, entry.Y);
            item.Offset = at - HudPoints.On(item.Bounds, item.Origin);
        }
    }

    private static Rect2 Screen(PlayfieldView view) => new(Vector2.Zero, view.ScreenSize);

    /// <summary>Saves every element: where its origin sits from its anchor, and the rest of its pose.</summary>
    private void SaveAll(HudLayout layout, PlayfieldView view)
    {
        foreach (HudItemLayout item in layout.Items)
        {
            if (item.State == Default(item, view))
            {
                settings.Remove(item.Id);
                continue;
            }

            Vector2 from = item.OriginPoint - HudPoints.On(Screen(view), item.Anchor);
            settings.Set(item.Id, new HudLayoutSettings.HudLayoutEntry(
                (byte)item.Anchor, (byte)item.Origin, from.X, from.Y, item.Scale, item.Rotation, item.Visible, item.FlipX, item.FlipY));
        }

        settings.Save();
    }

    private static HudItemState Default(HudItemLayout item, PlayfieldView view)
    {
        int point = HudPoints.Closest(item.Bounds, view.ScreenSize);
        return new HudItemState(Vector2.Zero, 1f, 0f, false, false, true, point, point);
    }

    private void SetEditing(ref HudLayout layout, PlayfieldView view, bool editing)
    {
        if (layout.Editing == editing)
            return;

        layout.Editing = editing;
        _drag = Drag.None;
        _selected.Clear();
        _undo.Clear();
        _redo.Clear();
        if (editing)
            _opened = Capture(layout);
        else
            SaveAll(layout, view);
    }

    // --- applying it ---------------------------------------------------------

    /// <summary>Puts an element's members where its layout says: moved, posed about its origin, shown or hidden.</summary>
    private void ApplyMembers(HudItemLayout item, bool editing, Entity playfield, CommandBuffer buffer)
    {
        Vector2 origin = item.OriginPoint;
        for (int i = 0; i < item.Members.Count; i++)
        {
            Entity member = item.Members[i];
            if (member.IsNull)
                continue;

            Vector2 at = item.BasePositions[i] + item.Offset;
            if (member.HasComponent<Position>())
            {
                ref Position position = ref member.GetComponent<Position>();
                if (position.Value != at)
                {
                    position.Value = at;
                    buffer.AddTag<Moved>(member.Id);
                }
            }

            if (item.Posed)
                buffer.AddComponent(member.Id, new UiPose(origin - at, item.ScaleVector, item.Rotation));
            else if (member.HasComponent<UiPose>())
                buffer.RemoveComponent<UiPose>(member.Id);

            bool shown = item.Visible || editing;
            if (shown == member.Tags.Has<CanvasHidden>())
            {
                if (shown)
                    buffer.RemoveTag<CanvasHidden>(member.Id);
                else
                    buffer.AddTag<CanvasHidden>(member.Id);
            }
        }

        if (item.Id == "error_bar" && playfield.HasComponent<ErrorBar>())
        {
            ref ErrorBar bar = ref playfield.GetComponent<ErrorBar>();
            bar.CenterX = bar.BaseCenterX + item.Offset.X;
            bar.Y = bar.BaseY + item.Offset.Y;
        }
    }

    // --- editing -------------------------------------------------------------

    private void Edit(ref HudLayout layout, PlayfieldView view)
    {
        HudLayout current = layout;
        BuildPanels(current, view);
        Keys(ref layout, view);
        if (!layout.Editing)
            return;

        PointerInput input = pointer.Read();
        _pointer = input.Position;
        bool pressed = input.Pressed && !_wasPressed;
        bool released = !input.Pressed && _wasPressed;
        bool secondary = input.SecondaryPressed && !_wasSecondary;
        _wasPressed = input.Pressed;
        _wasSecondary = input.SecondaryPressed;
        bool alt = keyboard.IsPressed(Key.Alt);
        _guides.Clear();

        if (secondary && TopmostAt(current, input.Position) is { } toggled)
        {
            Change(current, () => toggled.Visible = !toggled.Visible);
            return;
        }

        if (pressed)
            Press(current, view, input);
        else if (input.Pressed)
            Hold(current, view, input, alt);
        else if (released)
            Release(current);
    }

    private void Press(HudLayout layout, PlayfieldView view, PointerInput input)
    {
        Vector2 at = input.Position;
        foreach (Button button in _buttons)
        {
            if (button.Box.HasPoint(at))
            {
                button.Do();
                return;
            }
        }

        if (_selected.Count > 0 && SliderAt(view, at) is { } slider)
        {
            Begin(layout, slider, at);
            Hold(layout, view, input, alt: false);
            return;
        }

        if (OverPanel(view, at, layout))
            return;

        if (_selected.Count > 0)
        {
            Rect2 box = SelectionBox();
            if (ScaleHandle(box).HasPoint(at))
            {
                Begin(layout, Drag.Scale, at);
                _pivot = box.GetCenter();
                return;
            }

            if (Vector2.Distance(RotateHandle(box), at) <= HandleSize)
            {
                Begin(layout, Drag.Rotate, at);
                _pivot = box.GetCenter();
                return;
            }
        }

        if (TopmostAt(layout, at) is { } hit)
        {
            if (input.Shift)
            {
                if (!_selected.Remove(hit))
                    _selected.Add(hit);
                return;
            }

            if (!_selected.Contains(hit))
            {
                _selected.Clear();
                _selected.Add(hit);
            }

            Begin(layout, Drag.Move, at);
            return;
        }

        if (!input.Shift)
            _selected.Clear();
        Begin(layout, Drag.Box, at);
    }

    private void Begin(HudLayout layout, Drag drag, Vector2 at)
    {
        _drag = drag;
        _dragStart = at;
        _dragFrom = Capture(layout);
        _dragChanged = false;
    }

    private void Hold(HudLayout layout, PlayfieldView view, PointerInput input, bool alt)
    {
        Vector2 delta = input.Position - _dragStart;
        switch (_drag)
        {
            case Drag.Move:
                if (!alt)
                    delta = Snap(layout, view, delta);
                foreach (HudItemLayout item in _selected)
                    item.Offset = StartOf(layout, item).Offset + delta;
                _dragChanged |= delta != Vector2.Zero;
                break;

            case Drag.Scale:
            {
                float from = Vector2.Distance(_dragStart, _pivot), to = Vector2.Distance(input.Position, _pivot);
                float factor = from > 1f ? to / from : 1f;
                foreach (HudItemLayout item in _selected)
                    item.Scale = Math.Clamp(StartOf(layout, item).Scale * factor, MinScale, MaxScale);
                _dragChanged = true;
                break;
            }

            case Drag.Rotate:
            {
                float turn = Angle(input.Position - _pivot) - Angle(_dragStart - _pivot);
                foreach (HudItemLayout item in _selected)
                {
                    float angle = StartOf(layout, item).Rotation + turn;
                    if (input.Shift)
                        angle = MathF.Round(angle / (MathF.PI / 12f)) * (MathF.PI / 12f);
                    item.Rotation = Wrap(angle);
                }

                _dragChanged = true;
                break;
            }

            case Drag.ScaleSlider:
            {
                float scale = MinScale + (MaxScale - MinScale) * SliderValue(view, 0, input.Position);
                foreach (HudItemLayout item in _selected)
                    item.Scale = MathF.Round(scale * 20f) / 20f;
                _dragChanged = true;
                break;
            }

            case Drag.TurnSlider:
            {
                float degrees = MathF.Round((SliderValue(view, 1, input.Position) * 360f - 180f) / 5f) * 5f;
                foreach (HudItemLayout item in _selected)
                    item.Rotation = degrees * MathF.PI / 180f;
                _dragChanged = true;
                break;
            }
        }
    }

    private void Release(HudLayout layout)
    {
        if (_drag == Drag.Box)
        {
            Rect2 box = BoxRect();
            foreach (HudItemLayout item in layout.Items)
            {
                if (box.Size.X > 2f && box.Intersects(item.Shown()) && !_selected.Contains(item))
                    _selected.Add(item);
            }
        }
        else if (_drag != Drag.None && _dragChanged)
            PushUndo(_dragFrom);

        _drag = Drag.None;
    }

    private HudItemState StartOf(HudLayout layout, HudItemLayout item) => _dragFrom[layout.Items.IndexOf(item)];

    /// <summary>
    /// Pulls a move onto the nearest line within reach, across and down apart:
    /// the screen's edges and centre, and every other shown element's edges and
    /// centre; the lines it lands on are drawn as guides.
    /// </summary>
    private Vector2 Snap(HudLayout layout, PlayfieldView view, Vector2 delta)
    {
        Rect2? moving = null;
        foreach (HudItemLayout item in _selected)
        {
            HudItemState was = StartOf(layout, item);
            Rect2 shown = item.Shown();
            shown.Position += was.Offset - item.Offset + delta;
            moving = moving is { } union ? union.Merge(shown) : shown;
        }

        if (moving is not { } box)
            return delta;

        var xs = new List<float> { 0f, view.ScreenSize.X / 2f, view.ScreenSize.X };
        var ys = new List<float> { 0f, view.ScreenSize.Y / 2f, view.ScreenSize.Y };
        foreach (HudItemLayout other in layout.Items)
        {
            if (_selected.Contains(other) || !other.Visible)
                continue;
            Rect2 r = other.Shown();
            xs.AddRange([r.Position.X, r.GetCenter().X, r.End.X]);
            ys.AddRange([r.Position.Y, r.GetCenter().Y, r.End.Y]);
        }

        float[] edgesX = [box.Position.X, box.GetCenter().X, box.End.X];
        float[] edgesY = [box.Position.Y, box.GetCenter().Y, box.End.Y];
        if (Nearest(edgesX, xs) is (float shiftX, float lineX))
        {
            delta.X += shiftX;
            _guides.Add((new Vector2(lineX, 0f), new Vector2(lineX, view.ScreenSize.Y)));
        }

        if (Nearest(edgesY, ys) is (float shiftY, float lineY))
        {
            delta.Y += shiftY;
            _guides.Add((new Vector2(0f, lineY), new Vector2(view.ScreenSize.X, lineY)));
        }

        return delta;
    }

    private static (float Shift, float Line)? Nearest(float[] edges, List<float> lines)
    {
        (float, float)? best = null;
        float bestGap = SnapPixels;
        foreach (float edge in edges)
        {
            foreach (float line in lines)
            {
                float gap = MathF.Abs(line - edge);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = (line - edge, line);
                }
            }
        }

        return best;
    }

    private void Keys(ref HudLayout layout, PlayfieldView view)
    {
        bool ctrl = keyboard.IsPressed(Key.Ctrl);
        bool shift = keyboard.IsPressed(Key.Shift);
        HudLayout current = layout;
        if (Pressed(Key.Z) && ctrl)
        {
            if (shift)
                Redo(current);
            else
                Undo(current);
        }

        if (Pressed(Key.Y) && ctrl)
            Redo(current);
        if (Pressed(Key.A) && ctrl)
        {
            _selected.Clear();
            _selected.AddRange(current.Items);
        }

        if (Pressed(Key.S) && ctrl)
            SaveAll(current, view);
        if (Pressed(Key.Escape))
            _selected.Clear();
        if (Pressed(Key.Delete) && _selected.Count > 0)
            Change(current, () => _selected.ForEach(item => item.Visible = false));
        if (Pressed(Key.H) && !ctrl && _selected.Count > 0)
            Change(current, () => _selected.ForEach(item => item.FlipX = !item.FlipX));
        if (Pressed(Key.V) && !ctrl && _selected.Count > 0)
            Change(current, () => _selected.ForEach(item => item.FlipY = !item.FlipY));

        float step = shift ? 10f : 1f;
        Vector2 nudge = Vector2.Zero;
        if (Pressed(Key.Left)) nudge.X -= step;
        if (Pressed(Key.Right)) nudge.X += step;
        if (Pressed(Key.Up)) nudge.Y -= step;
        if (Pressed(Key.Down)) nudge.Y += step;
        if (nudge != Vector2.Zero && _selected.Count > 0)
            Change(current, () => _selected.ForEach(item => item.Offset += nudge));
    }

    private bool Pressed(Key key) => _keys.Started(key, keyboard.IsPressed(key));

    // --- undo ----------------------------------------------------------------

    private static HudItemState[] Capture(HudLayout layout)
    {
        var states = new HudItemState[layout.Items.Count];
        for (int i = 0; i < states.Length; i++)
            states[i] = layout.Items[i].State;
        return states;
    }

    private static void Restore(HudLayout layout, HudItemState[] states)
    {
        for (int i = 0; i < states.Length && i < layout.Items.Count; i++)
            layout.Items[i].Apply(states[i]);
    }

    /// <summary>Makes a change as one undo step.</summary>
    private void Change(HudLayout layout, Action change)
    {
        HudItemState[] before = Capture(layout);
        change();
        PushUndo(before);
    }

    private void PushUndo(HudItemState[] before)
    {
        _undo.Add(before);
        _redo.Clear();
    }

    private void Undo(HudLayout layout)
    {
        if (_undo.Count == 0)
            return;
        _redo.Add(Capture(layout));
        Restore(layout, _undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    private void Redo(HudLayout layout)
    {
        if (_redo.Count == 0)
            return;
        _undo.Add(Capture(layout));
        Restore(layout, _redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }

    // --- panels --------------------------------------------------------------

    private const float Pad = 12f, Row = 30f, PanelWidth = 260f;

    private static Rect2 TopBar(PlayfieldView view) => new(16f, 16f, view.ScreenSize.X - 32f, 40f);

    private static Rect2 Toolbox(HudLayout layout) => new(16f, 68f, PanelWidth, Pad * 2f + 24f + layout.Items.Count * Row);

    private static Rect2 SettingsPanel(PlayfieldView view) => new(view.ScreenSize.X - PanelWidth - 16f, 68f, PanelWidth, 372f);

    private bool OverPanel(PlayfieldView view, Vector2 at, HudLayout layout) =>
        TopBar(view).HasPoint(at) || Toolbox(layout).HasPoint(at) || (_selected.Count > 0 && SettingsPanel(view).HasPoint(at));

    /// <summary>Lays out every panel button for this frame: the top bar, the element list and, with a selection, its settings.</summary>
    private void BuildPanels(HudLayout layout, PlayfieldView view)
    {
        _buttons.Clear();
        Rect2 bar = TopBar(view);
        float x = bar.End.X - Pad;
        foreach ((string label, Action act) in new (string, Action)[]
        {
            ("Done", () => SetEditingFromButton(layout, view)),
            ("Reset all", () => Change(layout, () => layout.Items.ForEach(item => item.Apply(Default(item, view))))),
            ("Revert", () => { if (_opened is { } opened) Change(layout, () => Restore(layout, opened)); }),
            ("Redo", () => Redo(layout)),
            ("Undo", () => Undo(layout)),
        })
        {
            float width = 96f;
            x -= width;
            _buttons.Add(new Button(new Rect2(x, bar.Position.Y + 6f, width - 8f, bar.Size.Y - 12f), label, false, act));
        }

        Rect2 box = Toolbox(layout);
        float y = box.Position.Y + Pad + 24f;
        foreach (HudItemLayout item in layout.Items)
        {
            HudItemLayout row = item;
            _buttons.Add(new Button(new Rect2(box.Position.X + Pad, y, 28f, Row - 6f), "", row.Visible,
                () => Change(layout, () => row.Visible = !row.Visible), Small: true));
            _buttons.Add(new Button(new Rect2(box.Position.X + Pad + 36f, y, box.Size.X - Pad * 2f - 36f, Row - 6f), Name(row), _selected.Contains(row),
                () => { _selected.Clear(); _selected.Add(row); }));
            y += Row;
        }

        if (_selected.Count == 0)
            return;

        Rect2 panel = SettingsPanel(view);
        float left = panel.Position.X + Pad;
        float top = panel.Position.Y + Pad + 28f;
        PointGrid(layout, left, top + 20f, true);
        PointGrid(layout, left + 130f, top + 20f, false);

        float toggles = top + 20f + 3f * 30f + 150f;
        float half = (panel.Size.X - Pad * 3f) / 2f;
        bool flipX = _selected.TrueForAll(item => item.FlipX), flipY = _selected.TrueForAll(item => item.FlipY);
        bool shown = _selected.TrueForAll(item => item.Visible);
        _buttons.Add(new Button(new Rect2(left, toggles, half, 26f), "Mirror across (H)", flipX,
            () => Change(layout, () => _selected.ForEach(item => item.FlipX = !flipX))));
        _buttons.Add(new Button(new Rect2(left + half + Pad, toggles, half, 26f), "Mirror down (V)", flipY,
            () => Change(layout, () => _selected.ForEach(item => item.FlipY = !flipY))));
        _buttons.Add(new Button(new Rect2(left, toggles + 34f, half, 26f), "Shown", shown,
            () => Change(layout, () => _selected.ForEach(item => item.Visible = !shown))));
        _buttons.Add(new Button(new Rect2(left + half + Pad, toggles + 34f, half, 26f), "Reset", false,
            () => Change(layout, () => _selected.ForEach(item => item.Apply(Default(item, view))))));
    }

    /// <summary>Done: editing ends where the playfield's own layout is in hand, after this frame's edits.</summary>
    private void SetEditingFromButton(HudLayout layout, PlayfieldView view) => _doneRequested = true;

    private bool _doneRequested;

    /// <summary>A 3 × 3 grid of point buttons for the selection's anchor or origin.</summary>
    private void PointGrid(HudLayout layout, float left, float top, bool anchor)
    {
        for (int i = 0; i < 9; i++)
        {
            int point = i;
            bool on = _selected.TrueForAll(item => (anchor ? item.Anchor : item.Origin) == point);
            var box = new Rect2(left + i % 3 * 34f, top + i / 3 * 30f, 28f, 24f);
            _buttons.Add(new Button(box, "", on, () => Change(layout, () =>
            {
                foreach (HudItemLayout item in _selected)
                {
                    if (anchor)
                        item.Anchor = point;
                    else
                        item.Origin = point;
                }
            }), Small: true));
        }
    }

    private Rect2 SliderTrack(PlayfieldView view, int index)
    {
        Rect2 panel = SettingsPanel(view);
        float top = panel.Position.Y + Pad + 28f + 20f + 3f * 30f + 30f + index * 56f;
        return new Rect2(panel.Position.X + Pad, top + 22f, panel.Size.X - Pad * 2f, 14f);
    }

    private Drag? SliderAt(PlayfieldView view, Vector2 at)
    {
        if (SliderTrack(view, 0).Grow(6f).HasPoint(at))
            return Drag.ScaleSlider;
        if (SliderTrack(view, 1).Grow(6f).HasPoint(at))
            return Drag.TurnSlider;
        return null;
    }

    private float SliderValue(PlayfieldView view, int index, Vector2 at)
    {
        Rect2 track = SliderTrack(view, index);
        return Math.Clamp((at.X - track.Position.X) / track.Size.X, 0f, 1f);
    }

    private static string Name(HudItemLayout item) => item.Id.Replace('_', ' ');

    // --- drawing -------------------------------------------------------------

    private static readonly Color Edge = Palette.Primary;
    private static readonly Color Muted = Palette.PrimarySoft;

    private void Draw(HudLayout layout, PlayfieldView view)
    {
        if (!overlay.IsValid)
            return;

        foreach (HudItemLayout item in layout.Items)
            DrawItem(item, view);

        foreach ((Vector2 from, Vector2 to) in _guides)
            canvas.DrawLine(overlay, from, to, Palette.Accent, 1f);

        if (_selected.Count > 0)
            DrawSelection(view);

        if (_drag == Drag.Box)
        {
            Rect2 box = BoxRect();
            canvas.DrawRect(overlay, box, new Color(Edge.R, Edge.G, Edge.B, 0.12f));
            canvas.DrawBorder(overlay, box, Edge, 1f);
        }

        DrawPanels(layout, view);
    }

    private void DrawItem(HudItemLayout item, PlayfieldView view)
    {
        bool selected = _selected.Contains(item);
        Color line = !item.Visible ? Palette.Danger : selected ? Palette.TextBright : Muted;
        Vector2[] corners = item.Corners();
        canvas.DrawPolygon(overlay, corners, new Color(line.R, line.G, line.B, selected ? 0.14f : 0.06f));
        Span<Vector2> outline = stackalloc Vector2[5];
        corners.CopyTo(outline);
        outline[4] = corners[0];
        canvas.DrawPolyline(overlay, outline, line, selected ? 2f : 1f);
        string label = item.Visible ? Name(item) : Name(item) + " (hidden)";
        Rect2 shown = item.Shown();
        canvas.DrawTextLeft(overlay, new Rect2(shown.Position + new Vector2(2f, -20f), new Vector2(260f, 18f)), label, line);
    }

    private void DrawSelection(PlayfieldView view)
    {
        foreach (HudItemLayout item in _selected)
        {
            // The anchor on the screen, the origin on the element, and the line between them.
            Vector2 anchor = HudPoints.On(Screen(view), item.Anchor);
            Vector2 origin = item.OriginPoint;
            canvas.DrawLine(overlay, anchor, origin, new Color(Palette.Accent.R, Palette.Accent.G, Palette.Accent.B, 0.6f), 1f);
            Span<Vector2> diamond = [anchor + new Vector2(0f, -7f), anchor + new Vector2(7f, 0f), anchor + new Vector2(0f, 7f), anchor + new Vector2(-7f, 0f)];
            canvas.DrawPolygon(overlay, diamond, Palette.Accent);
            canvas.DrawCircle(overlay, origin, 5f, Palette.TextBright);
            canvas.DrawCircleOutline(overlay, origin, 8f, Palette.Accent, 2f);
        }

        Rect2 box = SelectionBox();
        canvas.DrawBorder(overlay, box.Grow(4f), new Color(1f, 1f, 1f, 0.35f), 1f);
        Rect2 handle = ScaleHandle(box);
        canvas.DrawRect(overlay, handle, Palette.TextBright);
        canvas.DrawBorder(overlay, handle, Edge, 2f);
        Vector2 turn = RotateHandle(box);
        canvas.DrawLine(overlay, new Vector2(box.GetCenter().X, box.Position.Y - 4f), turn, Palette.TextBright, 1f);
        canvas.DrawCircle(overlay, turn, HandleSize * 0.6f, Palette.TextBright);
        canvas.DrawCircleOutline(overlay, turn, HandleSize * 0.6f, Edge, 2f);
    }

    private void DrawPanels(HudLayout layout, PlayfieldView view)
    {
        Color back = new(0.03f, 0.05f, 0.09f, 0.94f);
        Rect2 bar = TopBar(view);
        canvas.DrawRect(overlay, bar, back);
        canvas.DrawBorder(overlay, bar, Muted, 1f);
        canvas.DrawTextLeft(overlay, new Rect2(bar.Position + new Vector2(Pad, 11f), new Vector2(760f, 20f)),
            "HUD EDITOR   drag move · corner scale · top turn · Alt free · arrows nudge · H/V mirror · Del hide · F7 done",
            Palette.Text);

        Rect2 box = Toolbox(layout);
        canvas.DrawRect(overlay, box, back);
        canvas.DrawBorder(overlay, box, Muted, 1f);
        canvas.DrawTextLeft(overlay, new Rect2(box.Position + new Vector2(Pad, Pad), new Vector2(box.Size.X, 20f)), "ELEMENTS", Palette.TextDim);

        if (_selected.Count > 0)
            DrawSettings(view, back);

        foreach (Button button in _buttons)
            DrawButton(button);
    }

    private void DrawSettings(PlayfieldView view, Color back)
    {
        Rect2 panel = SettingsPanel(view);
        canvas.DrawRect(overlay, panel, back);
        canvas.DrawBorder(overlay, panel, Muted, 1f);
        float left = panel.Position.X + Pad;
        float top = panel.Position.Y + Pad;
        string title = _selected.Count == 1 ? Name(_selected[0]).ToUpperInvariant() : $"{_selected.Count} ELEMENTS";
        canvas.DrawTextLeft(overlay, new Rect2(left, top, panel.Size.X, 20f), title, Palette.TextBright);
        canvas.DrawTextLeft(overlay, new Rect2(left, top + 28f, 120f, 18f), "ANCHOR", Palette.TextDim);
        canvas.DrawTextLeft(overlay, new Rect2(left + 130f, top + 28f, 120f, 18f), "ORIGIN", Palette.TextDim);
        if (_selected.Count == 1)
        {
            canvas.DrawTextLeft(overlay, new Rect2(left, top + 28f + 20f + 92f, 240f, 18f),
                $"{HudPoints.Names[_selected[0].Anchor]} / {HudPoints.Names[_selected[0].Origin]}", Palette.TextDim);
        }

        HudItemLayout first = _selected[0];
        DrawSlider(view, 0, $"SCALE  {first.Scale:0.00}×", (first.Scale - MinScale) / (MaxScale - MinScale));
        float degrees = first.Rotation * 180f / MathF.PI;
        DrawSlider(view, 1, $"TURN  {degrees:0}°", (degrees + 180f) / 360f);
    }

    private void DrawSlider(PlayfieldView view, int index, string label, float value)
    {
        Rect2 track = SliderTrack(view, index);
        canvas.DrawTextLeft(overlay, new Rect2(track.Position - new Vector2(0f, 22f), new Vector2(track.Size.X, 18f)), label, Palette.TextDim);
        canvas.DrawRect(overlay, new Rect2(track.Position + new Vector2(0f, 5f), new Vector2(track.Size.X, 4f)), Muted);
        float knob = track.Position.X + track.Size.X * Math.Clamp(value, 0f, 1f);
        canvas.DrawRect(overlay, new Rect2(track.Position + new Vector2(0f, 5f), new Vector2(knob - track.Position.X, 4f)), Edge);
        canvas.DrawCircle(overlay, new Vector2(knob, track.GetCenter().Y), 8f, Palette.TextBright);
    }

    /// <summary>A panel button: muted blue at rest, bright blue when on (an eye or a point lit), its label over it.</summary>
    private void DrawButton(Button button)
    {
        bool hover = button.Box.HasPoint(_pointer);
        Color fill = button.On ? Edge : new Color(Muted.R, Muted.G, Muted.B, hover ? 0.55f : 0.3f);
        canvas.DrawRect(overlay, button.Box, fill);
        if (hover)
            canvas.DrawBorder(overlay, button.Box, Palette.TextBright, 1f);
        if (button.Small && button.Label.Length == 0 && button.Box.Size.X < 40f && button.Box.Size.Y < 30f && button.On)
            canvas.DrawCircle(overlay, button.Box.GetCenter(), 4f, Palette.TextBright);
        if (button.Label.Length > 0)
            canvas.DrawText(overlay, button.Box, button.Label, button.On ? Palette.TextBright : Palette.Text);
    }

    // --- shapes --------------------------------------------------------------

    private HudItemLayout? TopmostAt(HudLayout layout, Vector2 at)
    {
        for (int i = layout.Items.Count - 1; i >= 0; i--)
        {
            if (layout.Items[i].Contains(at))
                return layout.Items[i];
        }

        return null;
    }

    private Rect2 SelectionBox()
    {
        Rect2 box = _selected[0].Shown();
        foreach (HudItemLayout item in _selected)
            box = box.Merge(item.Shown());
        return box;
    }

    private static Rect2 ScaleHandle(Rect2 box) =>
        new(box.End + new Vector2(4f - HandleSize / 2f), new Vector2(HandleSize));

    private static Vector2 RotateHandle(Rect2 box) => new(box.GetCenter().X, box.Position.Y - RotateReach);

    private Rect2 BoxRect()
    {
        Vector2 min = Vector2.Min(_dragStart, _pointer), max = Vector2.Max(_dragStart, _pointer);
        return new Rect2(min, max - min);
    }

    private static float Angle(Vector2 v) => MathF.Atan2(v.Y, v.X);

    private static float Wrap(float radians)
    {
        while (radians > MathF.PI) radians -= MathF.Tau;
        while (radians < -MathF.PI) radians += MathF.Tau;
        return radians;
    }
}
