using Godot;

namespace XanaduProject.Perceptions;

public abstract partial class Perception : CharacterBody2D
{
    protected int Gravity;

    private const int BaseVelocity = 700;

    protected Perception()
    {
        Variant fetchGravity = ProjectSettings.GetSetting("physics/2d/default_gravity");
        Gravity = fetchGravity.AsInt32();

        Velocity = new Vector2(BaseVelocity, 0);
    }
}