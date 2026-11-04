// Shows the latest buffers on the stage, stretched with linear filtering.
shader_type canvas_item;

uniform sampler2D dye : filter_linear;
uniform sampler2D velocity : filter_linear;
uniform sampler2D pressure : filter_linear;
uniform int view = 0;             // 0 dye, 1 velocity, 2 pressure
uniform float speed_scale = 0.01; // 1 / cells per second shown at full brightness

vec3 hue(float h) {
	return clamp(abs(fract(h + vec3(0.0, 2.0, 1.0) / 3.0) * 6.0 - 3.0) - 1.0, 0.0, 1.0);
}

void fragment() {
	vec3 c;
	if (view == 0) {
		vec3 d = texture(dye, UV).rgb;
		c = vec3(0.02, 0.025, 0.04) + (1.0 - exp(-d * 1.6));
	} else if (view == 1) {
		vec2 v = texture(velocity, UV).xy;
		c = hue(atan(v.y, v.x) / 6.2831853 + 0.5) * (1.0 - exp(-length(v) * speed_scale * 3.0));
	} else {
		float p = texture(pressure, UV).x * 0.05;
		c = p > 0.0 ? vec3(1.0, 0.45, 0.2) * (1.0 - exp(-p)) : vec3(0.2, 0.5, 1.0) * (1.0 - exp(p));
	}
	COLOR = vec4(c, 1.0);
}
