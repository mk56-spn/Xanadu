using Godot;

namespace Xanadu.Lab;

/// <summary>
/// A white rect for a shader to paint. <see cref="Origin"/> is where the node's
/// origin (its pivot) sits inside the rect; zero puts it on the top-left corner,
/// so UV and local position line up.
/// </summary>
public partial class ShaderQuad : Node2D
{
    private readonly Vector2 _size;
    private readonly Vector2 _origin;

    public ShaderQuad() { }

    public ShaderQuad(Vector2 size, Vector2 origin = default)
    {
        _size = size;
        _origin = origin;
    }

    public override void _Draw() => DrawRect(new Rect2(-_origin, _size), Colors.White);
}
