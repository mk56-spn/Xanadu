// One relaxation pass of every flow field: each open cell's distance becomes
// its cheapest neighbour's plus the step (no corner cutting past rock). Goal
// cells are 0. Run several times a frame; because a cell never keeps its own
// old value, distances can rise as well as fall, so the fields follow goals
// that move. Tiles 0-9: each squad's objective, from the control row.
// 10-14: home, seeded at every spawner the faction owns, so it leads to the
// nearest. Which spawner a cell seeds for was marked once by the terrain pass (z).
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D nav_in : hint_blit_source0;
uniform sampler2D unit_in : hint_blit_source1;   // row 0: each army's squad objectives
uniform sampler2D fx_in : hint_blit_source2;     // row 0: spawner owners
uniform vec4 bounds;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"

bool rock(ivec2 texel) {
	return texelFetch(nav_in, texel, 0).y > 0.5;
}

// A goal seeds its cell and the ring around it, so a goal inside rock still has open seeds.
bool seeds(ivec2 c, vec2 goal) {
	ivec2 g = nav_cell(goal, bounds);
	return max(abs(c.x - g.x), abs(c.y - g.y)) <= 1;
}

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	ivec2 n = nav_size(bounds);
	ivec2 t = texel / n;
	int tile = t.y * NAV_COLUMNS + t.x;
	ivec2 c = texel - t * n;

	vec4 self = texelFetch(nav_in, texel, 0);
	if (self.y > 0.5) {
		COLOR0 = vec4(NO_PATH, 1.0, self.z, 0.0);
	} else {
		bool goal = false;
		if (tile < HOME_TILE) {
			vec4 objectives = texelFetch(unit_in, ivec2(tile / 2, 0), 0);
			goal = seeds(c, tile % 2 == 0 ? objectives.xy : objectives.zw);
		} else if (tile < NAV_TILES) {
			int s = int(self.z + 0.5) - 1;
			goal = s >= 0 && spawner_owner(s, texelFetch(fx_in, ivec2(SPAWNER_TEXEL + s, 0), 0)) == tile - HOME_TILE;
		}
		float d = NO_PATH;
		if (goal) {
			d = 0.0;
		} else {
			for (int dy = -1; dy <= 1; dy++) {
				for (int dx = -1; dx <= 1; dx++) {
					ivec2 q = c + ivec2(dx, dy);
					if ((dx == 0 && dy == 0) || q.x < 0 || q.y < 0 || q.x >= n.x || q.y >= n.y)
						continue;
					ivec2 t = texel + ivec2(dx, dy);
					vec4 v = texelFetch(nav_in, t, 0);
					if (v.y > 0.5)
						continue;
					bool diagonal = dx != 0 && dy != 0;
					if (diagonal && (rock(texel + ivec2(dx, 0)) || rock(texel + ivec2(0, dy))))
						continue;
					d = min(d, v.x + (diagonal ? 1.4142 : 1.0));
				}
			}
		}
		COLOR0 = vec4(min(d, NO_PATH), 0.0, self.z, 0.0);
	}
}
