// The world, rebuilt every frame from shapes: COLOR0 is emission in rgb and
// solidity in a (lights and walls are both solid), COLOR1 seeds the jump flood
// with each solid cell's own position.
shader_type texture_blit;
render_mode blend_disabled;

uniform float time;
uniform vec2 pointer;        // cells
uniform float pointer_on;    // 1 while the pointer is over the stage
uniform float power = 6.0;   // light emission

#include "res://Experiments/Radiance/rc_common.gdshaderinc"

float circle(vec2 p, vec2 c, float r) {
	return length(p - c) - r;
}

float box(vec2 p, vec2 c, vec2 half_size, float angle) {
	p -= c;
	float s = sin(angle), k = cos(angle);
	p = mat2(vec2(k, -s), vec2(s, k)) * p;
	vec2 d = abs(p) - half_size;
	return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

// A ring of radius r and half thickness w, cut into `arcs` pieces with gaps.
float broken_ring(vec2 p, float r, float w, float arcs, float gap, float spin) {
	float a = atan(p.y, p.x) + spin;
	float cell = TAU / arcs;
	float local = mod(a, cell) - cell * 0.5;
	float ring = abs(length(p) - r) - w;
	float cut = length(p) * (cell * 0.5 - gap - abs(local));
	return max(ring, -cut);
}

void blit() {
	vec2 cell = FRAGCOORD.xy;
	// Height spans -0.5..0.5 so shapes keep their proportions at any grid size.
	vec2 p = (cell - grid * 0.5) / grid.y;

	vec3 emit = vec3(0.0);
	bool solid = false;

	// Walls: a spinning broken ring, an inner cross, pillars and two drifting bars.
	float wall = broken_ring(p, 0.2, 0.012, 5.0, 0.12, time * 0.25);
	wall = min(wall, box(p, vec2(0.0), vec2(0.07, 0.008), -time * 0.6));
	wall = min(wall, box(p, vec2(0.0), vec2(0.008, 0.07), -time * 0.6));
	for (int i = 0; i < 8; i++) {
		float a = float(i) / 8.0 * TAU + 0.2;
		wall = min(wall, box(p, vec2(cos(a), sin(a)) * vec2(0.62, 0.36), vec2(0.018), a));
	}
	wall = min(wall, box(p, vec2(-0.45 + 0.08 * sin(time * 0.4), -0.05), vec2(0.006, 0.16), 0.3));
	wall = min(wall, box(p, vec2(0.45, 0.08 + 0.06 * sin(time * 0.5)), vec2(0.006, 0.14), -0.4));
	if (wall < 0.0)
		solid = true;

	// Lights: three orbiting coloured ones and a warm one on the pointer.
	vec3 colours[3] = vec3[](vec3(0.2, 0.7, 1.0), vec3(1.0, 0.25, 0.55), vec3(0.4, 1.0, 0.3));
	for (int i = 0; i < 3; i++) {
		float a = time * (0.3 + 0.07 * float(i)) + float(i) * TAU / 3.0;
		vec2 c = vec2(cos(a) * 0.36, sin(a) * 0.3);
		if (circle(p, c, 0.014) < 0.0) {
			emit = colours[i] * power;
			solid = true;
		}
	}
	vec2 warm = pointer_on > 0.5
		? (pointer - grid * 0.5) / grid.y
		: vec2(sin(time * 0.37) * 0.55, cos(time * 0.23) * 0.38);
	if (circle(p, warm, 0.02) < 0.0) {
		emit = vec3(1.0, 0.8, 0.55) * power;
		solid = true;
	}

	COLOR0 = vec4(emit, solid ? 1.0 : 0.0);
	COLOR1 = solid ? vec4(cell, 0.0, 1.0) : vec4(FAR, FAR, 0.0, 1.0);
}
