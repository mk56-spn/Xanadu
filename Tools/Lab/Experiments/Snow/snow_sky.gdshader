// The sky behind everything: each pixel turns back into a view ray, so the
// horizon and the sun glow line up with the projected scene exactly.
shader_type canvas_item;

#include "res://Experiments/Snow/snow_view.gdshaderinc"

const vec3 TOP = vec3(0.13, 0.16, 0.30);
const vec3 SUN = vec3(1.0, 0.70, 0.48);

varying vec2 pixel;

void vertex() {
	pixel = VERTEX;
}

void fragment() {
	vec2 s = (pixel - screen_center) / focal;
	vec3 ray = normalize(cam_fwd + cam_right * s.x - cam_up * s.y);
	vec3 col = mix(FOG, TOP, smoothstep(0.0, 0.5, ray.y));
	float g = max(dot(ray, sun_dir), 0.0);
	col += SUN * (pow(g, 12.0) * 0.22 + pow(g, 300.0) * 0.7);
	COLOR = vec4(posterize(col, FRAGCOORD.xy), 1.0);
}
