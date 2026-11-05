using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Levels;

/// <summary>Grass patches between the editor's entities and the level document.</summary>
public static partial class LevelDocumentMapper
{
    private static void CaptureGrass(EntityStore entities, LevelDocument document)
    {
        foreach (Entity entity in entities.Query<GrassPatch>().ToEntityList())
            document.Grass.Add(ToDocument(entity.GetComponent<GrassPatch>(), entity));

        document.Grass.Sort((a, b) =>
        {
            int x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Y.CompareTo(b.Y);
        });
    }

    private static void ApplyGrass(EntityStore entities, LevelDocument document)
    {
        foreach (Entity entity in entities.Query<GrassPatch>().ToEntityList())
            entity.DeleteEntity();

        foreach (GrassDocument grass in document.Grass)
        {
            Entity created = EditorItems.Create(entities, ToPatch(grass), grass.Shader);
            Subscribe(created, grass.Groups);
            SelfModulate.Put(created, grass.SelfModulate);
        }
    }

    private static GrassDocument ToDocument(GrassPatch patch, Entity entity) => new()
    {
        X = patch.Position.X,
        Y = patch.Position.Y,
        Width = patch.Width,
        Length = patch.Length,
        Density = patch.Density,
        Sway = patch.Sway,
        Color = patch.Color.ToRgba32(),
        Variation = patch.Variation,
        Seed = patch.Seed,
        Rotation = patch.RotationDegrees,
        SelfModulate = SelfModulate.Packed(entity),
        Groups = GroupsOf(entity),
        Shader = ItemShader.Of(entity),
    };

    /// <summary>The patch a document describes, its settings brought into range.</summary>
    public static GrassPatch ToPatch(GrassDocument grass) => new GrassPatch(new Vector2(grass.X, grass.Y), grass.Seed)
    {
        Width = grass.Width,
        Length = grass.Length,
        Density = grass.Density,
        Sway = grass.Sway,
        Color = new Color(grass.Color),
        Variation = grass.Variation,
        RotationDegrees = grass.Rotation,
    }.Clamped();
}
