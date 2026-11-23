using Xanadu.Model.Levels;

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

        document.Notes ??= [];
        document.Notes.RemoveAll(note => note is null);
        document.Chords ??= [];
        document.Chords.RemoveAll(chord => chord is null);
        document.Pads ??= [];
        document.Pads.RemoveAll(pad => pad is null);
        document.Drafts ??= [];
        document.Drafts.RemoveAll(draft => draft is null);
        document.DashStops ??= [];
        document.DashStops.RemoveAll(stop => stop is null);
        document.Decos ??= [];
        document.Decos.RemoveAll(deco => deco?.Xif is null);
        return document;
    }
}
