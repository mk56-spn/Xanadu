using System;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Canvas;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

/// <summary>Shared playfield geometry and drawing used by editor and play modes.</summary>
public static class PlayfieldRenderer
{
    public static readonly Color PlayBackground = new("#090d16");
    public static readonly Color PlayfieldBackground = new("#0f172a");
    public static readonly Color BorderColor = new("#475569");
    public static readonly Color GridColor = new("#273449");
    public static readonly Color AxisColor = new("#64748b");
    public static readonly Color AccentColor = new("#facc15");
    public static readonly Color DimTextColor = new("#94a3b8");
    public static readonly Color NoteColor = new(1f, 0.55f, 0.15f);
    public static readonly Color SelectedNoteColor = new(1f, 0.9f, 0.3f);
    public static readonly Color PlayerColor = new("#e2e8f0");
    public static readonly Color QueueColor = new("#38bdf8");

    public const float MineSize = 18;

    private static readonly Color[] LaneColors =
    {
        new("#22d3ee"),
        new("#3b82f6"),
        new("#8b5cf6"),
        new("#facc15"),
        new("#fb923c"),
        new("#ef4444"),
        new("#ec4899"),
    };

    public const float NoteSize = 24;

    public static Rect2 FieldRect(PlayfieldView view, Playfield playfield) =>
        new(
            view.ScreenOrigin,
            new Vector2(playfield.Width * view.Scale, playfield.Height * view.Scale));

    public static void DrawPlay(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead)
    {
        canvas.DrawRect(rid, new Rect2(Vector2.Zero, view.ScreenSize), PlayBackground);

        Rect2 field = FieldRect(view, playfield);
        canvas.DrawRect(rid, field, PlayfieldBackground);
        DrawBorder(canvas, rid, field, BorderColor, 2);

        DrawNotes(canvas, rid, entities, view, playfield, playhead, default);
        DrawMines(canvas, rid, entities, view, playhead);
        DrawMovementQueue(canvas, rid, entities, view, playhead, field);
        DrawPlayer(canvas, rid, entities, view);
        DrawHitTrails(canvas, rid, entities);
        DrawKeyTrail(canvas, rid, entities, field, playhead);
        DrawTransport(canvas, rid, field, playhead);
    }

    public static void DrawField(
        IRectCanvas canvas,
        Rid rid,
        Rect2 field,
        float borderWidth = 1)
    {
        canvas.DrawRect(rid, field, PlayfieldBackground);
        DrawBorder(canvas, rid, field, BorderColor, borderWidth);
    }

    public static void DrawGrid(
        IRectCanvas canvas,
        Rid rid,
        Playfield playfield,
        Rect2 field)
    {
        const int divisions = 10;

        for (int i = 0; i <= divisions; i++)
        {
            float t = i / (float)divisions;
            float x = field.Position.X + field.Size.X * t;
            float y = field.Position.Y + field.Size.Y * t;
            bool center = i == divisions / 2;
            Color color = center ? AxisColor : GridColor;
            float width = center ? 2 : 1;

            canvas.DrawLine(
                rid,
                new Vector2(x, field.Position.Y),
                new Vector2(x, field.End.Y),
                color,
                width);

            canvas.DrawLine(
                rid,
                new Vector2(field.Position.X, y),
                new Vector2(field.End.X, y),
                color,
                width);
        }

        float labelY = field.End.Y - 20;
        DrawLeftText(canvas, rid, "0", field.Position.X + 6, labelY, DimTextColor, field.Size.X);
        DrawLeftText(
            canvas,
            rid,
            (playfield.Width / 2).ToString(),
            field.Position.X + field.Size.X / 2 - 18,
            labelY,
            DimTextColor,
            field.Size.X);
        DrawLeftText(
            canvas,
            rid,
            playfield.Width.ToString(),
            field.End.X - 46,
            labelY,
            DimTextColor,
            field.Size.X);
    }

    public static void DrawNotes(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        Entity selectedNote)
    {
        float size = NoteSize * view.Scale;

        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            float alpha = NoteFade.Alpha(note, playhead.TimeMs);
            if (alpha <= 0)
                continue;

            Vector2 center = view.PlayfieldToScreen(note.Position);
            var bounds = new Rect2(
                center - new Vector2(size / 2, size / 2),
                new Vector2(size, size));

            Color laneColor = ColorFor(note.Target);
            Color color = entity.Tags.Has<NoteMissed>()
                ? new Color(0.4f, 0.4f, 0.5f, alpha * 0.45f)
                : new Color(laneColor.R, laneColor.G, laneColor.B, alpha);

            canvas.DrawRect(rid, bounds, color);

            if (!selectedNote.IsNull && selectedNote.Id == entity.Id)
            {
                DrawBorder(
                    canvas,
                    rid,
                    bounds,
                    new Color(SelectedNoteColor.R, SelectedNoteColor.G, SelectedNoteColor.B, alpha),
                    2);
            }
        }
    }

    public static void DrawMines(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playhead playhead)
    {
        float size = MineSize * view.Scale;

        foreach (Entity entity in entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            float alpha = MineFade.Alpha(mine, playhead.TimeMs);
            if (alpha <= 0)
                continue;

            Vector2 center = view.PlayfieldToScreen(mine.Position);
            var bounds = new Rect2(
                center - new Vector2(size / 2, size / 2),
                new Vector2(size, size));
            Color color = new Color(1f, 0.22f, 0.16f, alpha);

            canvas.DrawRect(
                rid,
                bounds,
                new Color(0.08f, 0.02f, 0.02f, alpha * 0.9f));
            DrawBorder(canvas, rid, bounds, color, 2);
            DrawBorder(
                canvas,
                rid,
                new Rect2(
                    bounds.Position + new Vector2(3, 3),
                    bounds.Size - new Vector2(6, 6)),
                new Color(1f, 0.55f, 0.2f, alpha),
                1);
            canvas.DrawLine(
                rid,
                bounds.Position + new Vector2(5, 5),
                bounds.End - new Vector2(5, 5),
                color,
                2);
            canvas.DrawLine(
                rid,
                new Vector2(bounds.End.X - 5, bounds.Position.Y + 5),
                new Vector2(bounds.Position.X + 5, bounds.End.Y - 5),
                color,
                2);
        }
    }

    public static Color ColorFor(KeybindAction action) =>
        LaneColors[(int)action % LaneColors.Length];

    public static void DrawMovementQueue(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playhead playhead,
        Rect2 field)
    {
        Entity playerEntity = default;
        foreach (Entity candidate in entities.Query<Player, Position>().ToEntityList())
        {
            playerEntity = candidate;
            break;
        }

        if (playerEntity.IsNull)
            return;

        Vector2 playerScreen = playerEntity.GetComponent<Position>().Value;
        const float lookaheadMs = 1_200f;
        const float activeWindowMs = 150f;

        bool anyUpcoming = false;
        foreach (Entity entity in entities.Query<MovementEvent>().ToEntityList())
        {
            if (entity.Tags.Has<MovementEventUsed>())
                continue;

            long diff = entity.GetComponent<MovementEvent>().TimeMs -
                        playhead.TimeMs;

            if (diff < -activeWindowMs || diff > lookaheadMs)
                continue;

            anyUpcoming = true;

            float t = Math.Clamp(diff / lookaheadMs, 0f, 1f);
            float y = Mathf.Lerp(
                playerScreen.Y,
                field.Position.Y + 12,
                t);
            float width = 26;
            float height = 10;
            var marker = new Rect2(
                playerScreen.X - width / 2,
                y - height / 2,
                width,
                height);

            bool active = Math.Abs(diff) <= activeWindowMs;
            canvas.DrawRect(
                rid,
                marker,
                active ? AccentColor : QueueColor);
            DrawBorder(
                canvas,
                rid,
                marker,
                active ? Colors.White : BorderColor,
                1);
        }

        if (!anyUpcoming)
            return;

        canvas.DrawLine(
            rid,
            new Vector2(playerScreen.X, field.Position.Y + 12),
            new Vector2(playerScreen.X, playerScreen.Y),
            new Color(QueueColor.R, QueueColor.G, QueueColor.B, 0.22f),
            2);
    }

    public static void DrawHitTrails(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities)
    {
        foreach (Entity entity in entities.Query<HitTrail>().ToEntityList())
        {
            HitTrail trail = entity.GetComponent<HitTrail>();
            float t = Math.Clamp(
                trail.Age / Math.Max(0.01f, trail.Lifetime),
                0f,
                1f);
            float alpha = 1f - t;
            Color color = trail.Color;

            for (int i = 0; i < 5; i++)
            {
                float offset = (i + 1) * 8f;
                float size = 12f * (1f - t * 0.4f);
                canvas.DrawRect(
                    rid,
                    new Rect2(
                        trail.Position.X - size / 2,
                        trail.Position.Y - offset - size / 2,
                        size,
                        size),
                    new Color(
                        color.R,
                        color.G,
                        color.B,
                        alpha * (1f - i / 5f) * 0.35f));
            }

            var badge = new Rect2(
                trail.Position.X - 22,
                trail.Position.Y + 14,
                44,
                24);
            canvas.DrawRect(
                rid,
                badge,
                new Color(color.R, color.G, color.B, alpha * 0.8f));
            DrawBorder(
                canvas,
                rid,
                badge,
                new Color(1, 1, 1, alpha * 0.8f),
                1);
            canvas.DrawTextStyled(
                rid,
                badge,
                trail.Label,
                new Color(1, 1, 1, alpha),
                12);
        }
    }

    public static void DrawKeyTrail(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        Rect2 field,
        Playhead playhead)
    {
        KeyTrail? state = null;
        string[]? labels = null;
        foreach (Entity entity in entities.Query<KeyTrail>().ToEntityList())
        {
            state = entity.GetComponent<KeyTrail>();
        }

        foreach (Entity entity in entities.Query<KeyTrailLabels>().ToEntityList())
        {
            labels = entity.GetComponent<KeyTrailLabels>().Labels;
        }

        if (state is null)
            return;

        const float keySize = 48;
        const float gap = 8;
        const float historyHeight = 180;
        const float historyWindowMs = 1_000f;
        float total = KeybindSettings.Actions.Length * keySize +
                      (KeybindSettings.Actions.Length - 1) * gap;
        float startX = Math.Max(10, field.Position.X - total - 12);
        float centerY = field.End.Y - 28;

        float keyBottom = centerY + keySize / 2;
        float historyTop = centerY - keySize / 2 - 4 - historyHeight;
        var trailBox = new Rect2(
            startX - 10,
            historyTop - 8,
            total + 20,
            keyBottom + 8 - (historyTop - 8));

        canvas.DrawRect(
            rid,
            trailBox,
            new Color(0.02f, 0.03f, 0.06f, 0.45f));
        DrawBorder(
            canvas,
            rid,
            trailBox,
            new Color(0.35f, 0.45f, 0.58f, 0.8f),
            1);

        // Released trails rise out of their keycaps as time advances.
        foreach (Entity entity in entities.Query<KeyPressTrail>().ToEntityList())
        {
            KeyPressTrail segment = entity.GetComponent<KeyPressTrail>();
            int index = (int)segment.Action;
            if (index < 0 || index >= KeybindSettings.Actions.Length)
                continue;

            long now = playhead.TimeMs;
            long end = segment.EndMs >= 0
                ? segment.EndMs
                : now;
            long windowStart = now - (long)historyWindowMs;
            if (end < windowStart || segment.StartMs > now)
                continue;

            float x = startX + index * (keySize + gap) + keySize / 2;
            float bottom = centerY - keySize / 2 - 4;
            float startY = bottom -
                           Math.Clamp(
                               (now - Math.Max(segment.StartMs, windowStart)) /
                               historyWindowMs,
                               0f,
                               1f) * historyHeight;
            float endY = bottom -
                         Math.Clamp(
                             (now - Math.Max(end, windowStart)) /
                             historyWindowMs,
                             0f,
                             1f) * historyHeight;
            float top = Math.Min(startY, endY);
            float height = Math.Max(2f, Math.Abs(endY - startY));
            float age = Math.Clamp(
                (playhead.TimeMs - end) / historyWindowMs,
                0f,
                1f);
            Color color = ColorFor(segment.Action);
            float alpha = 0.18f + (1f - age) * 0.62f;

            var segmentBounds = new Rect2(
                x - keySize * 0.28f,
                top,
                keySize * 0.56f,
                height);
            canvas.DrawRect(
                rid,
                segmentBounds,
                new Color(color.R, color.G, color.B, alpha * 0.35f));
            DrawBorder(
                canvas,
                rid,
                segmentBounds,
                new Color(1f, 1f, 1f, alpha * 0.35f),
                1);
            if (height > 5f)
            {
                canvas.DrawRect(
                    rid,
                    new Rect2(
                        x - 3f,
                        top + 2f,
                        6f,
                        height - 4f),
                    new Color(color.R, color.G, color.B, alpha));
            }
        }

        for (int i = 0; i < KeybindSettings.Actions.Length; i++)
        {
            KeybindAction action = KeybindSettings.Actions[i];
            float intensity = Math.Clamp(state.Value.Get(action), 0f, 1f);
            Color color = ColorFor(action);
            var keyBounds = new Rect2(
                startX + i * (keySize + gap),
                centerY - keySize / 2,
                keySize,
                keySize);

            Color fill = intensity > 0f
                ? new Color(
                    color.R,
                    color.G,
                    color.B,
                    0.18f + intensity * 0.45f)
                : new Color(0.03f, 0.04f, 0.08f, 0.82f);

            canvas.DrawRect(rid, keyBounds, fill);
            DrawBorder(canvas, rid, keyBounds, color, 1);
            canvas.DrawTextStyled(
                rid,
                keyBounds,
                labels is null || i >= labels.Length
                    ? KeybindSettings.LabelFor(action)
                    : labels[i],
                color,
                12);
        }
    }

    public static void DrawPlayer(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view)
    {
        foreach (Entity entity in entities.Query<Player, Position>().ToEntityList())
        {
            Player player = entity.GetComponent<Player>();
            Vector2 center = entity.GetComponent<Position>().Value;
            Vector2 size = player.Size;
            var bounds = new Rect2(
                center - size / 2,
                size);

            canvas.DrawRect(rid, bounds, PlayerColor);
            DrawBorder(canvas, rid, bounds, QueueColor, 2);
            return;
        }
    }

    public static void DrawTransport(IRectCanvas canvas, Rid rid, Rect2 field, Playhead playhead)
    {
        float barY = field.End.Y + 10;
        var bar = new Rect2(field.Position.X, barY, field.Size.X, 8);
        canvas.DrawRect(rid, bar, PlayfieldBackground);
        DrawBorder(canvas, rid, bar, BorderColor, 1);

        if (playhead.DurationMs <= 0)
            return;

        float progress = Math.Clamp(
            playhead.TimeMs / (float)playhead.DurationMs,
            0f,
            1f);

        canvas.DrawRect(
            rid,
            new Rect2(bar.Position, new Vector2(bar.Size.X * progress, bar.Size.Y)),
            AccentColor);

        float x = bar.Position.X + bar.Size.X * progress;
        canvas.DrawLine(
            rid,
            new Vector2(x, bar.Position.Y - 2),
            new Vector2(x, bar.End.Y + 2),
            Colors.White,
            2);
    }

    public static void DrawBorder(
        IRectCanvas canvas,
        Rid rid,
        Rect2 bounds,
        Color color,
        float width)
    {
        canvas.DrawRect(rid,
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)), color);
        canvas.DrawRect(rid,
            new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)), color);
        canvas.DrawRect(rid,
            new Rect2(bounds.Position, new Vector2(width, bounds.Size.Y)), color);
        canvas.DrawRect(rid,
            new Rect2(new Vector2(bounds.End.X - width, bounds.Position.Y),
                new Vector2(width, bounds.Size.Y)), color);
    }

    private static void DrawLeftText(
        IRectCanvas canvas,
        Rid rid,
        string text,
        float x,
        float y,
        Color color,
        float width) =>
        canvas.DrawTextLeft(
            rid,
            new Rect2(x, y, width - 32, 20),
            text,
            color);
}
