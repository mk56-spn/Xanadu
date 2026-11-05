using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Items;

namespace Xanadu.Levels;

/// <summary>Polygon items between the editor's entities and the level document; the item is cloned both ways.</summary>
public static partial class LevelDocumentMapper
{
    private static void CapturePolygons(EntityStore entities, LevelDocument document)
    {
        foreach (Entity entity in entities.Query<LevelPolygon>().ToEntityList())
        {
            LevelPolygon polygon = entity.GetComponent<LevelPolygon>();
            document.Polygons.Add(new PolygonDocument
            {
                X = polygon.Position.X,
                Y = polygon.Position.Y,
                Rotation = polygon.RotationDegrees,
                Item = XifCopy.Clone(polygon.Item ?? new Xif()),
                SelfModulate = SelfModulate.Packed(entity),
                Groups = GroupsOf(entity),
                Layer = ItemLayer.Of(entity).Layer,
                Z = ItemLayer.Of(entity).Z,
                Blend = ItemBlend.Of(entity),
                ShaderSource = ItemShaderSource.Of(entity),
                Shader = ItemShader.Of(entity),
            });
        }

        document.Polygons.Sort((a, b) =>
        {
            int x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Y.CompareTo(b.Y);
        });
    }

    private static void ApplyPolygons(EntityStore entities, LevelDocument document)
    {
        foreach (Entity entity in entities.Query<LevelPolygon>().ToEntityList())
            entity.DeleteEntity();

        foreach (PolygonDocument polygon in document.Polygons)
        {
            Entity created = EditorItems.Create(entities, new LevelPolygon(
                new Vector2(polygon.X, polygon.Y),
                XifCopy.Clone(polygon.Item ?? new Xif()),
                polygon.Rotation), polygon.Shader);
            Subscribe(created, polygon.Groups);
            ItemLayer.Put(created, new ItemLayer(polygon.Layer, polygon.Z));
            ItemBlend.Put(created, polygon.Blend);
            ItemShaderSource.Put(created, polygon.ShaderSource);
            SelfModulate.Put(created, polygon.SelfModulate);
        }
    }
}
