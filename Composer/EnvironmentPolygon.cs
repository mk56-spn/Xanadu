using Godot;

namespace XanaduProject.Composer;

[GlobalClass]
public partial class EnvironmentPolygon : Polygon2D
{
	private StaticBody2D _body = new StaticBody2D { CollisionLayer = 4 };
	private CollisionPolygon2D _hitBox = new CollisionPolygon2D { OneWayCollision = true };

	public EnvironmentPolygon()
	{
		AddChild(_body);
		_body.AddChild(_hitBox);

		_hitBox.Polygon = Polygon;
	}
}