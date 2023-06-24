using System;
using Godot;

namespace XanaduProject.Perceptions;

public partial class Core : Perception
{
    private const int JumpVelocity = -1900;

    private Tween _rotationTween;

    private Area2D _nucleus;
    private Polygon2D _body;

    public override void _Ready()
    {
        base._Ready();
        
        _body = GetNode<Polygon2D>("Body");
        _nucleus = GetNode<Area2D>("Nucleus");

        GetNode<Area2D>("Shell").AreaShapeEntered += (_, _, _, _) => SetPhysicsProcess(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        
        nucleus_collision();

        if (IsOnFloor())
            ground_movement();
        else
            air_movement(delta);

        MoveAndSlide();
    }

    private void ground_movement()
    {
        grounded_rotation();

        if (Input.IsActionPressed("main"))
            Velocity = new Vector2(Velocity.X, JumpVelocity);
    }

    private void grounded_rotation()
    {
        float targetRotation = Mathf.Snapped(_body.RotationDegrees, 90);

        if (_rotationTween != null || !(Math.Abs(_body.RotationDegrees - targetRotation) > 0.01)) return;
        
        _rotationTween = CreateTween();
        _rotationTween.TweenProperty(_body, "rotation_degrees", targetRotation, 0.1);
    }

    private void air_movement(double delta)
    {

        _rotationTween?.Kill();
        _rotationTween = null;
        
        _body.Rotate(Mathf.DegToRad(360 * (float)delta));

        Velocity = new Vector2(Velocity.X, Mathf.Min(1500, Velocity.Y + Gravity * (float)delta));
    }
    private void nucleus_collision() =>
        _nucleus.Modulate = _nucleus.HasOverlappingBodies() ? Colors.Red : Colors.Green;
    
}