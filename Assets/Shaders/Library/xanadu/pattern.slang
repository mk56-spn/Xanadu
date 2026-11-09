// xanadu.pattern: lines, checkers and tiling in pixel units (ported from the
// legacy Patterns and UVUtils includes). Feed them ItemInput.centred or .local.
module pattern;
import xanadu.math;

/// One pixel's worth of the value's change, for anti-aliased edges.
public float aa(float x) { return fwidth(x); }

/// Vertical lines `width` pixels thick, `spacing` pixels apart.
public float verticalLines(float2 px, float width, float spacing)
{
    float x = mod(px.x, spacing);
    return smoothstep(width + aa(x), width - aa(x), abs(x - spacing * 0.5));
}

public float horizontalLines(float2 px, float width, float spacing)
{
    float y = mod(px.y, spacing);
    return smoothstep(width + aa(y), width - aa(y), abs(y - spacing * 0.5));
}

/// 0 or 1 in squares `size` pixels across.
public float checkerboard(float2 px, float size)
{
    float2 p = floor(px / size);
    return mod(p.x + p.y, 2.0);
}

/// A checkerboard whose squares split again: bits 0..2 of `mask` turn on up to three levels.
public float checkerboardRecursive(float2 px, float size, int mask)
{
    float2 p = px / size;
    float2 inside = frac(p);
    float2 id = floor(p);
    float parity = mod(id.x + id.y, 2.0);

    int levels = ((mask & 1) != 0 ? 1 : 0) + ((mask & 2) != 0 ? 1 : 0) + ((mask & 4) != 0 ? 1 : 0);
    for (int l = 0; l < levels; l++)
    {
        inside *= 2.0;
        float2 quadrant = floor(inside);
        parity = mod(parity + quadrant.x + quadrant.y, 2.0);
        inside = frac(inside);
    }

    return parity;
}

/// A position tiled every `s` units: where it falls in its tile (centred) and which tile.
public struct RepeatUV
{
    public float2 repeated;
    public float2 id;
};

public RepeatUV tileUvWithId(float2 p, float s)
{
    RepeatUV data;
    data.id = round(p / s);
    data.repeated = p - s * data.id;
    return data;
}
