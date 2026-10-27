shader_type canvas_item;

// Placeholder preview art: slow amber plasma, dark toward the corners.
uniform vec4 base_color : source_color = vec4(0.04, 0.025, 0.0, 1.0);
uniform vec4 glow_color : source_color = vec4(1.0, 0.753, 0.208, 1.0);
uniform float speed = 0.25;
uniform float half_size = 250.0;

varying vec2 local_position;

void vertex() {
	local_position = VERTEX;
}

void fragment() {
	vec2 uv = local_position / half_size;
	float t = TIME * speed;
	vec2 p = uv * 3.0;

	float v = sin(p.x + t * 2.0)
		+ sin(p.y * 1.3 - t * 1.5)
		+ sin((p.x + p.y) * 0.8 + t)
		+ sin(length(p - vec2(sin(t), cos(t * 1.3)) * 1.5) * 2.0);
	v = v * 0.125 + 0.5;

	float vignette = smoothstep(1.7, 0.3, length(uv));
	vec3 color = mix(base_color.rgb, glow_color.rgb, v * v * 0.6 * vignette);
	COLOR = vec4(color, 1.0);
}
