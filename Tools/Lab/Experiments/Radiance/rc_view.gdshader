// Per pixel, the last merge: march to each of the four nearest cascade 0 probes
// and take their light (all four directions) only if the way is clear. So walls
// cut light at pixel precision, not at the probes' 2-cell spacing.
shader_type canvas_item;

uniform sampler2D scene : filter_nearest;
uniform sampler2D field : filter_nearest;
uniform sampler2D cascade0 : filter_nearest;
uniform int view = 0;            // 0 light, 1 distance, 2 scene
uniform float exposure = 1.6;

#include "res://Experiments/Radiance/rc_common.gdshaderinc"

vec3 probe_light(ivec2 probe) {
	vec3 sum = vec3(0.0);
	for (int d = 0; d < 4; d++)
		sum += texelFetch(cascade0, tile_origin(d, 2) + probe, 0).rgb;
	return sum * 0.25;
}

void fragment() {
	vec2 p = UV * grid;
	ivec2 c = clamp(ivec2(p), ivec2(0), ivec2(grid) - 1);
	vec4 s = texelFetch(scene, c, 0);
	ivec2 probes = ivec2(grid) / 2;
	ivec2 cells[4];
	float weights[4];
	vec3 col;
	if (view == 0) {
		vec3 light = vec3(0.0);
		bool lamp = s.a > 0.5 && dot(s.rgb, vec3(1.0)) > 0.0;
		if (lamp) {
			light = s.rgb;
		} else if (s.a > 0.5) {
			// Walls: the plain blend of nearby probes, which reaches in past the
			// surface, so edges facing a light catch it.
			surrounding(p, 2.0, probes, cells, weights);
			for (int k = 0; k < 4; k++)
				light += weights[k] * probe_light(cells[k]);
			light = light * 0.5 + 0.004;
		} else {
			surrounding(p, 2.0, probes, cells, weights);
			for (int k = 0; k < 4; k++) {
				vec4 seg = march(scene, field, p, (vec2(cells[k]) + 0.5) * 2.0);
				light += weights[k] * (seg.rgb + seg.a * probe_light(cells[k]));
			}
		}
		col = 1.0 - exp(-light * exposure);
		// A little noise so the smooth falloff doesn't band in 8 bits.
		col += (fract(sin(dot(FRAGCOORD.xy, vec2(12.9898, 78.233))) * 43758.5453) - 0.5) / 255.0;
	} else if (view == 1) {
		float d = length(texelFetch(field, c, 0).xy - p);
		col = vec3(0.25, 0.5, 1.0) * (0.55 + 0.45 * cos(d * 0.35)) * exp(-d / 120.0);
		col = mix(col, vec3(1.0), step(d, 0.75));
	} else {
		col = s.a > 0.5 ? (dot(s.rgb, vec3(1.0)) > 0.0 ? s.rgb / 6.0 : vec3(0.5)) : vec3(0.03);
	}
	COLOR = vec4(col, 1.0);
}
