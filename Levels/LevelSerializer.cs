using Xanadu.Ecs;

namespace Xanadu.Levels;

/// <summary>
/// XMF map serialization for level documents, plus the normalization every
/// load goes through. Levels are binary; the map shares XAF character
/// packages' chunked container.
/// </summary>
public static class LevelSerializer
{
    public static byte[] Serialize(LevelDocument document) =>
        XmfBinary.Write(document);

    public static LevelDocument Deserialize(byte[] map) =>
        Normalize(XmfBinary.Read(map));

    public static LevelDocument Normalize(LevelDocument document)
    {
        if (document.Version <= 0)
            document.Version = 1;
        if (string.IsNullOrWhiteSpace(document.Id))
            document.Id = System.Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(document.Slug))
            document.Slug = "level-" + document.Id[..System.Math.Min(8, document.Id.Length)];

        document.Notes ??= new();
        document.Notes.RemoveAll(note => note is null);
        document.Mines ??= new();
        document.StartPoints ??= new();
        document.StartPoints.RemoveAll(start => start is null);
        document.Blocks ??= new();
        document.Groups ??= new();
        document.Groups.RemoveAll(group => group is null || group.Id is < 0 or >= GroupLimits.Count);
        foreach (GroupDocument group in document.Groups)
        {
            group.Name ??= "";
            group.Effects ??= new();
            group.Effects.RemoveAll(effect => effect is null);
            foreach (GroupEffectDocument effect in group.Effects)
            {
                if (!System.Enum.IsDefined(effect.Ease))
                    effect.Ease = GroupEase.Linear;
            }
        }

        foreach (NoteDocument note in document.Notes)
            note.Groups ??= new();
        foreach (StartPointDocument start in document.StartPoints)
            start.Groups ??= new();
        document.Mines.RemoveAll(mine => mine is null);
        document.Blocks.RemoveAll(block => block is null);
        foreach (MineDocument mine in document.Mines)
            mine.Groups ??= new();
        foreach (BlockDocument block in document.Blocks)
            block.Groups ??= new();

        NormalizeShaders(document);
        return document;
    }

    /// <summary>Keeps only shaders a level may carry, one per slot, and clears references to the rest.</summary>
    private static void NormalizeShaders(LevelDocument document)
    {
        document.Shaders ??= new();
        var seen = new System.Collections.Generic.HashSet<int>();
        document.Shaders.RemoveAll(shader =>
            shader is null ||
            !LevelShaders.IsSlot(shader.Slot) ||
            !seen.Add(shader.Slot) ||
            !ShaderSourceRules.IsAcceptable(shader.Source, out _));
        foreach (ShaderDocument shader in document.Shaders)
        {
            shader.Name ??= "";
            shader.Params ??= new();
        }

        int Valid(int slot) => seen.Contains(slot) && document.Shaders.Exists(s => s.Slot == slot) ? slot : 0;

        document.PlayerShader = Valid(document.PlayerShader);
        document.FieldShader = Valid(document.FieldShader);
        foreach (NoteDocument note in document.Notes)
            note.Shader = Valid(note.Shader);
        foreach (MineDocument mine in document.Mines)
            mine.Shader = Valid(mine.Shader);
        foreach (BlockDocument block in document.Blocks)
            block.Shader = Valid(block.Shader);
    }
}
