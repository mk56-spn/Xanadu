// Grid step 1: every slot writes (cell, unit id) for the sort. Slots past the
// last unit are padding up to a power of two; they and the dead get NO_CELL.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D motion_in : hint_blit_source0;
uniform sampler2D unit_in : hint_blit_source1;
uniform vec4 bounds;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"
#include "res://Experiments/Rts/rts_grid.gdshaderinc"

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	int i = texel.y * WIDTH + texel.x;
	ivec2 size = textureSize(motion_in, 0);
	int count = size.x * (size.y - 1);
	float key = NO_CELL;
	if (i < count) {
		ivec2 t = unit_texel(i);
		if (is_alive(texelFetch(unit_in, t, 0))) {
			ivec2 c = cell_of(texelFetch(motion_in, t, 0).xy, bounds);
			key = float(c.y * grid_size(bounds).x + c.x);
		}
	}
	COLOR0 = vec4(key, float(i), 0.0, 0.0);
}
