// The sniper fire list, step 1: slot i is sniper i (snipers sit at fixed
// ranks, so i names one). A sniper firing this step, by the shared fire rule,
// writes (victim id, sniper id); the rest write NO_CELL and sort to the end.
// The sort then groups each victim's incoming shots, and the step finds its
// own by binary search, however far away the snipers are.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D motion_in : hint_blit_source0;
uniform sampler2D unit_in : hint_blit_source1;
uniform sampler2D nav_field : filter_nearest;
uniform vec4 bounds;
uniform float dt;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"
#include "res://Experiments/Rts/rts_grid.gdshaderinc"
#include "res://Experiments/Rts/rts_fire.gdshaderinc"

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	int i = texel.y * WIDTH + texel.x;
	ivec2 size = textureSize(motion_in, 0);
	int count = size.x * (size.y - 1);
	// Sniper i: faction i % FACTIONS, the (i / FACTIONS)-th sniper rank, 15 of every 20.
	int id = ((i / FACTIONS) * 20 + 15) * FACTIONS + i % FACTIONS;
	vec4 key = vec4(NO_CELL, float(id), 0.0, 0.0);
	if (id < count && type_of(id) == SNIPER) {
		ivec2 t = unit_texel(id);
		vec4 u = texelFetch(unit_in, t, 0);
		int target = int(u.z);
		if (is_alive(u) && target >= 0) {
			vec2 tp = texelFetch(motion_in, unit_texel(target), 0).xy;
			if (fires_at(u, id, texelFetch(motion_in, t, 0).xy, target, tp))
				key.x = float(target);
		}
	}
	COLOR0 = key;
}
