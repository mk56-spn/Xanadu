// Shared by every fluid blit. The grid is one texel per cell; positions are in
// cell units with cell centres at +0.5, the same space as FRAGCOORD.
uniform vec2 grid;   // cells across, cells down

ivec2 clamp_cell(ivec2 c) {
	return clamp(c, ivec2(0), ivec2(grid) - 1);
}

// A neighbour read; past the walls it repeats the edge cell.
vec4 at(sampler2D t, ivec2 c) {
	return texelFetch(t, clamp_cell(c), 0);
}

bool outside(ivec2 c) {
	return any(lessThan(c, ivec2(0))) || any(greaterThanEqual(c, ivec2(grid)));
}

// Bilinear read at a cell-space position, done by hand so it never depends on
// the blit source's filter.
vec4 bilinear(sampler2D t, vec2 p) {
	p = clamp(p - 0.5, vec2(0.0), grid - 1.0);
	ivec2 i = ivec2(floor(p));
	vec2 f = p - vec2(i);
	vec4 top = mix(at(t, i), at(t, i + ivec2(1, 0)), f.x);
	vec4 bottom = mix(at(t, i + ivec2(0, 1)), at(t, i + ivec2(1, 1)), f.x);
	return mix(top, bottom, f.y);
}
