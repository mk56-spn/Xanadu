// Inverts whatever the screen read gives it.
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;

void fragment() {
	COLOR = vec4(1.0 - texture(screen_texture, SCREEN_UV).rgb, 1.0);
}
