// The Frutiger Aero backdrop: sky, sun rays, rolling hills and drifting bubbles.
// Knows nothing about the button drawn over it.
shader_type canvas_item;

#include "res://Experiments/Aero/aero_common.gdshaderinc"

uniform vec2 size;   // quad size, px

const vec3 SKY_TOP = vec3(0.05, 0.47, 0.88);
const vec3 SKY_LOW = vec3(0.78, 0.95, 1.00);
const vec3 SUN = vec3(1.00, 0.98, 0.86);
const vec3 HILL_FAR = vec3(0.47, 0.84, 0.36);
const vec3 HILL_NEAR = vec3(0.26, 0.70, 0.20);

varying vec2 pos;   // local px, 0 at the top-left corner

void vertex() {
	pos = VERTEX;
}

void fragment() {
	vec2 uv = pos / size;
	float aspect = size.x / size.y;
	vec3 c = mix(SKY_TOP, SKY_LOW, smoothstep(0.0, 0.8, uv.y));

	// Sun up and to the right, with slowly turning rays.
	vec2 to_sun = (uv - vec2(0.84, 0.10)) * vec2(aspect, 1.0);
	float d = length(to_sun);
	float ang = atan(to_sun.y, to_sun.x);
	float rays = pow(0.5 + 0.5 * sin(ang * 14.0 + TIME * 0.12), 8.0)
	           + 0.6 * pow(0.5 + 0.5 * sin(ang * 9.0 - TIME * 0.07), 10.0);
	c += SUN * (0.55 * exp(-d * 5.0) + 0.5 * exp(-d * 28.0) + 0.10 * rays * exp(-d * 1.2));

	// Two rolling hills, each with a sunlit crest.
	float aa = fwidth(uv.y) * 1.5;
	float h1 = 0.74 + 0.05 * sin(uv.x * 4.6 + 0.7) + 0.022 * sin(uv.x * 11.0 + 2.0);
	float h2 = 0.84 + 0.04 * sin(uv.x * 3.1 + 2.5) + 0.018 * sin(uv.x * 13.0 + 0.4);
	vec3 far = mix(HILL_FAR, HILL_FAR * 0.72, smoothstep(h1, 1.0, uv.y));
	far += vec3(0.6, 0.7, 0.3) * exp(-max(uv.y - h1, 0.0) * 70.0) * 0.35;
	c = mix(c, far, smoothstep(h1 - aa, h1 + aa, uv.y));
	vec3 near = mix(HILL_NEAR, HILL_NEAR * 0.6, smoothstep(h2, 1.0, uv.y));
	near += vec3(0.7, 0.9, 0.3) * exp(-max(uv.y - h2, 0.0) * 60.0) * 0.40;
	c = mix(c, near, smoothstep(h2 - aa, h2 + aa, uv.y));

	// Bokeh bubbles drifting upward.
	for (int i = 0; i < 26; i++) {
		float s = float(i) + 1.0;
		float r = mix(10.0, 46.0, hash(s * 3.1) * hash(s * 7.7));
		float speed = mix(0.025, 0.07, hash(s * 5.3));
		float x = hash(s * 1.7) * size.x + sin(TIME * 0.6 + s) * 14.0;
		float y = fract(hash(s * 2.9) - TIME * speed) * (size.y + 2.0 * r) - r;
		vec4 b = bubble(pos, vec2(x, y), r);
		c = mix(c, b.rgb, b.a);
	}

	COLOR = vec4(c, 1.0);
}
