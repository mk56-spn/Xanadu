using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Items;

namespace Xanadu.Levels;

/// <summary>Copies editor ECS state to and from the serializable document.</summary>
public static class LevelDocumentMapper
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
            Notes = new List<NoteDocument>(),
            Mines = new List<MineDocument>(),
            StartPoints = new List<StartPointDocument>(),
            Blocks = new List<BlockDocument>(),
            Groups = CaptureGroups(editor),
        };

        CaptureShaders(editor, document);

        // A brand-new level keeps the identity the document constructor made.
        if (level.LevelId.Length > 0)
            document.Id = level.LevelId;
        if (level.Slug.Length > 0)
            document.Slug = level.Slug;

        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            document.Notes.Add(new NoteDocument
            {
                TimeMs = note.TimeMs,
                X = note.Position.X,
                Y = note.Position.Y,
                Target = note.Target,
                Kind = note.Kind,
                DashDirection = note.DashDirection,
                ContactRequired = note.ContactRequired,
                Character = note.Character,
                DashStrength = note.DashStrength,
                PairIndex = note.PairIndex,
                Momentum = note.Momentum,
                ContactLeft = note.ContactLeft,
                ContactRight = note.ContactRight,
                ChannelSide = note.ChannelSide,
                HoldMs = note.HoldMs,
                ShapeId = note.ShapeId,
                ShapePiece = note.ShapePiece,
                Groups = GroupsOf(entity),
                Item = Xif.FromEcs(ItemShader.Of(entity), ItemCommonShader.Of(entity)),
            });
        }

        document.Notes.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        foreach (Entity entity in entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            document.Mines.Add(new MineDocument
            {
                TimeMs = mine.TimeMs,
                X = mine.Position.X,
                Y = mine.Position.Y,
                FadeInMs = mine.FadeInMs,
                FadeOutMs = mine.FadeOutMs,
                Groups = GroupsOf(entity),
                Shader = ItemShader.Of(entity),
            });
        }

        document.Mines.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        foreach (Entity entity in entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            document.StartPoints.Add(new StartPointDocument
            {
                Name = start.Name,
                TimeMs = start.TimeMs,
                X = start.Position.X,
                Y = start.Position.Y,
                MomentumX = start.Momentum.X,
                MomentumY = start.Momentum.Y,
                DirectionX = start.Direction.X,
                DirectionY = start.Direction.Y,
                Groups = GroupsOf(entity),
            });
        }

        document.StartPoints.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        foreach (Entity entity in entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            document.Blocks.Add(new BlockDocument
            {
                X = block.Position.X,
                Y = block.Position.Y,
                Width = block.Size.X,
                Height = block.Size.Y,
                ShapeId = block.ShapeId,
                ShapePiece = block.ShapePiece,
                Groups = GroupsOf(entity),
                Shader = ItemShader.Of(entity),
            });
        }

        document.Blocks.Sort((a, b) =>
        {
            int x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Y.CompareTo(b.Y);
        });

        foreach (Entity entity in entities.Query<Spike>().ToEntityList())
        {
            Spike spike = entity.GetComponent<Spike>();
            document.Spikes.Add(new SpikeDocument
            {
                X = spike.Position.X,
                Y = spike.Position.Y,
                Width = spike.Size.X,
                Height = spike.Size.Y,
                Groups = GroupsOf(entity),
                Shader = ItemShader.Of(entity),
            });
        }

        document.Spikes.Sort((a, b) =>
        {
            int x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Y.CompareTo(b.Y);
        });

        return document;
    }

    public static void Apply(EntityStore entities, Entity editor, LevelDocument document)
    {
        document.Notes ??= new List<NoteDocument>();
        document.Mines ??= new List<MineDocument>();
        document.StartPoints ??= new List<StartPointDocument>();
        document.Blocks ??= new List<BlockDocument>();
        document.Spikes ??= new List<SpikeDocument>();
        document.Groups ??= new List<GroupDocument>();

        foreach (Entity entity in entities.Query<Note>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<Mine>().ToEntityList())
            entity.DeleteEntity();

        ApplyGroups(editor, document.Groups);
        ApplyShaders(editor, document);

        foreach (NoteDocument note in document.Notes)
        {
            Entity created = EditorItems.Create(entities, new Note(
                note.TimeMs,
                new Vector2(note.X, note.Y),
                note.Target,
                note.Kind,
                note.DashDirection,
                note.ContactRequired,
                note.Character,
                note.DashStrength,
                note.PairIndex,
                note.Momentum,
                note.ContactLeft,
                note.ContactRight,
                note.ChannelSide,
                note.HoldMs,
                note.ShapeId,
                note.ShapePiece), note.Item?.PrimaryShader ?? 0, note.Item?.PrimaryCommonShader ?? "");
            Subscribe(created, note.Groups);
        }

        foreach (Entity entity in entities.Query<StartPoint>().ToEntityList())
            entity.DeleteEntity();

        foreach (StartPointDocument start in document.StartPoints)
        {
            uint groups = ItemGroups.FromList(start.Groups);
            Entity created = EditorItems.Create(entities, new StartPoint(
                start.Name,
                start.TimeMs,
                new Vector2(start.X, start.Y),
                new System.Numerics.Vector2(
                    start.MomentumX,
                    start.MomentumY),
                new Vector2(start.DirectionX, start.DirectionY),
                groups));
            Subscribe(created, groups);
        }

        foreach (MineDocument mine in document.Mines)
        {
            Entity created = EditorItems.Create(entities, new Mine(
                mine.TimeMs,
                new Vector2(mine.X, mine.Y),
                mine.FadeInMs,
                mine.FadeOutMs), mine.Shader);
            Subscribe(created, mine.Groups);
        }

        foreach (Entity entity in entities.Query<Block>().ToEntityList())
            entity.DeleteEntity();

        foreach (BlockDocument block in document.Blocks)
        {
            Entity created = EditorItems.Create(entities, new Block(
                new Vector2(block.X, block.Y),
                new Vector2(block.Width, block.Height),
                block.ShapeId,
                block.ShapePiece), block.Shader);
            if (block.ShapeId > 0)
                created.AddTag<ConstructionHidden>();
            Subscribe(created, block.Groups);
        }

        foreach (Entity entity in entities.Query<Spike>().ToEntityList())
            entity.DeleteEntity();

        foreach (SpikeDocument spike in document.Spikes)
        {
            Entity created = EditorItems.Create(entities, new Spike(
                new Vector2(spike.X, spike.Y),
                new Vector2(spike.Width, spike.Height)), spike.Shader);
            Subscribe(created, spike.Groups);
        }

        SongDocument? song = string.IsNullOrWhiteSpace(document.SongId)
            ? null
            : new SongStore().Find(document.SongId);

        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        level.LevelId = document.Id ?? "";
        level.Slug = document.Slug ?? "";
        level.SongId = document.SongId;
        level.Name = document.Name;
        level.AudioPath = song?.AudioPath ?? level.AudioPath;
        level.Artist = song?.Artist ?? "";
        level.Difficulty = document.Name;
        level.Background = LevelBackground.TryParse(
            document.BackgroundColor,
            out Color background)
            ? background
            : null;

        ref Playfield playfield = ref editor.GetComponent<Playfield>();
        playfield.Width = document.PlayfieldWidth;
        playfield.Height = document.PlayfieldHeight;

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.Bpm = song?.Bpm ?? 120;
        playhead.OffsetMs = song?.OffsetMs ?? 0;
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

    private static List<int> GroupsOf(Entity entity) =>
        entity.TryGetComponent(out ItemGroups groups) ? ItemGroups.ToList(groups.Mask) : new List<int>();

    /// <summary>Puts a new item into the groups the document lists for it.</summary>
    private static void Subscribe(Entity item, List<int>? groups)
    {
        Subscribe(item, ItemGroups.FromList(groups));
    }

    private static void Subscribe(Entity item, uint mask)
    {
        if (mask != 0)
            item.AddComponent(new ItemGroups(mask));
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

            var document = new GroupDocument { Id = i, Name = group.Name };
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
                });
            }

            result.Add(document);
        }

        return result;
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
            foreach (GroupEffectDocument effect in document.Effects ?? new List<GroupEffectDocument>())
            {
                group.Effects.Add(new GroupEffect(
                    effect.Kind,
                    effect.Start,
                    effect.End,
                    new Vector2(effect.X, effect.Y),
                    new Color(effect.Tint ?? "#ffffffff"),
                    effect.Ease,
                    effect.Muted));
            }
        }

        if (editor.HasComponent<LevelGroups>())
            editor.GetComponent<LevelGroups>() = levelGroups;
        else
            editor.AddComponent(levelGroups);
    }
}
