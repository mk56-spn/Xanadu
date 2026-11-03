// Runs once: lays rocks and broken walls into the nav texture, clear around
// every spawner, and marks the cells each spawner seeds its owner's home
// field from (z: spawner + 1, 0 for none). Every tile gets the same; distances
// start at NO_PATH.
shader_type texture_blit;
render_mode blend_disabled;

uniform vec4 bounds;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"

uint hash(uint x) {
	x ^= x >> 16u; x *= 0x7feb352du;
	x ^= x >> 15u; x *= 0x846ca68bu;
	x ^= x >> 16u;
	return x;
}

float lattice(ivec2 c, uint seed) {
	return float(hash(uint(c.x) * 73856093u ^ uint(c.y) * 19349663u ^ seed) >> 8u) * (1.0 / 16777216.0);
}

float noise(vec2 p, uint seed) {
	vec2 i = floor(p);
	vec2 f = p - i;
	f = f * f * (3.0 - 2.0 * f);
	ivec2 c = ivec2(i);
	float a = mix(lattice(c, seed), lattice(c + ivec2(1, 0), seed), f.x);
	float b = mix(lattice(c + ivec2(0, 1), seed), lattice(c + ivec2(1, 1), seed), f.x);
	return mix(a, b, f.y);
}

float fractal(vec2 p, uint seed) {
	return noise(p, seed) * 0.6 + noise(p * 2.1, seed + 1u) * 0.3 + noise(p * 4.3, seed + 2u) * 0.1;
}

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	ivec2 n = nav_size(bounds);
	vec2 p = nav_centre(texel % n, bounds);

	// Rocky outcrops, and long walls along a noise contour, broken by gaps.
	bool rock = fractal(p / 1500.0, 11u) > 0.64;
	bool wall = abs(fractal(p / 3200.0, 23u) - 0.5) < 0.02 && noise(p / 420.0, 37u) > 0.3;
	bool blocked = rock || wall;
	float seed = 0.0;
	ivec2 c = texel % n;
	for (int s = 0; s < SPAWNERS; s++) {
		vec2 sp = spawner_pos(s, bounds);
		if (distance(p, sp) < CLEAR_RADIUS)
			blocked = false;
		ivec2 g = nav_cell(sp, bounds);
		if (max(abs(c.x - g.x), abs(c.y - g.y)) <= 1)
			seed = float(s + 1);
	}
	COLOR0 = vec4(NO_PATH, blocked ? 1.0 : 0.0, seed, 0.0);
}
