// Draws the particles: one triangle array, three vertices per particle, built
// once. UV holds the particle's texel in the state textures; VERTEX holds a
// corner of a unit triangle. The vertex shader looks the particle up and puts
// the corner where it belongs, so the CPU never touches a particle.
shader_type canvas_item;
render_mode blend_add;

uniform sampler2D motion : filter_nearest;   // the step shader's latest output
uniform sampler2D life : filter_nearest;

#include "res://Experiments/Particles/particle_look.gdshaderinc"

void vertex() {
	ivec2 texel = ivec2(UV);
	vec4 m = texelFetch(motion, texel, 0);
	vec4 l = texelFetch(life, texel, 0);
	float c = cos(l.z);
	float s = sin(l.z);
	VERTEX = m.xy + mat2(vec2(c, s), vec2(-s, c)) * VERTEX * particle_scale(l);
	COLOR = particle_color(l);
}
