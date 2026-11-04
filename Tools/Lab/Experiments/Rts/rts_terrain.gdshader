// Runs once, in two blits: lays rocks and broken walls into the nav texture,
// clear around every spawner. Every tile gets the same; distances start at
// NO_PATH. The second blit (masking) reads the first
// and writes each cell's open neighbours into w, since rock never changes.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D terrain_in : hint_blit_source0;
uniform vec4 bounds;
uniform bool masking;

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

bool rock_at(ivec2 texel) { return texelFetch(terrain_in, texel, 0).y > 0.5; }

vec4 mask(ivec2 texel) {
	ivec2 n = nav_size(bounds);
	ivec2 c = texel % n;
	vec4 self = texelFetch(terrain_in, texel, 0);
	int open = 0;
	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			ivec2 q = c + ivec2(dx, dy);
			if ((dx == 0 && dy == 0) || q.x < 0 || q.y < 0 || q.x >= n.x || q.y >= n.y)
				continue;
			if (rock_at(texel + ivec2(dx, dy)))
				continue;
			if (dx != 0 && dy != 0 && (rock_at(texel + ivec2(dx, 0)) || rock_at(texel + ivec2(0, dy))))
				continue;
			open |= neighbour_bit(dx, dy);
		}
	}
	return vec4(self.xyz, float(open));
}

vec4 terrain(ivec2 texel) {
	ivec2 n = nav_size(bounds);
	vec2 p = nav_centre(texel % n, bounds);

	// Rocky outcrops, and long walls along a noise contour, broken by gaps.
	bool rock = fractal(p / 1500.0, 11u) > 0.64;
	bool wall = abs(fractal(p / 3200.0, 23u) - 0.5) < 0.02 && noise(p / 420.0, 37u) > 0.3;
	bool blocked = rock || wall;
	for (int s = 0; s < SPAWNERS; s++) {
		if (distance(p, spawner_pos(s, bounds)) < CLEAR_RADIUS)
			blocked = false;
	}
	return vec4(NO_PATH, blocked ? 1.0 : 0.0, 0.0, 0.0);
}

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	COLOR0 = masking ? mask(texel) : terrain(texel);
}
