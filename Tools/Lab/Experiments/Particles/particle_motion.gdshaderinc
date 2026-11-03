// One particle's step, shared by both particle tabs so they behave identically:
// the texture_blit version (one texel per particle) and the GPUParticles2D
// version (one particle-shader invocation per particle).
//
// Particles drift through a curl-noise flow over the whole field and are
// pushed away from the mouse. The step is deliberately heavy and the particles
// small and spread out, so the simulation costs far more than the drawing.
//
// life: x age, y lifetime (s), z heading (rad), w unused. Lifetime 0 means never
// born. Callers declare the uniforms emitter, emitter_velocity, bounds, dt,
// time and substeps.

const int OCTAVES = 4;
const float FLOW_SCALE = 1.0 / 260.0;   // noise cycles per canvas px
const float FLOW_SPEED = 240.0;         // px/s the flow pulls toward
const float FLOW_DRIFT = 0.15;          // how fast the field itself changes
const float RESPONSE = 3.0;             // per second: how quickly velocity follows the flow
const float PUSH_RADIUS = 220.0;
const float PUSH = 2600.0;              // px/s² at the mouse, falling to 0 at the radius
const float INHERIT = 0.5;              // share of the mouse's velocity a push passes on
const float LIFE_MIN = 2.0;
const float LIFE_MAX = 6.0;

uint hash(uint x) {
	x ^= x >> 16u; x *= 0x7feb352du;
	x ^= x >> 15u; x *= 0x846ca68bu;
	x ^= x >> 16u;
	return x;
}

float rand(inout uint s) {
	s = hash(s);
	return float(s >> 8u) * (1.0 / 16777216.0);
}

// Smooth value noise in 0..1, on a lattice moving through a third axis z.
float noise3(vec3 p) {
	vec3 i = floor(p);
	vec3 f = p - i;
	f = f * f * (3.0 - 2.0 * f);
	uvec3 c = uvec3(ivec3(i));
	float n000 = float(hash(c.x ^ hash(c.y ^ hash(c.z))) >> 8u);
	float n100 = float(hash((c.x + 1u) ^ hash(c.y ^ hash(c.z))) >> 8u);
	float n010 = float(hash(c.x ^ hash((c.y + 1u) ^ hash(c.z))) >> 8u);
	float n110 = float(hash((c.x + 1u) ^ hash((c.y + 1u) ^ hash(c.z))) >> 8u);
	float n001 = float(hash(c.x ^ hash(c.y ^ hash(c.z + 1u))) >> 8u);
	float n101 = float(hash((c.x + 1u) ^ hash(c.y ^ hash(c.z + 1u))) >> 8u);
	float n011 = float(hash(c.x ^ hash((c.y + 1u) ^ hash(c.z + 1u))) >> 8u);
	float n111 = float(hash((c.x + 1u) ^ hash((c.y + 1u) ^ hash(c.z + 1u))) >> 8u);
	float a = mix(mix(n000, n100, f.x), mix(n010, n110, f.x), f.y);
	float b = mix(mix(n001, n101, f.x), mix(n011, n111, f.x), f.y);
	return mix(a, b, f.z) * (1.0 / 16777216.0);
}

float fractal(vec2 p, float z) {
	float sum = 0.0;
	float amp = 0.5;
	for (int o = 0; o < OCTAVES; o++) {
		sum += noise3(vec3(p, z + float(o) * 17.0)) * amp;
		p = p * 2.03 + vec2(13.1, 7.7);
		amp *= 0.5;
	}
	return sum;
}

// Curl of the noise: a flow with no sources or sinks, so particles swirl
// instead of bunching up. Central differences: four fractal lookups.
vec2 flow(vec2 pos, float z) {
	vec2 p = pos * FLOW_SCALE;
	const float e = 0.01;
	float dx = fractal(p + vec2(e, 0.0), z) - fractal(p - vec2(e, 0.0), z);
	float dy = fractal(p + vec2(0.0, e), z) - fractal(p - vec2(0.0, e), z);
	return vec2(dy, -dx) / (2.0 * e) * FLOW_SPEED;
}

void step_particle(uint id, inout vec2 pos, inout vec2 vel, inout vec4 life) {
	float age = life.x + dt;
	float lifetime = life.y;

	// Born anywhere in the field. The first birth is pushed back by up to a
	// lifetime, so the field fills gradually rather than all at once.
	bool unborn = lifetime == 0.0;
	if (unborn || age >= lifetime) {
		uint s = hash(id) ^ hash(floatBitsToUint(time));
		lifetime = mix(LIFE_MIN, LIFE_MAX, rand(s));
		age = unborn ? -rand(s) * lifetime : 0.0;
		pos = mix(bounds.xy, bounds.zw, vec2(rand(s), rand(s)));
		vel = vec2(0.0);
	}

	if (age >= 0.0) {
		int n = max(substeps, 1);
		float h = dt / float(n);
		for (int i = 0; i < n; i++) {
			float z = (time - dt + h * float(i)) * FLOW_DRIFT;
			vec2 accel = (flow(pos, z) - vel) * RESPONSE;

			vec2 away = pos - emitter;
			float d = length(away);
			float near = 1.0 - clamp(d / PUSH_RADIUS, 0.0, 1.0);
			accel += (away / max(d, 1.0)) * PUSH * near * near;
			accel += emitter_velocity * INHERIT * RESPONSE * near;

			vel += accel * h;
			pos += vel * h;

			if (pos.x < bounds.x) { pos.x = bounds.x; vel.x = abs(vel.x); }
			if (pos.x > bounds.z) { pos.x = bounds.z; vel.x = -abs(vel.x); }
			if (pos.y < bounds.y) { pos.y = bounds.y; vel.y = abs(vel.y); }
			if (pos.y > bounds.w) { pos.y = bounds.w; vel.y = -abs(vel.y); }
		}
	}

	life = vec4(age, lifetime, atan(vel.y, vel.x), 0.0);
}
