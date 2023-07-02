using Godot;

namespace XanaduProject.Composer;

[GlobalClass]
public partial class ThreatPolygon : Polygon2D
{
    private Area2D body = new Area2D();
    private CollisionPolygon2D hitBox = new CollisionPolygon2D();

    public ThreatPolygon()
    {
        AddChild(body);
        body.AddChild(hitBox);
        
        body.CollisionLayer = 8;
        Modulate = Colors.Red;

        hitBox.Polygon = Polygon;
    }
}