using System;
using Box2DSharp.Collision.Shapes;
using Box2DSharp.Dynamics;
using Box2DSharp.Dynamics.Contacts;
using Xanadu.Ecs;

namespace Xanadu.Systems.Physics;

/// <summary>
/// Pure C# Box2D world. Screen coordinates are y-down pixels; physics
/// coordinates are y-down meters, so positions can be converted with a scale.
/// </summary>
public sealed class Box2DPhysicsWorld : IDisposable
{
    public const float PixelsPerMeter = 100f;
    public const float FixedTimeStep = 1f / 240f;

    /// <summary>The fixed downward pull, in metres per second squared.</summary>
    public const float DefaultGravity = 22.2f;

    /// <summary>The gravity momentum values are authored against, in metres per second squared.</summary>
    public const float MomentumReferenceGravity = 15f;

    /// <summary>
    /// Scales a launch speed so it reaches the height and distance it reached at
    /// <see cref="MomentumReferenceGravity"/>, only faster: height is v²/2g, so
    /// speed has to grow with the square root of gravity.
    /// </summary>
    public static float MomentumScale => MathF.Sqrt(DefaultGravity / MomentumReferenceGravity);

    /// <summary>The largest momentum a note can be authored with, the triple strength.</summary>
    public const float MaxMomentum = 1200f;

    /// <summary>
    /// The speed the strongest momentum note gives, in metres per second. The
    /// player can never go faster than this sideways or when falling.
    /// </summary>
    public static float MaxSpeed => MaxMomentum / PixelsPerMeter * MomentumScale;

    private readonly World _world;
    private float _accumulator;
    private bool _disposed;

    public Box2DPhysicsWorld()
    {
        _world = new World(new Vector2(0f, DefaultGravity));
        _world.SetContactListener(new OneWayContacts());
    }

    public World World => _world;

    public float Gravity => _world.Gravity.Y;

    public Body CreateDynamicBox(
        Vector2 centerPixels,
        Vector2 sizePixels,
        float mass = 1f,
        float friction = 0.25f)
    {
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
        AddMoverBox(body, sizePixels, 0f, mass, friction);
        return body;
    }

    /// <summary>
    /// Gives a body made by <see cref="CreateDynamicBox"/> a box whose bottom is
    /// raised by <paramref name="liftPixels"/>, its top staying where it was. The
    /// body's position stays the centre of the full <paramref name="sizePixels"/>
    /// box, and its mass stays the same.
    /// </summary>
    public static void RaiseBottom(Body body, Vector2 sizePixels, float liftPixels)
    {
        Fixture old = body.FixtureList[0];
        float mass = body.Mass;
        float friction = old.Friction;
        body.DestroyFixture(old);
        AddMoverBox(body, sizePixels, liftPixels, mass, friction);
    }

    /// <summary>
    /// Sets how much a body grips what it touches, including what it touches
    /// now: Box2D mixes a contact's friction once, when the contact begins.
    /// </summary>
    public void SetFriction(Body body, float friction)
    {
        Fixture fixture = body.FixtureList[0];
        if (fixture.Friction == friction)
            return;

        fixture.Friction = friction;
        foreach (Contact contact in _world.ContactManager.ContactList)
        {
            if (contact.FixtureA == fixture || contact.FixtureB == fixture)
                contact.ResetFriction();
        }
    }

    private static void AddMoverBox(Body body, Vector2 sizePixels, float liftPixels, float mass, float friction)
    {
        float width = sizePixels.X / PixelsPerMeter;
        float height = (sizePixels.Y - liftPixels) / PixelsPerMeter;
        var offset = new Vector2(0f, -liftPixels / 2 / PixelsPerMeter);

        var shape = new PolygonShape();
        shape.SetAsBox(width / 2, height / 2, offset, 0f);

        var fixtureDef = new FixtureDef
        {
            Shape = shape,
            Density = mass / MathF.Max(0.0001f, width * height),
            Friction = friction,
            Restitution = 0f,
            UserData = new OneWayMover(new Vector2(width / 2, height / 2), offset),
        };

        body.CreateFixture(fixtureDef);
    }

    public Body CreateStaticBox(
        Vector2 centerPixels,
        Vector2 sizePixels,
        float friction = 0.5f,
        BlockOneWay oneWay = BlockOneWay.Off)
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
            UserData = oneWay == BlockOneWay.Off
                ? null
                : new OneWaySurface(
                    oneWay,
                    new Vector2(sizePixels.X / PixelsPerMeter / 2, sizePixels.Y / PixelsPerMeter / 2)),
        };

        body.CreateFixture(fixtureDef);
        return body;
    }

    public void DestroyBody(Body body)
    {
        if (!_disposed && body is not null)
            _world.DestroyBody(body);
    }

    /// <summary>How far the clock is through the next fixed step, from 0 to 1.</summary>
    public float StepProgress => _accumulator / FixedTimeStep;

    /// <summary>Whether <see cref="Step"/> with this delta would advance the world.</summary>
    public bool WillStep(float delta) =>
        !_disposed && _accumulator + MathF.Min(delta, 0.25f) >= FixedTimeStep;

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

    public static Vector2 ToPhysics(Vector2 screenPosition) =>
        new(screenPosition.X / PixelsPerMeter, screenPosition.Y / PixelsPerMeter);

    public static Vector2 ToScreen(Vector2 physicsPosition) =>
        new(physicsPosition.X * PixelsPerMeter, physicsPosition.Y * PixelsPerMeter);

    // Box2DSharp keeps a body's gravity scale internal once the body exists.
    private static readonly System.Reflection.FieldInfo GravityScaleField =
        typeof(Body).GetField("GravityScale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(Body), "GravityScale");

    /// <summary>How much of the world's gravity pulls on <paramref name="body"/>: 1 is all of it, 0 none.</summary>
    public static float GravityScale(Body body) => (float)GravityScaleField.GetValue(body)!;

    public static void SetGravityScale(Body body, float scale)
    {
        if (GravityScale(body) != scale)
            GravityScaleField.SetValue(body, scale);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _world.Dispose();
    }
}
