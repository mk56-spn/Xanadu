// Shared by the radiance cascade passes and the view. Every cascade texture is
// the full grid laid out direction-first: cascade n splits it into a square of
// 2^(n+1) x 2^(n+1) tiles, one per ray direction (4^(n+1) of them), and each
// tile holds that direction's probes, spaced 2^(n+1) cells apart. Going up a
// cascade, probes get 2x sparser and directions 4x denser, so every cascade
// costs the same.
uniform vec2 grid;   // cells across, cells down

const float FAR = -1e5;   // the jump flood's "no seed yet"

bool outside(ivec2 c) {
	return any(lessThan(c, ivec2(0))) || any(greaterThanEqual(c, ivec2(grid)));
}

// One direction's radiance at probe-grid position q, bilinear between the four
// nearest probes of its tile. tiles is the cascade's tiles per side.
vec3 probe_bilinear(sampler2D t, int dir, int tiles, vec2 q) {
	ivec2 probes = ivec2(grid) / tiles;
	ivec2 origin = ivec2(dir % tiles, dir / tiles) * probes;
	q = clamp(q, vec2(0.0), vec2(probes - 1));
	ivec2 i = ivec2(floor(q));
	ivec2 j = min(i + 1, probes - 1);
	vec2 f = q - vec2(i);
	vec3 upper_row = mix(texelFetch(t, origin + i, 0).rgb, texelFetch(t, origin + ivec2(j.x, i.y), 0).rgb, f.x);
	vec3 lower_row = mix(texelFetch(t, origin + ivec2(i.x, j.y), 0).rgb, texelFetch(t, origin + j, 0).rgb, f.x);
	return mix(upper_row, lower_row, f.y);
}

// Where direction `dir`'s tile starts in a cascade with `tiles` tiles per side.
ivec2 tile_origin(int dir, int tiles) {
	return ivec2(dir % tiles, dir / tiles) * (ivec2(grid) / tiles);
}

// Marches the segment a -> b, skipping by the jump-flood distance. rgb: light
// of the first solid cell hit (black for a wall); a: 1 if nothing was hit.
vec4 march(sampler2D solids, sampler2D nearest, vec2 a, vec2 b) {
	vec2 ab = b - a;
	float len = length(ab);
	vec2 dir = ab / max(len, 1e-4);
	float t = 0.0;
	for (int s = 0; s < 96 && t <= len; s++) {
		vec2 p = a + dir * t;
		ivec2 c = ivec2(floor(p));
		if (outside(c))
			return vec4(0.0, 0.0, 0.0, 1.0);
		vec4 hit = texelFetch(solids, c, 0);
		if (hit.a > 0.5)
			return vec4(hit.rgb, 0.0);
		float d = length(texelFetch(nearest, c, 0).xy - p);
		t += max(d - 0.75, 0.5);
	}
	return vec4(0.0, 0.0, 0.0, 1.0);
}

// The four probes of a grid spaced `spacing` cells apart that surround cell-space
// point p, as probe indices and bilinear weights, clamped at the edges.
void surrounding(vec2 p, float spacing, ivec2 probes, out ivec2 cells[4], out float weights[4]) {
	vec2 q = clamp(p / spacing - 0.5, vec2(0.0), vec2(probes - 1));
	ivec2 i = ivec2(floor(q));
	ivec2 j = min(i + 1, probes - 1);
	vec2 f = q - vec2(i);
	cells[0] = i;
	cells[1] = ivec2(j.x, i.y);
	cells[2] = ivec2(i.x, j.y);
	cells[3] = j;
	weights[0] = (1.0 - f.x) * (1.0 - f.y);
	weights[1] = f.x * (1.0 - f.y);
	weights[2] = (1.0 - f.x) * f.y;
	weights[3] = f.x * f.y;
}
