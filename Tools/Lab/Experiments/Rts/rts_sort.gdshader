// Grid step 2: one pass of a bitonic sort by (cell, id). The C# side runs
// every (k, j) pass in order, ping-ponging two textures. k and j arrive in
// MODULATE.rg: each blit carries its own, where a material uniform would
// only take one value per frame.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D keys_in : hint_blit_source0;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"
#include "res://Experiments/Rts/rts_grid.gdshaderinc"

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	int i = texel.y * WIDTH + texel.x;
	int k = int(MODULATE.r + 0.5);
	int j = int(MODULATE.g + 0.5);
	int partner = i ^ j;
	vec4 a = texelFetch(keys_in, texel, 0);
	vec4 b = texelFetch(keys_in, slot_texel(partner), 0);
	bool a_first = a.x < b.x || (a.x == b.x && a.y < b.y);
	bool ascending = (i & k) == 0;
	bool keep_first = (i < partner) == ascending;
	COLOR0 = a_first == keep_first ? a : b;
}
