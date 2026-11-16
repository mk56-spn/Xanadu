namespace Xanadu.Model.Physics;

/// <summary>How physics units map to the playfield, for whoever converts between them: the physics world, movement rules and the code that previews a fall.</summary>
public static class PhysicsScale
{
    public const float PixelsPerMeter = 100f;

    /// <summary>The fixed downward pull, in metres per second squared.</summary>
    public const float DefaultGravity = 22.2f;

    /// <summary>The largest momentum a note can be authored with, the triple strength.</summary>
    public const float MaxMomentum = 1200f;
}
