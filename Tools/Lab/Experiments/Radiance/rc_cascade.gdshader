// One cascade, run from the top cascade down to 0. Each texel is one ray of one
// probe; cascade n covers distances (4^n - 1)/3 to (4^(n+1) - 1)/3 base
// intervals, so near light gets fine positions and far light fine angles.
//
// Merging uses the bilinear fix: rather than blending the four coarser probes
// around this one (which leaks light through walls between them), the ray is
// marched once toward each of them, ending where that probe's own interval
// starts. Light from a coarse probe only counts if that path is clear.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D scene_in : hint_blit_source0;   // rgb emission, a solid
uniform sampler2D field_in : hint_blit_source1;   // xy nearest solid cell
uniform sampler2D upper_in : hint_blit_source2;   // cascade n + 1, already merged
uniform int cascade;
uniform int top;                   // highest cascade in use; its misses see the sky
uniform float interval0 = 2.0;     // cascade 0's ray length, in cells
uniform vec3 sky = vec3(0.01, 0.014, 0.03);

#include "res://Experiments/Radiance/rc_common.gdshaderinc"

vec3 radiance(ivec2 c) {
	int tiles = 2 << cascade;
	ivec2 probes = ivec2(grid) / tiles;
	ivec2 tile = c / probes;
	int dir = tile.y * tiles + tile.x;
	float angle = (float(dir) + 0.5) / float(tiles * tiles) * TAU;
	vec2 d = vec2(cos(angle), sin(angle));
	vec2 origin = (vec2(c - tile * probes) + 0.5) * float(tiles);

	float scale = pow(4.0, float(cascade));
	float start = interval0 * (scale - 1.0) / 3.0;
	float end = start + interval0 * scale;

	if (cascade >= top) {
		vec4 ray = march(scene_in, field_in, origin + d * start, origin + d * end);
		return ray.rgb + ray.a * sky;
	}

	int upper = tiles * 2;
	ivec2 upper_probes = ivec2(grid) / upper;
	ivec2 cells[4];
	float weights[4];
	surrounding(origin, float(upper), upper_probes, cells, weights);

	vec3 sum = vec3(0.0);
	for (int k = 0; k < 4; k++) {
		if (weights[k] <= 0.0)
			continue;
		vec2 probe = (vec2(cells[k]) + 0.5) * float(upper);
		vec4 ray = march(scene_in, field_in, origin + d * start, probe + d * end);
		vec3 above = vec3(0.0);
		for (int s = 0; s < 4; s++)
			above += texelFetch(upper_in, tile_origin(dir * 4 + s, upper) + cells[k], 0).rgb;
		sum += weights[k] * (ray.rgb + ray.a * above * 0.25);
	}
	return sum;
}

void blit() {
	COLOR0 = vec4(radiance(ivec2(FRAGCOORD.xy)), 1.0);
}
