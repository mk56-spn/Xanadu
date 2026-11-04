// Pass 4 of 4: subtract the pressure gradient, leaving (nearly) divergence-free
// velocity for the next frame to advect.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D velocity_in : hint_blit_source0;
uniform sampler2D pressure_in : hint_blit_source1;

#include "res://Experiments/Fluid/fluid_grid.gdshaderinc"

void blit() {
	ivec2 c = ivec2(FRAGCOORD.xy);
	vec2 grad = 0.5 * vec2(at(pressure_in, c + ivec2(1, 0)).x - at(pressure_in, c - ivec2(1, 0)).x,
		at(pressure_in, c + ivec2(0, 1)).x - at(pressure_in, c - ivec2(0, 1)).x);
	COLOR0 = vec4(texelFetch(velocity_in, c, 0).xy - grad, 0.0, 1.0);
}
