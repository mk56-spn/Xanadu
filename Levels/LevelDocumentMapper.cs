using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Items;

namespace Xanadu.Levels;

/// <summary>Copies editor ECS state to and from the serializable document.</summary>
public static partial class LevelDocumentMapper
{
    public static LevelDocument Capture(EntityStore entities, Entity editor)
    {
        LevelInfo level = editor.GetComponent<LevelInfo>();
        Playfield playfield = editor.GetComponent<Playfield>();
        Playhead playhead = editor.GetComponent<Playhead>();

        var document = new LevelDocument
        {
            SongId = level.SongId,
            Name = level.Name,
            SnapDivision = playhead.SnapDivision,
            PlayfieldWidth = playfield.Width,
            PlayfieldHeight = playfield.Height,
            BackgroundColor = level.Background is { } background
                ? LevelBackground.Format(background)
                : "",
            OutsideColor = level.Outside is { } outside
                ? LevelBackground.Format(outside)
                : "",
            FrameColor = level.Frame is { } frame
                ? LevelBackground.Format(frame)
                : "",
            Groups = CaptureGroups(editor),
            Layers = CaptureLayers(editor),
        };

        CaptureShaders(editor, document);

        // A brand-new level keeps the identity the document constructor made.
        if (level.LevelId.Length > 0)
            document.Id = level.LevelId;
        if (level.Slug.Length > 0)
            document.Slug = level.Slug;

        foreach (ItemKind kind in ItemKinds.All)
            kind.CaptureInto(entities, document);

        return document;
    }

    /// <summary>Applies a level document, opening its song from the library.</summary>
    public static void Apply(EntityStore entities, Entity editor, LevelDocument document) =>
        Apply(entities, editor, document, SongStore.Shared.Open(document.SongId));

    /// <summary>Applies a level document with its song already opened, as a <see cref="LevelEntry"/> carries it.</summary>
    public static void Apply(EntityStore entities, Entity editor, LevelDocument document, LoadedSong? song)
    {
        document.Groups ??= new List<GroupDocument>();

        foreach (ItemKind kind in ItemKinds.All)
            kind.DeleteAll(entities);

        ApplyGroups(editor, document.Groups);
        ApplyLayers(editor, document.Layers);
        ApplyShaders(editor, document);

        foreach (ItemKind kind in ItemKinds.All)
            kind.ApplyFrom(entities, document);


        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        level.LevelId = document.Id ?? "";
        level.Slug = document.Slug ?? "";
        level.SongId = document.SongId;
        level.Name = document.Name;
        level.Song = song;
        level.Artist = song?.Artist ?? "";
        level.Difficulty = document.Name;
        level.Background = LevelBackground.TryParse(
            document.BackgroundColor,
            out Color background)
            ? background
            : null;
        level.Outside = LevelBackground.TryParse(
            document.OutsideColor,
            out Color outside)
            ? outside
            : null;
        level.Frame = LevelBackground.TryParse(
            document.FrameColor,
            out Color frame)
            ? frame
            : null;

        ref Playfield playfield = ref editor.GetComponent<Playfield>();
        playfield.Width = document.PlayfieldWidth;
        playfield.Height = document.PlayfieldHeight;

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.Tempo = song?.Tempo() ?? TempoMap.Default;
        playhead.SnapDivision = document.SnapDivision;
        playhead.TimeMs = 0;
        playhead.DurationMs = 0;
        playhead.Playing = false;
        playhead.SeekRequestMs = 0;
        playhead.AudioRevision++;
    }

    private static void CaptureShaders(Entity editor, LevelDocument document)
    {
        if (!editor.TryGetComponent(out LevelShaders shaders))
            return;

        for (int i = 0; i < shaders.Slots.Length; i++)
        {
            ShaderSlot? slot = shaders.Slots[i];
            if (slot is null)
                continue;

            document.Shaders.Add(new ShaderDocument
            {
                Slot = i + 1,
                Name = slot.Name,
                Source = slot.Source,
                Params = new Dictionary<string, float[]>(slot.Params),
            });
        }

        document.PlayerShader = shaders.PlayerSlot;
        document.FieldShader = shaders.FieldSlot;
    }

    private static void ApplyShaders(Entity editor, LevelDocument document)
    {
        LevelShaders shaders = LevelShaders.Create();
        foreach (ShaderDocument shader in document.Shaders ?? new List<ShaderDocument>())
        {
            if (!LevelShaders.IsSlot(shader.Slot))
                continue;

            shaders.Slots[shader.Slot - 1] = new ShaderSlot
            {
                Name = shader.Name ?? "",
                Source = shader.Source ?? "",
                Params = new Dictionary<string, float[]>(shader.Params ?? new()),
            };
        }

        shaders.PlayerSlot = LevelShaders.IsSlot(document.PlayerShader) ? document.PlayerShader : 0;
        shaders.FieldSlot = LevelShaders.IsSlot(document.FieldShader) ? document.FieldShader : 0;

        if (editor.HasComponent<LevelShaders>())
        {
            editor.GetComponent<LevelShaders>() = shaders;
            return;
        }

        // Adding the component is a structural change, which the editor's command
        // system cannot make mid-update; its entity is built with the component,
        // so this only runs for a level with shaders on an entity made without one.
        if (document.Shaders is { Count: > 0 })
            editor.AddComponent(shaders);
    }

    /// <summary>The groups with a name or effects, as documents.</summary>
    private static List<GroupDocument> CaptureGroups(Entity editor)
    {
        var result = new List<GroupDocument>();
        if (!editor.TryGetComponent(out LevelGroups levelGroups))
            return result;

        for (int i = 0; i < levelGroups.Groups.Length; i++)
        {
            GroupDefinition group = levelGroups.Groups[i];
            if (group.IsEmpty)
                continue;

            var document = new GroupDocument
            {
                Id = i,
                Name = group.Name,
                Shader = group.Shader,
                Buffered = group.Buffered,
                BufferTint = group.BufferTint.ToHtml(),
                BufferAlpha = group.BufferAlpha,
                BufferShader = group.BufferShader,
                BufferBlend = group.BufferBlend,
            };
            document.ShaderKeys.AddRange(CaptureKeys(group.ShaderKeys));
            document.BufferKeys.AddRange(CaptureKeys(group.BufferKeys));

            foreach (GroupEffect effect in group.Effects)
            {
                document.Effects.Add(new GroupEffectDocument
                {
                    Kind = effect.Kind,
                    Start = effect.Start,
                    End = effect.End,
                    Ease = effect.Ease,
                    Muted = effect.Muted,
                    X = effect.Offset.X,
                    Y = effect.Offset.Y,
                    Tint = effect.Tint.ToHtml(),
                    Cycles = effect.Cycles,
                    EveryBeats = effect.EveryBeats,
                    Cycle = effect.Cycle,
                });
            }

            result.Add(document);
        }

        return result;
    }

    /// <summary>A shader track as documents: the value clipped to the vec4 a key holds.</summary>
    private static List<ShaderKeyDocument> CaptureKeys(List<ShaderKey> keys)
    {
        var documents = new List<ShaderKeyDocument>();
        foreach (ShaderKey key in keys)
        {
            documents.Add(new ShaderKeyDocument
            {
                Param = key.Param,
                Time = key.Time,
                Value = new[] { key.Value.X, key.Value.Y, key.Value.Z, key.Value.W }
                    .AsSpan(0, Math.Clamp(key.Size, 0, ShaderKey.MaxSize))
                    .ToArray(),
                Ease = key.Ease,
            });
        }

        return documents;
    }

    /// <summary>Document keys back onto a track.</summary>
    private static void ApplyKeys(List<ShaderKeyDocument>? documents, List<ShaderKey> keys)
    {
        foreach (ShaderKeyDocument key in documents ?? new List<ShaderKeyDocument>())
        {
            float[] value = key.Value ?? [];
            int size = Math.Clamp(value.Length, 0, ShaderKey.MaxSize);
            var vector = new Vector4(
                size > 0 ? value[0] : 0f,
                size > 1 ? value[1] : 0f,
                size > 2 ? value[2] : 0f,
                size > 3 ? value[3] : 0f);
            keys.Add(new ShaderKey(key.Param ?? "", key.Time, vector, size, key.Ease));
        }
    }

    /// <summary>Puts the document's groups on the level's entity, every other slot empty.</summary>
    private static void ApplyGroups(Entity editor, List<GroupDocument> documents)
    {
        LevelGroups levelGroups = LevelGroups.Create();
        foreach (GroupDocument document in documents)
        {
            if (document.Id is < 0 or >= GroupLimits.Count)
                continue;

            GroupDefinition group = levelGroups.Groups[document.Id];
            group.Name = document.Name ?? "";
            group.Shader = document.Shader;
            group.Buffered = document.Buffered;
            group.BufferTint = new Color(document.BufferTint ?? "#ffffffff");
            group.BufferAlpha = document.BufferAlpha;
            group.BufferShader = document.BufferShader;
            group.BufferBlend = document.BufferBlend;
            ApplyKeys(document.ShaderKeys, group.ShaderKeys);
            ApplyKeys(document.BufferKeys, group.BufferKeys);

            foreach (GroupEffectDocument effect in document.Effects ?? new List<GroupEffectDocument>())
            {
                group.Effects.Add(new GroupEffect(
                    effect.Kind,
                    effect.Start,
                    effect.End,
                    new Vector2(effect.X, effect.Y),
                    new Color(effect.Tint ?? "#ffffffff"),
                    effect.Ease,
                    effect.Muted,
                    Math.Clamp(effect.Cycles, 1, GroupEffect.MaxCycles),
                    effect.EveryBeats > 0f ? effect.EveryBeats : 1f,
                    effect.Cycle));
            }
        }

        if (editor.HasComponent<LevelGroups>())
            editor.GetComponent<LevelGroups>() = levelGroups;
        else
            editor.AddComponent(levelGroups);
    }
}
