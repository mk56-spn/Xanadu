// Reads the screen through a wave and tints it green. Over stripes it shows wavy
// stripes; over an empty buffer it shows dark green.
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear;

void fragment() {
	vec2 uv = SCREEN_UV + vec2(sin(SCREEN_UV.y * 90.0) * 0.008, 0.0);
	vec4 behind = texture(screen_texture, uv);
	COLOR = vec4(mix(behind.rgb, vec3(0.2, 1.0, 0.3), 0.3), 1.0);
}
