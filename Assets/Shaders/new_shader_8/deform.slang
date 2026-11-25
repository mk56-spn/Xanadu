// DEFORM: deform runs on every corner before it lands; move v.position to move the shape.
import xanadu;

ItemVertex deform(ItemVertex v)
{
    v.position.y += sin(time * 3 + v.uv.x * 6) * 6;
    return v;
}
