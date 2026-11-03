// Steps every particle one frame. Each texel is one particle: the blit reads
// last frame's state from the two sources and writes this frame's into the two
// targets, which the C# side then swaps (ping-pong). The physics itself is in
// particle_motion.gdshaderinc, shared with the GPUParticles2D tab.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D motion_in : hint_blit_source0;   // xy position (canvas px), zw velocity (px/s)
uniform sampler2D life_in : hint_blit_source1;     // x age, y lifetime (s), z heading

// Plain uniforms: a global uniform compiles to broken GLSL in texture_blit (4.8 dev7).
uniform vec2 emitter;
uniform vec2 emitter_velocity;   // px/s, so a swept mouse drags particles along
uniform vec4 bounds;             // field rect: xy min corner, zw max corner
uniform float dt;
uniform float time;
uniform int substeps = 8;          // flow evaluations per frame: the cost knob

#include "res://Experiments/Particles/particle_motion.gdshaderinc"

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	vec4 motion = texelFetch(motion_in, texel, 0);
	vec4 life = texelFetch(life_in, texel, 0);
	vec2 pos = motion.xy;
	vec2 vel = motion.zw;
	step_particle(uint(texel.x) | (uint(texel.y) << 12u), pos, vel, life);
	COLOR0 = vec4(pos, vel);
	COLOR1 = life;
}
