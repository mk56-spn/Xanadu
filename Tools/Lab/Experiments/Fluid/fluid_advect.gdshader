// Pass 1 of 4: semi-Lagrangian advection. Each cell traces its velocity back
// one step and pulls velocity and dye from where that lands, so it is stable at
// any step size. Vorticity confinement and the splats (pointer, jets) are added
// here too, then the pressure passes take the divergence back out.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D velocity_in : hint_blit_source0;   // xy velocity (cells/s)
uniform sampler2D dye_in : hint_blit_source1;        // rgb dye

uniform float dt;
uniform float time;
uniform float vorticity = 30.0;    // confinement strength: puts back the swirl advection smears out
uniform float velocity_fade = 0.2; // per second
uniform float dye_fade = 0.35;     // per second
uniform vec2 pointer_from;         // the pointer's segment this frame, in cells
uniform vec2 pointer_to;
uniform vec2 pointer_velocity;     // cells/s
uniform float pointer_on;          // 1 while the pointer is over the stage

#include "res://Experiments/Fluid/fluid_grid.gdshaderinc"

float curl(ivec2 c) {
	return 0.5 * ((at(velocity_in, c + ivec2(1, 0)).y - at(velocity_in, c - ivec2(1, 0)).y)
		- (at(velocity_in, c + ivec2(0, 1)).x - at(velocity_in, c - ivec2(0, 1)).x));
}

vec3 hue(float h) {
	return clamp(abs(fract(h + vec3(0.0, 2.0, 1.0) / 3.0) * 6.0 - 3.0) - 1.0, 0.0, 1.0);
}

// Weight of a soft brush of radius r (cells) swept along a -> b.
float brush(vec2 p, vec2 a, vec2 b, float r) {
	vec2 ab = b - a;
	float t = clamp(dot(p - a, ab) / max(dot(ab, ab), 1e-4), 0.0, 1.0);
	vec2 d = p - (a + ab * t);
	return exp(-dot(d, d) / (r * r));
}

void blit() {
	ivec2 c = ivec2(FRAGCOORD.xy);
	vec2 p = FRAGCOORD.xy;

	vec2 back = p - dt * texelFetch(velocity_in, c, 0).xy;
	vec2 vel = bilinear(velocity_in, back).xy * exp(-velocity_fade * dt);
	vec3 dye = bilinear(dye_in, back).rgb * exp(-dye_fade * dt);

	// Vorticity confinement: push along the curl's gradient, sideways to it.
	float w = curl(c);
	vec2 grad = vec2(abs(curl(c + ivec2(1, 0))) - abs(curl(c - ivec2(1, 0))),
		abs(curl(c + ivec2(0, 1))) - abs(curl(c - ivec2(0, 1)))) * 0.5;
	vec2 n = grad / (length(grad) + 1e-5);
	vel += vorticity * w * vec2(n.y, -n.x) * dt;

	// Two jets from the bottom corners, sweeping slowly; colours drift round the wheel.
	float r = grid.y * 0.025;
	for (int j = 0; j < 2; j++) {
		float side = j == 0 ? -1.0 : 1.0;
		vec2 origin = vec2(grid.x * (0.5 + side * 0.32), grid.y * 0.92);
		float angle = -1.5708 - side * (0.45 + 0.35 * sin(time * 0.7 + float(j) * 2.1));
		vec2 dir = vec2(cos(angle), sin(angle));
		float k = brush(p, origin, origin + dir * r, r);
		vel = mix(vel, dir * grid.y * 0.45, k * 0.6);
		dye += hue(time * 0.05 + float(j) * 0.5) * k * 10.0 * dt;
	}

	// The pointer drags the fluid along its path and leaves its own colour.
	float k = brush(p, pointer_from, pointer_to, r * 1.2) * pointer_on;
	vel += pointer_velocity * k * 0.8;
	dye += hue(time * 0.11 + 0.25) * k * min(length(pointer_velocity) * 0.02, 1.0);

	COLOR0 = vec4(vel, 0.0, 1.0);
	COLOR1 = vec4(dye, 1.0);
}
