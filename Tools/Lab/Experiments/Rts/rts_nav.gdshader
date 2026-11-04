// One relaxation pass of every flow field: each open cell's distance becomes
// its cheapest neighbour's plus the step (no corner cutting past rock). Tile g
// leads to goal g: a spawner's cell and the ring round it are 0, and a sector
// waypoint's cells within SECTOR_SEED. Neither goals nor rock move, so this
// only runs until the fields have settled at startup.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D nav_in : hint_blit_source0;
uniform vec4 bounds;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	ivec2 n = nav_size(bounds);
	ivec2 t = texel / n;
	int tile = t.y * NAV_COLUMNS + t.x;
	ivec2 c = texel - t * n;

	vec4 self = texelFetch(nav_in, texel, 0);
	if (self.y > 0.5) {
		COLOR0 = vec4(NO_PATH, 1.0, self.z, self.w);
	} else {
		float d = NO_PATH;
		ivec2 g = nav_cell(goal_pos(min(tile, GOALS - 1), bounds), bounds);
		int seed_reach = tile < SPAWNERS ? 1 : SECTOR_SEED;
		if (tile < GOALS && max(abs(c.x - g.x), abs(c.y - g.y)) <= seed_reach) {
			d = 0.0;
		} else {
			// Rock is fixed, so which neighbours count was worked out once (w).
			for (int dy = -1; dy <= 1; dy++) {
				for (int dx = -1; dx <= 1; dx++) {
					if (!open_to(self.w, dx, dy))
						continue;
					float v = texelFetch(nav_in, texel + ivec2(dx, dy), 0).x;
					d = min(d, v + (dx != 0 && dy != 0 ? 1.4142 : 1.0));
				}
			}
		}
		COLOR0 = vec4(min(d, NO_PATH), 0.0, self.z, self.w);
	}
}
