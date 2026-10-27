using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;

namespace Xanadu.Editor.Analysis;

public enum PlayabilityProblem
{
    /// <summary>The note is too far sideways from where the player can be.</summary>
    TooFarSideways,

    /// <summary>The note is higher than gravity lets the player stay.</summary>
    TooHigh,
}

public readonly record struct PlayabilityIssue(
    long TimeMs,
    Vector2 Position,
    PlayabilityProblem Problem,
    float Shortfall);

/// <summary>The playfield as the player sees it, in screen pixels.</summary>
public readonly record struct PlayabilityGeometry(
    Vector2 Origin,
    Vector2 FieldSize,
    float Scale,
    Vector2 PlayerSize,
    float NoteSize,
    float GravityPixelsPerSecondSquared,
    float WindowMs)
{
    public static PlayabilityGeometry For(
        PlayfieldView view,
        Playfield playfield,
        float windowMs) => new(
        view.ScreenOrigin,
        new Vector2(playfield.Width, playfield.Height) * view.Scale,
        view.Scale,
        Player.ScaledSize(view.Scale),
        PlayfieldRenderer.NoteSize * view.Scale,
        Box2DPhysicsWorld.DefaultGravity * Box2DPhysicsWorld.PixelsPerMeter,
        windowMs);
}

/// <summary>
/// Finds notes the player can never be touching. The player only moves by
/// dashing, which teleports them to the dash target of a movement note, and
/// otherwise just falls. So after each dash the player sits at one x (the
/// field wraps sideways) and can only be at or below the height gravity gives
/// them. A note outside that is unreachable no matter how well it is played.
/// </summary>
/// <remarks>
/// The check is deliberately generous so that it never cries wolf: the dash
/// is assumed to land as late as the timing window allows, the fall may have
/// any starting speed, and blocks are ignored. It only reports certainties.
/// </remarks>
public static class PlayabilityAnalyzer
{
    public static List<PlayabilityIssue> Analyze(
        IReadOnlyList<AnalysedNote> notes,
        PlayabilityGeometry geometry,
        Vector2? start = null)
    {
        var issues = new List<PlayabilityIssue>();
        if (notes.Count == 0 || geometry.FieldSize.X <= 0 || geometry.FieldSize.Y <= 0)
            return issues;

        var ordered = new List<AnalysedNote>(notes);
        // Hits settle before a movement note at the same instant dashes the player away.
        ordered.Sort(static (a, b) =>
        {
            int time = a.TimeMs.CompareTo(b.TimeMs);
            return time != 0 ? time : (a.Kind == NoteKind.Movement).CompareTo(b.Kind == NoteKind.Movement);
        });

        Vector2 half = geometry.PlayerSize / 2;
        float floor = Math.Max(
            geometry.Origin.Y + half.Y,
            geometry.Origin.Y + geometry.FieldSize.Y - half.Y);

        // The player has fallen to the floor during the countdown.
        Vector2 player = new(
            Wrap(start is { } s ? ToScreen(s, geometry).X : geometry.Origin.X + geometry.FieldSize.X / 2, geometry),
            floor);
        long dashedAt = long.MinValue;

        foreach (AnalysedNote note in ordered)
        {
            Vector2 screen = ToScreen(note.Position, geometry);
            float reachX = (geometry.PlayerSize.X + geometry.NoteSize) / 2;
            float reachY = (geometry.PlayerSize.Y + geometry.NoteSize) / 2;

            float sideways = Math.Abs(screen.X - player.X);
            sideways = Math.Min(sideways, geometry.FieldSize.X - sideways);

            // The highest the player can be while the note still counts.
            float earliest = dashedAt == long.MinValue
                ? 0
                : Math.Max(0, note.TimeMs - geometry.WindowMs - dashedAt) / 1000f;
            float highest = Math.Min(
                floor,
                player.Y + 0.5f * geometry.GravityPixelsPerSecondSquared * earliest * earliest);

            if (sideways >= reachX)
            {
                issues.Add(new PlayabilityIssue(
                    note.TimeMs,
                    note.Position,
                    PlayabilityProblem.TooFarSideways,
                    (sideways - reachX) / geometry.Scale));
            }
            else if (screen.Y + reachY <= highest)
            {
                issues.Add(new PlayabilityIssue(
                    note.TimeMs,
                    note.Position,
                    PlayabilityProblem.TooHigh,
                    (highest - (screen.Y + reachY)) / geometry.Scale));
            }

            if (note.Kind != NoteKind.Movement)
                continue;

            // Assume the dash happens, even for an unreachable note, so one
            // bad note does not flag everything after it.
            Vector2 target = PlayerMovementSystem.DashTarget(note.Position, note.Direction);
            Vector2 landed = ToScreen(target, geometry);
            player = new Vector2(
                Wrap(landed.X, geometry),
                Math.Clamp(landed.Y, Math.Min(geometry.Origin.Y + half.Y, floor), floor));
            dashedAt = note.TimeMs + (long)geometry.WindowMs;
        }

        return issues;
    }

    private static Vector2 ToScreen(Vector2 position, PlayabilityGeometry geometry) =>
        geometry.Origin + position * geometry.Scale;

    private static float Wrap(float x, PlayabilityGeometry geometry)
    {
        float width = geometry.FieldSize.X;
        float local = (x - geometry.Origin.X) % width;
        if (local < 0)
            local += width;

        return geometry.Origin.X + local;
    }
}
