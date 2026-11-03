// Faction totals, spread over many threads: texel (x, f) sums faction f's
// units in state column x (one 64th of them), so no single thread walks every
// unit. The step's control row then adds up a faction's 64 texels.
//   xy  sum of living positions, z units alive, w kills this step
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D motion_in : hint_blit_source0;
uniform sampler2D unit_in : hint_blit_source1;
uniform sampler2D fx_in : hint_blit_source2;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	int f = texel.y;
	int rows = textureSize(motion_in, 0).y;
	vec4 total = vec4(0.0);
	for (int r = 1; r < rows; r++) {
		ivec2 t = ivec2(texel.x, r);
		if (faction_of(unit_id(t)) == f && is_alive(texelFetch(unit_in, t, 0))) {
			total.xy += texelFetch(motion_in, t, 0).xy;
			total.z += 1.0;
		}
		float flags = texelFetch(fx_in, t, 0).x;
		if (has_flag(flags, DIED_BIT) && killer_of(flags) == f)
			total.w += 1.0;
	}
	COLOR0 = total;
}
