using Godot;

namespace XanaduProject.Composer;

[GlobalClass]
public partial class EnvironmentPolygon : Polygon2D
{
	private StaticBody2D body = new StaticBody2D { CollisionLayer = 4 };
	private CollisionPolygon2D hitBox = new CollisionPolygon2D { OneWayCollision = true };

	public EnvironmentPolygon()
	{
		AddChild(body);
		body.AddChild(hitBox);

		hitBox.Polygon = Polygon;
	}
}