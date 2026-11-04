// Sort blit 2: turns chunk -> rank into rank -> chunk. Slot s finds the one
// chunk ranked s and copies where its triangles start in the draw and where
// they start in the geometry.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D ranks_in : hint_blit_source0;
uniform sampler2D chunks_in : hint_blit_source1;
uniform int chunks;

#include "res://Experiments/Snow/snow_slots.gdshaderinc"

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	int slot = texel.y * WIDTH + texel.x;
	vec4 found = vec4(0.0);
	if (slot < chunks) {
		for (int o = 0; o < chunks; o++) {
			vec4 r = texelFetch(ranks_in, slot_texel(o), 0);
			if (int(r.x + 0.5) == slot) {
				found = vec4(float(o), r.y, chunk_texel(chunks_in, o, 1).x, 0.0);
				break;
			}
		}
	}
	COLOR0 = found;
}
