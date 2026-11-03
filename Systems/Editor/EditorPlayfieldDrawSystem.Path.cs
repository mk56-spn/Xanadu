using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>The path the player's hitbox took in the last preview.</summary>
public sealed partial class EditorPlayfieldDrawSystem
{
    /// <summary>Field units the hitbox moves between the outlines drawn along the path.</summary>
    public const float PathBoxSpacing = 3f;

    private static readonly Color PathLine = new(Palette.Success, 0.8f);
    private static readonly Color PathJump = new(Palette.Success, 0.35f);
    private static readonly Color PathBox = new(Palette.Success, 0.14f);

    /// <summary>The hitbox's outline on the frame a note was hit.</summary>
    public static readonly Color PathHit = Palette.Accent;

    private readonly List<Vector2> _pathPoints = new();
    private readonly List<Rect2> _pathBoxes = new();
    private readonly List<Rect2> _pathHits = new();

    /// <summary>
    /// Draws the hitbox's outline all along the path, dense enough to read as the
    /// space it swept, its centre as a line, and its outline at the playhead, so a
    /// note can be placed for that moment. The frames a note was hit stand out in
    /// their own colour. Outlines off the field area are skipped.
    /// </summary>
    private void DrawPlayerPath(PlayfieldView view, Playhead playhead, PlayerPath path, float fieldWidth)
    {
        List<PlayerPathSample> samples = path.Samples;
        if (samples.Count == 0)
            return;

        Vector2 size = path.HitboxSize * view.Scale;
        Rect2 area = EditorViewport.Area(view.ScreenSize);
        Vector2? lastBox = null;
        _pathPoints.Clear();
        _pathBoxes.Clear();
        _pathHits.Clear();
        for (int i = 0; i < samples.Count; i++)
        {
            PlayerPathSample sample = samples[i];
            Vector2 screen = view.PlayfieldToScreen(sample.Position);
            if (sample.Jump)
            {
                FlushPathLine();

                // A wrap crosses the field without travelling it, so it gets no link.
                Vector2 from = samples[Math.Max(0, i - 1)].Position;
                if (Math.Abs(sample.Position.X - from.X) < fieldWidth / 2)
                    _canvas.DrawLine(_editor, view.PlayfieldToScreen(from), screen, PathJump, 1);
            }

            _pathPoints.Add(screen);
            if (sample.Hit)
            {
                var hitBox = new Rect2(screen - size / 2, size);
                if (hitBox.Intersects(area))
                    _pathHits.Add(hitBox);
            }

            if (lastBox is not Vector2 boxAt ||
                sample.Position.DistanceTo(boxAt) >= PathBoxSpacing)
            {
                lastBox = sample.Position;
                var box = new Rect2(screen - size / 2, size);
                if (box.Intersects(area))
                    _pathBoxes.Add(box);
            }
        }

        _canvas.DrawRectOutlines(
            _editor, CollectionsMarshal.AsSpan(_pathBoxes), PathBox);
        FlushPathLine();
        foreach (Rect2 hitBox in _pathHits)
            _canvas.DrawBorder(_editor, hitBox, PathHit, 2);

        if (PathPositionAt(samples, playhead.TimeMs) is Vector2 now)
        {
            Vector2 center = view.PlayfieldToScreen(now);
            _canvas.DrawBorder(
                _editor, new Rect2(center - size / 2, size), Palette.Success, 2);
        }
    }

    private void FlushPathLine()
    {
        if (_pathPoints.Count >= 2)
            _canvas.DrawPolyline(_editor, _pathPoints.ToArray(), PathLine, 2);
        _pathPoints.Clear();
    }

    /// <summary>Where the hitbox was at a song time, or null outside the recording.</summary>
    public static Vector2? PathPositionAt(List<PlayerPathSample> samples, long timeMs)
    {
        if (samples.Count == 0 || timeMs < samples[0].TimeMs || timeMs > samples[^1].TimeMs)
            return null;

        int low = 0;
        int high = samples.Count - 1;
        while (high - low > 1)
        {
            int middle = (low + high) / 2;
            if (samples[middle].TimeMs <= timeMs)
                low = middle;
            else
                high = middle;
        }

        PlayerPathSample a = samples[low];
        PlayerPathSample b = samples[high];
        if (b.Jump)
            return timeMs >= b.TimeMs ? b.Position : a.Position;

        long span = b.TimeMs - a.TimeMs;
        return span <= 0
            ? b.Position
            : a.Position.Lerp(b.Position, (timeMs - a.TimeMs) / (float)span);
    }
}
