using System;
using Box2DSharp.Collision.Shapes;
using Box2DSharp.Dynamics;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace Xanadu.Systems.Physics;

/// <summary>
/// Pure C# Box2D world. Screen coordinates are y-down pixels; physics
/// coordinates are y-down meters, so positions can be converted with a scale.
/// </summary>
public sealed class Box2DPhysicsWorld : IDisposable
{
    public const float PixelsPerMeter = 100f;
    public const float FixedTimeStep = 1f / 60f;

    /// <summary>The fixed downward pull, in metres per second squared.</summary>
    public const float DefaultGravity = 15f;

    private readonly World _world;
    private float _accumulator;
    private bool _disposed;

    public Box2DPhysicsWorld()
    {
        _world = new World(new NVector2(0f, DefaultGravity));
    }

    public World World => _world;

    public float Gravity => _world.Gravity.Y;

    public Body CreateDynamicBox(
        Vector2 centerPixels,
        Vector2 sizePixels,
        float mass = 1f,
        float friction = 0.25f)
    {
        float width = sizePixels.X / PixelsPerMeter;
        float height = sizePixels.Y / PixelsPerMeter;

        var bodyDef = new BodyDef
        {
            BodyType = BodyType.DynamicBody,
            Position = ToPhysics(centerPixels),
            FixedRotation = true,
            AllowSleep = false,
            Awake = true,
            LinearDamping = 0.05f,
        };

        Body body = _world.CreateBody(bodyDef);

        var shape = new PolygonShape();
        shape.SetAsBox(width / 2, height / 2);

        var fixtureDef = new FixtureDef
        {
            Shape = shape,
            Density = mass / MathF.Max(0.0001f, width * height),
            Friction = friction,
            Restitution = 0f,
        };

        body.CreateFixture(fixtureDef);
        return body;
    }

    public Body CreateStaticBox(
        Vector2 centerPixels,
        Vector2 sizePixels,
        float friction = 0.5f)
    {
        var bodyDef = new BodyDef
        {
            BodyType = BodyType.StaticBody,
            Position = ToPhysics(centerPixels),
            Awake = true,
        };

        Body body = _world.CreateBody(bodyDef);

        var shape = new PolygonShape();
        shape.SetAsBox(
            sizePixels.X / PixelsPerMeter / 2,
            sizePixels.Y / PixelsPerMeter / 2);

        var fixtureDef = new FixtureDef
        {
            Shape = shape,
            Density = 0f,
            Friction = friction,
            Restitution = 0f,
        };

        body.CreateFixture(fixtureDef);
        return body;
    }

    public void DestroyBody(Body body)
    {
        if (!_disposed && body is not null)
            _world.DestroyBody(body);
    }

    public void Step(float delta)
    {
        if (_disposed)
            return;

        _accumulator += MathF.Min(delta, 0.25f);

        while (_accumulator >= FixedTimeStep)
        {
            _world.Step(FixedTimeStep, 8, 3);
            _accumulator -= FixedTimeStep;
        }
    }

    public static NVector2 ToPhysics(Vector2 screenPosition) =>
        new(screenPosition.X / PixelsPerMeter, screenPosition.Y / PixelsPerMeter);

    public static Vector2 ToScreen(NVector2 physicsPosition) =>
        new(physicsPosition.X * PixelsPerMeter, physicsPosition.Y * PixelsPerMeter);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _world.Dispose();
    }
}
