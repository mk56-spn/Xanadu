// How a particle looks from its life (x age, y lifetime, z heading), shared by
// both particle tabs so they draw identically. Small, so drawing stays cheap
// and overlap stays low next to the step.

const float SIZE = 3.0;          // canvas px, tip to centre
const vec3 HOT = vec3(1.00, 0.78, 0.25);
const vec3 COOL = vec3(0.20, 0.55, 1.00);

// 0..1 over the particle's life; below 0 before birth.
float life_t(vec4 state) {
	return state.x / max(state.y, 1e-4);
}

// Unborn and dead particles shrink to a point and cover no pixels.
float particle_scale(vec4 state) {
	float t = life_t(state);
	return SIZE * step(0.0, t) * step(t, 1.0);
}

// Fades in and out, so births and deaths don't pop.
vec4 particle_color(vec4 state) {
	float t = clamp(life_t(state), 0.0, 1.0);
	return vec4(mix(HOT, COOL, t), 0.9 * smoothstep(0.0, 0.1, t) * (1.0 - smoothstep(0.8, 1.0, t)));
}
