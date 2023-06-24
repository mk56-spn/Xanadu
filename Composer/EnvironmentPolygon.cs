using Godot;

namespace XanaduProject.Composer;

[GlobalClass]
public partial class EnvironmentPolygon : Polygon2D
{
	private StaticBody2D _body = new() { CollisionLayer = 4 };
	private CollisionPolygon2D _hitBox = new() { OneWayCollision = true };

	public EnvironmentPolygon()
	{
		AddChild(_body);
		_body.AddChild(_hitBox);

		_hitBox.Polygon = Polygon;
	}
}