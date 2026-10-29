using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Items;

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
            document.Id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(document.Slug))
            document.Slug = "level-" + document.Id[..Math.Min(8, document.Id.Length)];

        document.Shaders ??= new();
        Func<int, int> validShader = NormalizeShaderPool(document.Shaders);
        NormalizeChartItems(document, validShader);
        return document;
    }

    public static byte[] SerializeMapset(XmfMapset mapset) =>
        XmfBinary.WriteMapset(mapset);

    public static XmfMapset DeserializeMapset(byte[] map) =>
        NormalizeMapset(XmfBinary.ReadMapset(map));

    public static XmfMapset NormalizeMapset(XmfMapset mapset)
    {
        if (mapset.Version <= 0)
            mapset.Version = 1;
        if (string.IsNullOrWhiteSpace(mapset.Id))
            mapset.Id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(mapset.Slug))
            mapset.Slug = "mapset-" + mapset.Id[..Math.Min(8, mapset.Id.Length)];

        mapset.Shaders ??= new();
        Func<int, int> validShader = NormalizeShaderPool(mapset.Shaders);

        mapset.Variants ??= new();
        mapset.Variants.RemoveAll(variant => variant is null);
        foreach (XmfVariant variant in mapset.Variants)
        {
            if (string.IsNullOrWhiteSpace(variant.Id))
                variant.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(variant.Slug))
                variant.Slug = "variant-" + variant.Id[..Math.Min(8, variant.Id.Length)];
            variant.Name ??= "Untitled";
            NormalizeChartItems(variant, validShader);
        }

        return mapset;
    }

    /// <summary>Keeps only shaders a pool may carry, one per slot, and returns a validator for references to it.</summary>
    private static Func<int, int> NormalizeShaderPool(List<ShaderDocument> shaders)
    {
        var seen = new HashSet<int>();
        shaders.RemoveAll(shader =>
            shader is null ||
            !LevelShaders.IsSlot(shader.Slot) ||
            !seen.Add(shader.Slot) ||
            !ShaderSourceRules.IsAcceptable(shader.Source, out _));
        foreach (ShaderDocument shader in shaders)
        {
            shader.Name ??= "";
            shader.Params ??= new();
        }

        return slot => seen.Contains(slot) && shaders.Exists(s => s.Slot == slot) ? slot : 0;
    }

    /// <summary>Normalizes a chart's placed items, and the shader references they and it carry.</summary>
    private static void NormalizeChartItems(IChartItems chart, Func<int, int> validShader)
    {
        chart.Notes ??= new();
        chart.Notes.RemoveAll(note => note is null);
        chart.Mines ??= new();
        chart.Mines.RemoveAll(mine => mine is null);
        chart.StartPoints ??= new();
        chart.StartPoints.RemoveAll(start => start is null);
        chart.Blocks ??= new();
        chart.Blocks.RemoveAll(block => block is null);
        chart.Spikes ??= new();
        chart.Spikes.RemoveAll(spike => spike is null);
        chart.Groups ??= new();
        chart.Groups.RemoveAll(group => group is null || group.Id is < 0 or >= GroupLimits.Count);

        foreach (GroupDocument group in chart.Groups)
        {
            group.Name ??= "";
            group.Effects ??= new();
            group.Effects.RemoveAll(effect => effect is null);
            foreach (GroupEffectDocument effect in group.Effects)
            {
                if (!Enum.IsDefined(effect.Ease))
                    effect.Ease = GroupEase.Linear;
            }
        }

        foreach (NoteDocument note in chart.Notes)
        {
            note.Groups ??= new();
            note.Item ??= new();
            NormalizeItem(note.Item, validShader);
        }
        foreach (StartPointDocument start in chart.StartPoints)
            start.Groups ??= new();
        foreach (MineDocument mine in chart.Mines)
        {
            mine.Groups ??= new();
            mine.Shader = validShader(mine.Shader);
        }
        foreach (BlockDocument block in chart.Blocks)
        {
            block.Groups ??= new();
            block.Shader = validShader(block.Shader);
        }
        foreach (SpikeDocument spike in chart.Spikes)
        {
            spike.Groups ??= new();
            spike.Shader = validShader(spike.Shader);
        }

        chart.BackgroundColor = LevelBackground.Normalize(chart.BackgroundColor);
        chart.PlayerShader = validShader(chart.PlayerShader);
        chart.FieldShader = validShader(chart.FieldShader);
    }

    /// <summary>Keeps only shader references a Xif may carry, and clears the rest.</summary>
    private static void NormalizeItem(Xif item, Func<int, int> validMapSlot)
    {
        item.Primitives ??= new();
        item.Shaders ??= new();
        item.Params ??= new();

        foreach (XifShaderRef shader in item.Shaders)
            shader.Path ??= "";
        item.Shaders.RemoveAll(shader => shader is null || !IsResolvableShaderRef(shader, validMapSlot));

        foreach (XifPrimitive primitive in item.Primitives)
        {
            primitive.Kind ??= "";
            primitive.Ref ??= "";
            primitive.Shader ??= new();
            primitive.Shader.Path ??= "";
            if (!primitive.Shader.IsNone && !IsResolvableShaderRef(primitive.Shader, validMapSlot))
                primitive.Shader = new();
        }
    }

    /// <summary>Whether a shader reference points at a slot or path its item may actually use.</summary>
    private static bool IsResolvableShaderRef(XifShaderRef shader, Func<int, int> validMapSlot) =>
        shader.Scope switch
        {
            XifShaderScope.Map => validMapSlot(shader.Slot) != 0,
            XifShaderScope.Common => CommonShaders.IsKnown(shader.Path),
            _ => false,
        };
}
