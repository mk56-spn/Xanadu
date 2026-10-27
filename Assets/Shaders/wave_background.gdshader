shader_type canvas_item;

// Drifting sine ribbons over a slowly shifting red/green wash.
// Ported from a Shadertoy: uv is flipped so y runs up, as it did there.
const float WIDTH_FACTOR = 2.0;

uniform float speed = 0.4;

vec3 calc_sine(
		vec2 uv,
		float frequency,
		float amplitude,
		float offset,
		vec3 color,
		float width,
		float exponent,
		bool dir) {
	float angle = TIME * speed * frequency + uv.x * 0.75;
	float y = sin(angle) * amplitude + offset;
	float diff_y = y - uv.y;
	float dist = abs(diff_y);

	// Falloff is four times steeper on the far side of the ribbon.
	if ((dir && diff_y > 0.0) || (!dir && diff_y < 0.0)) {
		dist *= 4.0;
	}

	float scale = pow(smoothstep(width * WIDTH_FACTOR, 0.0, dist), exponent);
	return min(color * scale, color);
}

void fragment() {
	vec2 uv = vec2(UV.x, 1.0 - UV.y);
	vec3 grey = vec3(0.5);
	vec3 color = vec3(0.0);

	float t1 = sin(TIME / 20.0) / 3.14 + 0.2;
	float t2 = sin(TIME / 10.0) / 3.14 + 0.2;

	color += calc_sine(uv, 0.20, 0.20, 0.5, grey, 0.10, 15.0, false);
	color += calc_sine(uv, 0.40, 0.15, 0.5, grey, 0.10, 17.0, false);
	color += calc_sine(uv, 0.60, 0.15, 0.5, grey, 0.05, 23.0, false);

	color += calc_sine(uv, 0.26, 0.07, 0.3, grey, 0.10, 17.0, true);
	color += calc_sine(uv, 0.46, 0.07, 0.3, grey, 0.05, 23.0, true);
	color += calc_sine(uv, 0.58, 0.05, 0.3, grey, 0.20, 15.0, true);

	color.x += t1 * (1.0 - uv.y);
	color.y += t2 * (1.0 - uv.y);

	COLOR = vec4(color, 1.0);
}
