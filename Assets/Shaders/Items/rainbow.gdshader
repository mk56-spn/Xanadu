shader_type canvas_item;

// Cycles the canvas item's hue over time, independent of its own color.
uniform float speed = 1.0;

vec3 hue_cycle(float hue) {
	vec3 rgb = clamp(abs(fract(hue + vec3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0, 0.0, 1.0);
	return rgb;
}

void fragment() {
	vec4 texture_color = texture(TEXTURE, UV);
	vec3 rainbow = hue_cycle(fract(TIME * speed));
	COLOR = vec4(texture_color.rgb * rainbow, texture_color.a);
}
