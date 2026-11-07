using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Draws the key trail into its HUD containers: keycaps light with their key,
/// and each press rises out of its keycap through the lane's history column
/// for <see cref="HistoryWindowMs"/>. Positions are local to the history
/// column, so the trail follows its frame wherever the HUD layout puts it.
/// </summary>
public sealed class PlayKeyTrailHudSystem : QuerySystem<Playhead, KeyTrail, KeyTrailHud>
{
    public static readonly Color IdleKeyFill = new(0.03f, 0.04f, 0.08f, 0.82f);

    private const float HistoryWindowMs = 1_000f;
    private const float SegmentWidthShare = 0.56f;
    private const float CoreWidth = 6f;

    private readonly EntityStore _entities;
    private readonly List<KeyPressTrail>[] _lanes;
    private ArchetypeQuery<KeyPressTrail>? _segments;

    public PlayKeyTrailHudSystem(EntityStore entities)
    {
        _entities = entities;
        _lanes = new List<KeyPressTrail>[KeybindSettings.Actions.Length];
        for (int i = 0; i < _lanes.Length; i++)
            _lanes[i] = new List<KeyPressTrail>();
    }

    protected override void OnUpdate()
    {
        _segments ??= _entities.Query<KeyPressTrail>();
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref playhead, ref trail, ref hud, _) =>
        {
            CollectLanes(playhead.TimeMs);
            for (int lane = 0; lane < _lanes.Length && lane < hud.Keys.Length; lane++)
            {
                KeybindAction action = KeybindSettings.Actions[lane];
                Color color = PlayfieldRenderer.ColorFor(action);
                float intensity = Math.Clamp(trail.Get(action), 0f, 1f);
                UiText.SetFill(hud.Keys[lane], intensity > 0f
                    ? new Color(color.R, color.G, color.B, 0.18f + intensity * 0.45f)
                    : IdleKeyFill, buffer);

                DrawLane(hud, lane, color, playhead.TimeMs, buffer);
            }
        });
    }

    /// <summary>Buckets the presses still inside the window by lane, newest first.</summary>
    private void CollectLanes(long now)
    {
        foreach (List<KeyPressTrail> lane in _lanes)
            lane.Clear();

        long windowStart = now - (long)HistoryWindowMs;
        _segments!.ForEachEntity((ref KeyPressTrail segment, Entity _) =>
        {
            int index = (int)segment.Action;
            long end = segment.EndMs >= 0 ? segment.EndMs : now;
            if (index < 0 || index >= _lanes.Length ||
                end < windowStart || segment.StartMs > now)
            {
                return;
            }

            _lanes[index].Add(segment);
        });

        foreach (List<KeyPressTrail> lane in _lanes)
            lane.Sort(static (a, b) => b.StartMs.CompareTo(a.StartMs));
    }

    private void DrawLane(KeyTrailHud hud, int lane, Color color, long now, CommandBuffer buffer)
    {
        List<KeyPressTrail> segments = _lanes[lane];
        float height = hud.HistorySize.Y;
        float bottom = height / 2;
        long windowStart = now - (long)HistoryWindowMs;

        for (int slot = 0; slot < hud.SlotsPerLane; slot++)
        {
            int index = lane * hud.SlotsPerLane + slot;
            Entity outline = hud.Outlines[index];
            Entity core = hud.Cores[index];
            if (slot >= segments.Count)
            {
                UiLayout.SetHidden(outline, buffer, true);
                UiLayout.SetHidden(core, buffer, true);
                continue;
            }

            KeyPressTrail segment = segments[slot];
            long end = segment.EndMs >= 0 ? segment.EndMs : now;
            float startY = bottom - Rise(now, Math.Max(segment.StartMs, windowStart)) * height;
            float endY = bottom - Rise(now, Math.Max(end, windowStart)) * height;
            float top = Math.Min(startY, endY);
            float span = Math.Max(2f, Math.Abs(endY - startY));
            float age = Rise(now, end);
            float alpha = 0.18f + (1f - age) * 0.62f;
            float centerY = top + span / 2;

            Place(outline,
                new Vector2(0, centerY),
                new Vector2(hud.HistorySize.X * SegmentWidthShare, span),
                new Color(color.R, color.G, color.B, alpha * 0.35f),
                buffer);
            ref UiBorder border = ref outline.GetComponent<UiBorder>();
            var borderColor = new Color(1f, 1f, 1f, alpha * 0.35f);
            if (border.Color != borderColor)
            {
                border.Color = borderColor;
                buffer.AddTag<Dirty>(outline.Id);
            }

            UiLayout.SetHidden(outline, buffer, false);

            bool showCore = span > 5f;
            UiLayout.SetHidden(core, buffer, !showCore);
            if (showCore)
            {
                Place(core,
                    new Vector2(0, centerY),
                    new Vector2(CoreWidth, span - 4f),
                    new Color(color.R, color.G, color.B, alpha),
                    buffer);
            }
        }
    }

    /// <summary>How far up the history column a moment has risen: 0 now, 1 at the window's end.</summary>
    private static float Rise(long now, long at) =>
        Math.Clamp((now - at) / HistoryWindowMs, 0f, 1f);

    private static void Place(Entity entity, Vector2 center, Vector2 size, Color fill, CommandBuffer buffer)
    {
        ref Position position = ref entity.GetComponent<Position>();
        if (position.Value != center)
        {
            position.Value = center;
            buffer.AddTag<Moved>(entity.Id);
        }

        ref Rectangle rectangle = ref entity.GetComponent<Rectangle>();
        var bounds = new Rect2(-size / 2, size);
        if (rectangle.Bounds != bounds)
        {
            rectangle.Bounds = bounds;
            buffer.AddTag<Dirty>(entity.Id);
        }

        UiText.SetFill(entity, fill, buffer);
    }
}
