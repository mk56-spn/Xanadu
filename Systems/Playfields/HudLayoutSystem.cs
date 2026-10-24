using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Canvas;
using Writer.Systems.Input;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

/// <summary>Applies saved HUD positions/visibility and edits them in play mode.</summary>
public sealed class HudLayoutSystem(
    IRectCanvas canvas,
    IPointerInputSource pointer,
    IKeyboardState keyboard,
    HudLayoutSettings settings,
    Rid overlay = default) : QuerySystem<HudLayout, PlayfieldView>
{
    private bool _wasEditPressed;
    private bool _wasSecondaryPressed;
    private HudItemLayout? _dragging;
    private Vector2 _dragStartPointer;
    private Vector2 _dragStartOffset;

    protected override void OnUpdate()
    {
        bool editPressed = keyboard.IsPressed(Key.F7);
        if (editPressed && !_wasEditPressed)
        {
            Query.ForEachEntity((ref layout, ref _, _) =>
                layout.Editing = !layout.Editing);
        }

        _wasEditPressed = editPressed;

        CommandBuffer buffer = CommandBuffer;
        canvas.Clear(overlay);

        Query.ForEachEntity((ref layout, ref view, entity) =>
        {
            ApplyLayout(ref layout, view, entity, buffer);
            if (layout.Editing)
                DrawEditOverlay(layout);
        });
    }

    private void DrawEditOverlay(HudLayout layout)
    {
        if (!overlay.IsValid)
            return;

        foreach (HudItemLayout item in layout.Items)
        {
            var bounds = new Rect2(
                item.Bounds.Position + item.Offset,
                item.Bounds.Size);
            Color fill = new(0.15f, 0.85f, 1f, 0.08f);
            Color border = new(0.35f, 0.95f, 1f, 0.9f);

            canvas.DrawRect(overlay, bounds, fill);
            canvas.DrawLine(overlay, bounds.Position,
                new Vector2(bounds.End.X, bounds.Position.Y), border, 1);
            canvas.DrawLine(overlay,
                new Vector2(bounds.End.X, bounds.Position.Y),
                bounds.End, border, 1);
            canvas.DrawLine(overlay, bounds.End,
                new Vector2(bounds.Position.X, bounds.End.Y), border, 1);
            canvas.DrawLine(overlay,
                new Vector2(bounds.Position.X, bounds.End.Y),
                bounds.Position, border, 1);
            canvas.DrawTextLeft(
                overlay,
                new Rect2(
                    bounds.Position + new Vector2(4, -18),
                    new Vector2(180, 18)),
                item.Id,
                border);
        }

        canvas.DrawRect(
            overlay,
            new Rect2(16, 16, 430, 28),
            new Color(0.03f, 0.05f, 0.09f, 0.92f));
        canvas.DrawTextLeft(
            overlay,
            new Rect2(24, 20, 414, 20),
            "HUD EDIT - DRAG TO MOVE - RIGHT CLICK VISIBILITY - F7 DONE",
            new Color(0.35f, 0.95f, 1f));
    }

    private void ApplyLayout(
        ref HudLayout layout,
        PlayfieldView view,
        Entity playfield,
        CommandBuffer buffer)
    {
        foreach (HudItemLayout item in layout.Items)
        {
            if (!item.Initialized)
            {
                item.Initialized = true;
                if (settings.TryGet(item.Id, out HudLayoutSettings.HudLayoutEntry entry))
                {
                    item.Offset = new Vector2(
                        entry.X * view.ScreenSize.X,
                        entry.Y * view.ScreenSize.Y) - item.Anchor;
                    item.Visible = entry.Visible;
                }
            }

            ApplyMembers(item, ref layout, playfield, buffer);
        }

        HandleEditing(ref layout, view, playfield, buffer);
    }

    private void ApplyMembers(
        HudItemLayout item,
        ref HudLayout layout,
        Entity playfield,
        CommandBuffer buffer)
    {
        for (int i = 0; i < item.Members.Count; i++)
        {
            Entity member = item.Members[i];
            if (member.IsNull)
                continue;

            if (member.HasComponent<Position>())
            {
                ref Position position = ref member.GetComponent<Position>();
                position.Value = item.BasePositions[i] + item.Offset;
                buffer.AddTag<Moved>(member.Id);
            }

            if (member.HasComponent<VisualRid>())
            {
                canvas.SetVisible(
                    member.GetComponent<VisualRid>().Value,
                    item.Visible || layout.Editing);
            }
        }

        if (item.Id == "judgement" && playfield.HasComponent<ScoreState>())
        {
            playfield.GetComponent<ScoreState>().HudOffset = item.Offset;
        }

        if (item.Id == "error_bar" && playfield.HasComponent<ErrorBar>())
        {
            ref ErrorBar bar = ref playfield.GetComponent<ErrorBar>();
            bar.CenterX = bar.BaseCenterX + item.Offset.X;
            bar.Y = bar.BaseY + item.Offset.Y;
        }

        if (item.Id == "key_trail" && playfield.HasComponent<KeyTrail>())
        {
            ref KeyTrail trail = ref playfield.GetComponent<KeyTrail>();
            trail.Offset = item.Offset;
            trail.Visible = item.Visible;
        }
    }

    private void HandleEditing(
        ref HudLayout layout,
        PlayfieldView view,
        Entity playfield,
        CommandBuffer buffer)
    {
        if (!layout.Editing)
        {
            _dragging = null;
            return;
        }

        PointerInput input = pointer.Read();
        if (!input.Pressed)
        {
            if (_dragging is not null)
                Save(_dragging, view);

            _dragging = null;
            _wasSecondaryPressed = input.SecondaryPressed;
            return;
        }

        if (input.SecondaryPressed && !_wasSecondaryPressed)
        {
            HudItemLayout? hit = HitTest(layout, input.Position);
            if (hit is not null)
            {
                hit.Visible = !hit.Visible;
                Save(hit, view);
            }
        }

        _wasSecondaryPressed = input.SecondaryPressed;

        if (_dragging is null)
        {
            HudItemLayout? hit = HitTest(layout, input.Position);
            if (hit is not null)
            {
                _dragging = hit;
                _dragStartPointer = input.Position;
                _dragStartOffset = hit.Offset;
            }
        }

        if (_dragging is not null)
        {
            _dragging.Offset = _dragStartOffset + (input.Position - _dragStartPointer);
            ApplyMembers(_dragging, ref layout, playfield, buffer);
        }
    }

    private static HudItemLayout? HitTest(HudLayout layout, Vector2 position)
    {
        for (int i = layout.Items.Count - 1; i >= 0; i--)
        {
            HudItemLayout item = layout.Items[i];
            var bounds = new Rect2(
                item.Bounds.Position + item.Offset,
                item.Bounds.Size);
            if (bounds.HasPoint(position))
                return item;
        }

        return null;
    }

    private void Save(HudItemLayout item, PlayfieldView view)
    {
        Vector2 anchor = item.Anchor + item.Offset;
        settings.Set(
            item.Id,
            anchor.X / Math.Max(1f, view.ScreenSize.X),
            anchor.Y / Math.Max(1f, view.ScreenSize.Y),
            item.Visible);
    }
}
