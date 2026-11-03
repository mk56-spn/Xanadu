// Grid step 3: copy the sorted list into the grid texture, and below it find
// each cell's run in the list by binary search.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D sorted_in : hint_blit_source0;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"
#include "res://Experiments/Rts/rts_grid.gdshaderinc"

int lower_bound(float key, int n) {
	int lo = 0;
	int hi = n;
	while (lo < hi) {
		int mid = (lo + hi) / 2;
		if (texelFetch(sorted_in, slot_texel(mid), 0).x < key)
			lo = mid + 1;
		else
			hi = mid;
	}
	return lo;
}

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	ivec2 size = textureSize(sorted_in, 0);
	if (texel.y < size.y) {
		COLOR0 = texelFetch(sorted_in, texel, 0);
	} else {
		int n = size.x * size.y;
		float cell = float((texel.y - size.y) * WIDTH + texel.x);
		COLOR0 = vec4(float(lower_bound(cell, n)), float(lower_bound(cell + 1.0, n)), 0.0, 0.0);
	}
}
