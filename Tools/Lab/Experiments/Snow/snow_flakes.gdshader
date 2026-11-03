// Falling snow: four vertices per flake in one triangle array, never touched
// by the CPU. VERTEX/UV.x hold a seed position; the shader drifts it with TIME
// and wraps it into a box around the camera, so the snowfall never runs out.
// UV.y is the quad corner (0..3); COLOR.rg are per-flake random numbers.
// Flakes draw after the scene unsorted: small enough that it never shows.
shader_type canvas_item;

#include "res://Experiments/Snow/snow_view.gdshaderinc"

const vec3 BOX = vec3(26.0, 14.0, 26.0);

void vertex() {
	float phase = COLOR.r * 6.2831;
	float fall = 0.8 + COLOR.g * 0.7;
	vec3 p = vec3(VERTEX.x, UV.x, VERTEX.y)
		+ vec3(sin(TIME * 0.7 + phase) * 0.5 + TIME * 0.35, -TIME * fall, cos(TIME * 0.5 + phase) * 0.4);
	p = cam_pos + mod(p - cam_pos + BOX * 0.5, BOX) - BOX * 0.5;

	vec3 view = to_view(p);
	float corner = UV.y;
	float size = max(0.045 * focal / max(view.z, 0.01), 1.0);
	VERTEX = view.z < 0.3
		? vec2(-100.0)
		: floor(to_screen(view)) + vec2(mod(corner, 2.0), floor(corner / 2.0)) * size;
	COLOR = vec4(0.96, 0.97, 1.0, 1.0 - fog_at(view.z * 2.5));
}
