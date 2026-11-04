// One jump flood pass: each cell looks `jump` cells away in 8 directions and
// keeps whichever seed is nearest. Halving jump from half the grid down to 1
// gives every cell its nearest solid cell in about log2(size) passes; the
// distance to it is how far a ray can safely skip.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D field_in : hint_blit_source0;   // xy nearest seed (cells)
uniform int jump;

#include "res://Experiments/Radiance/rc_common.gdshaderinc"

void blit() {
	vec2 p = FRAGCOORD.xy;
	ivec2 c = ivec2(p);
	vec2 best = vec2(FAR);
	float nearest = 1e20;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			ivec2 n = c + ivec2(x, y) * jump;
			if (outside(n))
				continue;
			vec2 seed = texelFetch(field_in, n, 0).xy;
			if (seed.x <= FAR * 0.5)
				continue;
			float d = dot(seed - p, seed - p);
			if (d < nearest) {
				nearest = d;
				best = seed;
			}
		}
	}
	COLOR0 = vec4(best, 0.0, 1.0);
}
