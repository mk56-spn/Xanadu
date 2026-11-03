// The same particles as the DRAWABLE tab, as a GPUParticles2D particle shader.
// Godot's lifetime is set so long it never restarts anyone: age, lifetime and
// heading live in CUSTOM and respawning happens here, exactly as in the blit. The
// physics is in particle_motion.gdshaderinc, shared with that tab.
shader_type particles;
render_mode disable_velocity, disable_force;   // position is ours, not integrated

uniform vec2 emitter;
uniform vec2 emitter_velocity;
uniform vec4 bounds;
uniform float dt;
uniform float time;
uniform int substeps = 8;          // flow evaluations per frame: the cost knob

#include "res://Experiments/Particles/particle_motion.gdshaderinc"
#include "res://Experiments/Particles/particle_look.gdshaderinc"

void start() {
	// Everyone is emitted at once (explosiveness 1) as never born.
	CUSTOM = vec4(0.0);
	VELOCITY = vec3(0.0);
	TRANSFORM = mat4(vec4(0.0), vec4(0.0), vec4(0.0, 0.0, 1.0, 0.0), vec4(emitter, 0.0, 1.0));
}

void process() {
	vec2 pos = TRANSFORM[3].xy;
	vec2 vel = VELOCITY.xy;
	vec4 life = CUSTOM;
	step_particle(INDEX, pos, vel, life);
	CUSTOM = life;
	VELOCITY = vec3(vel, 0.0);

	float scale = particle_scale(life);
	float c = cos(life.z) * scale;
	float s = sin(life.z) * scale;
	TRANSFORM = mat4(vec4(c, s, 0.0, 0.0), vec4(-s, c, 0.0, 0.0), vec4(0.0, 0.0, 1.0, 0.0), vec4(pos, 0.0, 1.0));
	COLOR = particle_color(life);
}
