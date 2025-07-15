
float checkerboard_aa(vec2 uv, float size) {
    vec2 pos = uv * size;
    vec2 f = fwidth(pos); // Get the width of one pixel in "pos" space
    vec2 p = floor(pos) + 0.5; // Center of the checker square

    // Use smoothstep for a soft transition at the edges
    vec2 v = smoothstep(p - f, p + f, pos);

    return mod(floor(pos).x + floor(pos).y, 2.0) == 0.0 ? v.x * v.y : (1.0 - v.x) * (1.0 - v.y);
}