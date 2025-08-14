#ifndef PATTERNS_GDSHADERINC

////////////////////////////////////////////////////////////////////////////////
//  Pixel-perfect 2-D pattern helpers                                         //
//  Drop-in include for Godot 4.x canvas_item shaders                         //
////////////////////////////////////////////////////////////////////////////////

// Anti-aliasing helper (1 px wide edge)
float aa(float x) { return fwidth(x); }

// --- Vertical / Horizontal lines -------------------------------------------
// width   -> thickness in **texture pixels**
// spacing -> distance between line centers in **texture pixels**
float vertical_lines(vec2 uv_px, float width, float spacing)
{
    float x = mod(uv_px.x, spacing);
    return smoothstep(width + aa(x), width - aa(x), abs(x - spacing * 0.5));
}

float horizontal_lines(vec2 uv_px, float width, float spacing)
{
    float y = mod(uv_px.y, spacing);
    return smoothstep(width + aa(y), width - aa(y), abs(y - spacing * 0.5));
}

// --- Checkerboard -----------------------------------------------------------
// size -> width/height of one checker in **texture pixels**
float checkerboard(vec2 uv_px, float size)
{
    vec2 p = floor(uv_px / size);
    return mod(p.x + p.y, 2.0);        // 0 or 1
}
////////////////////////////////////////////////////////////////////////////////
// Recursive checkerboard (up to 3 extra levels inside any square)
// size          -> pixel width/height of the *top-level* checker
// subdivide_mask-> bit mask (bits 0-2) → which levels are enabled
//                 1 = one extra split, 2 = two splits, 4 = three splits
//                 e.g. 7 = full 3-level recursion
float checkerboard_recursive(vec2 uv_px, float size, int subdivide_mask)
{
    vec2 p = uv_px / size;          // 0..1 inside top square
    vec2 fract_p = fract(p);        // fractional part inside the square

    // Determine *this* square’s parity
    vec2 id = floor(p);
    float parity = mod(id.x + id.y, 2.0);

    // Decide how many recursive levels this square should do
    int levels = 0;
    levels += (subdivide_mask & 1) != 0 ? 1 : 0;
    levels += (subdivide_mask & 2) != 0 ? 1 : 0;
    levels += (subdivide_mask & 4) != 0 ? 1 : 0;

    // Apply recursive subdivision
    for (int l = 0; l < levels; l++)
    {
        fract_p *= 2.0;                    // zoom into the quadrant
        vec2 quadrant = floor(fract_p);    // 0..1 integer quadrant
        parity = mod(parity + quadrant.x + quadrant.y, 2.0);
        fract_p = fract(fract_p);          // 0..1 inside the quadrant
    }

    return parity;  // 0 or 1
}

#endif // PATTERNS_GDSHADERINC