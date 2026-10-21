using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Systems.Physics;
using NVector2 = System.Numerics.Vector2;
using Position = Writer.Ecs.Position;

namespace Writer.Scenes;

/// <summary>Box2D-backed character test. Character size: 30 x 100 pixels.</summary>
public static class PhysicsScene
{
    private static readonly Vector2 CharacterSize = new(30, 100);

    public static void Build(
        EntityStore entities,
        Box2DPhysicsWorld physics,
        Vector2 viewportSize)
    {
        entities.CreateEntity(new VisualRid(), new Layer(0), Tags.Get<Uninitialised>());

        float width = viewportSize.X;
        float height = viewportSize.Y;

        AddStaticRect(
            entities,
            physics,
            center: new Vector2(width / 2, height - 30),
            size: new Vector2(width, 40),
            color: new Color("#546e7a"));

        AddStaticRect(
            entities,
            physics,
            center: new Vector2(0, height / 2),
            size: new Vector2(40, height),
            color: new Color("#546e7a"));

        AddStaticRect(
            entities,
            physics,
            center: new Vector2(width, height / 2),
            size: new Vector2(40, height),
            color: new Color("#546e7a"));

        var start = new Vector2(width * 0.2f, height * 0.45f);
        var body = physics.CreateDynamicBox(
            start,
            CharacterSize,
            mass: 1f,
            friction: 0.15f);

        entities.CreateEntity(
            new VisualRid(),
            new Rectangle(new Rect2(-CharacterSize / 2, CharacterSize)),
            new Drawable(0),
            new Position(start),
            new FillColor(Colors.Orange),
            new Character(body.Mass),
            new PhysicsBody(body),
            new Momentum(new NVector2(3f, -4f)),
            Tags.Get<Uninitialised, Dirty, ApplyMomentum>());

    }

    private static void AddStaticRect(
        EntityStore entities,
        Box2DPhysicsWorld physics,
        Vector2 center,
        Vector2 size,
        Color color)
    {
        physics.CreateStaticBox(center, size);

        entities.CreateEntity(
            new VisualRid(),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(0),
            new Position(center),
            new FillColor(color),
            Tags.Get<Uninitialised, Dirty>());
    }
}
