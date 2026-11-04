// Pass 2 of 4: how much each cell's velocity spreads out (or in). Writes it
// beside last frame's pressure, damped, so the solve starts warm.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D velocity_in : hint_blit_source0;
uniform sampler2D pressure_in : hint_blit_source1;   // x pressure, y divergence
uniform float warm_start = 0.8;

#include "res://Experiments/Fluid/fluid_grid.gdshaderinc"

// Free-slip walls: a neighbour past the wall mirrors this cell's flow into it.
vec2 side(ivec2 c, ivec2 o, vec2 centre) {
	ivec2 n = c + o;
	if (outside(n))
		return o.x != 0 ? vec2(-centre.x, centre.y) : vec2(centre.x, -centre.y);
	return texelFetch(velocity_in, n, 0).xy;
}

void blit() {
	ivec2 c = ivec2(FRAGCOORD.xy);
	vec2 v = texelFetch(velocity_in, c, 0).xy;
	float div = 0.5 * (side(c, ivec2(1, 0), v).x - side(c, ivec2(-1, 0), v).x
		+ side(c, ivec2(0, 1), v).y - side(c, ivec2(0, -1), v).y);
	COLOR0 = vec4(texelFetch(pressure_in, c, 0).x * warm_start, div, 0.0, 1.0);
}
