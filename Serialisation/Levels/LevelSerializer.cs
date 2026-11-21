using Xanadu.Model.Levels;
using System;
using System.Collections.Generic;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Serialisation.Levels;

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
        return document;
    }
}
