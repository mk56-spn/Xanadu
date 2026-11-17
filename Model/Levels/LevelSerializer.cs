using System;
using System.Collections.Generic;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Levels;

/// <summary>
/// XMF map serialization for level documents, plus the normalization every
/// load goes through.
/// </summary>
public static class LevelSerializer
{
    public static byte[] Serialize(LevelDocument document) =>
        XmfBinary.Write(document);

    public static LevelDocument Deserialize(byte[] map) =>
        Normalize(XmfBinary.Read(map));

    public static LevelDocument Normalize(LevelDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Id))
            document.Id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(document.Slug))
            document.Slug = "level-" + document.Id[..Math.Min(8, document.Id.Length)];
        document.Name ??= "Untitled";
        document.SongId ??= "";

        document.Notes ??= new();
        document.Notes.RemoveAll(note => note is null);
        foreach (NoteDocument note in document.Notes)
        {
            NormalizeRoute(note);
            NormalizeDashAndHold(note);
        }
        return document;
    }

    /// <summary>
    /// A route note keeps rising times after its head, at most
    /// <see cref="NoteRoute.MaxTimes"/>, and a known speed; a route left with no time is dropped.
    /// </summary>
    /// <summary>A note is one of route, dash or hold at most (route first, then dash); lengths below zero are none.</summary>
    private static void NormalizeDashAndHold(NoteDocument note)
    {
        if (note.DashLength < 0f || note.Route is not null)
            note.DashLength = 0f;
        if (note.HoldTailMs < 0 || note.Route is not null || note.DashLength > 0f)
            note.HoldTailMs = 0;
        if (note.HoldTailMs == 0)
            note.HoldX = note.HoldY = 0f;
    }

    private static void NormalizeRoute(NoteDocument note)
    {
        if (note.Route is null)
            return;
        note.Route.RemoveAll(time => time <= 0);
        note.Route.Sort();
        for (int i = note.Route.Count - 1; i > 0; i--)
        {
            if (note.Route[i] == note.Route[i - 1])
                note.Route.RemoveAt(i);
        }

        if (note.Route.Count > NoteRoute.MaxTimes)
            note.Route.RemoveRange(NoteRoute.MaxTimes, note.Route.Count - NoteRoute.MaxTimes);
        if (note.Route.Count < NoteRoute.MinTimes)
            note.Route = null;
        if (!Enum.IsDefined(note.RouteStrength))
            note.RouteStrength = DashStrength.Single;
    }
}
