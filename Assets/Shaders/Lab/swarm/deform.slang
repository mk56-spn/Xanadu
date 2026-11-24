// DEFORM: runs once per dot. The pool hands over the dot being drawn as `item`;
// deform puts the quad's corners round it and gives it its colour.
import xanadu;

ItemVertex deform(ItemVertex v)
{
    float2 offset = (v.uv - 0.5) * item.size;
    v.position = v.rest.position + item.position + offset;
    v.local = offset;
    v.colour = colours.hsv(item.hue, 0.6, 1);
    return v;
}
