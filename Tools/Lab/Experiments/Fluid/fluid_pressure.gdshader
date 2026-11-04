// Pass 3 of 4, run ITERATIONS times: one Jacobi sweep of the pressure Poisson
// equation. Each cell becomes the average of its neighbours minus its
// divergence; more sweeps carry pressure further, so the flow comes out
// closer to incompressible. Walls repeat the edge cell (zero gradient).
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D pressure_in : hint_blit_source0;   // x pressure, y divergence

#include "res://Experiments/Fluid/fluid_grid.gdshaderinc"

void blit() {
	ivec2 c = ivec2(FRAGCOORD.xy);
	float div = texelFetch(pressure_in, c, 0).y;
	float sum = at(pressure_in, c + ivec2(1, 0)).x + at(pressure_in, c - ivec2(1, 0)).x
		+ at(pressure_in, c + ivec2(0, 1)).x + at(pressure_in, c - ivec2(0, 1)).x;
	COLOR0 = vec4((sum - div) * 0.25, div, 0.0, 1.0);
}
