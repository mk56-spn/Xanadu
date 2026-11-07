// Shared by the Aero sky and button.

float hash(float n) {
	return fract(sin(n * 91.3458) * 47453.5453);
}

// A soap bubble: faint body, bright rim, a glint up-left. Colour and alpha.
vec4 bubble(vec2 p, vec2 c, float r) {
	float d = length(p - c);
	float aa = fwidth(d);
	float inside = 1.0 - smoothstep(r - aa, r + aa, d);
	float rim = exp(-abs(d - r * 0.93) / (r * 0.06));
	float glint = exp(-length(p - c - vec2(-0.38, -0.42) * r) / (r * 0.12));
	float a = inside * (0.07 + 0.30 * rim + 0.75 * glint);
	return vec4(mix(vec3(0.85, 1.0, 1.0), vec3(1.0), glint), a);
}
