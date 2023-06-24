using Godot;

namespace XanaduProject.Composer;

[GlobalClass]
public partial class ThreatPolygon : Polygon2D
{
    private Area2D _body = new();
    private CollisionPolygon2D _hitBox = new();

    public ThreatPolygon()
    {
        AddChild(_body);
        _body.AddChild(_hitBox);
        
        _body.CollisionLayer = 8;
        Modulate = Colors.Red;

        _hitBox.Polygon = Polygon;
    }
}