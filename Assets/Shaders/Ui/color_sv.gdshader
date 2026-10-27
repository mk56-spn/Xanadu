shader_type canvas_item;

// Saturation (left to right) by value (bottom to top) at a fixed hue.
uniform float hue = 0.0;

vec3 hsv_to_rgb(vec3 c) {
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	COLOR = vec4(hsv_to_rgb(vec3(hue, UV.x, 1.0 - UV.y)), 1.0);
}
