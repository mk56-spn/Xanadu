// What each faction has found, one texel per goal (x: a bit per faction): a
// faction learns a spawner once one of its living units comes within
// DISCOVER_RADIUS of it, and has scouted a sector once within SCOUTED_RADIUS
// of its waypoint. Knowledge is never lost. Runs each step after the grid.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D intel_in : hint_blit_source0;
uniform sampler2D motion_in : hint_blit_source1;
uniform sampler2D unit_in : hint_blit_source2;
uniform sampler2D grid_in : hint_blit_source3;   // sorted list, then cell table
uniform bool use_grid = true;
uniform int sorted_rows;
uniform vec4 bounds;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"
#include "res://Experiments/Rts/rts_grid.gdshaderinc"

const int EVERYONE = (1 << FACTIONS) - 1;

int table_entry(int cell, int part) {
	ivec2 t = ivec2(cell % WIDTH, sorted_rows + cell / WIDTH);
	vec4 entry = texelFetch(grid_in, t, 0);
	return int(part == 0 ? entry.x : entry.y);
}

// Adds unit j's faction if it's alive within r of p.
int sees(int j, vec2 p, float r, int mask) {
	ivec2 t = unit_texel(j);
	if (is_alive(texelFetch(unit_in, t, 0)) && distance(texelFetch(motion_in, t, 0).xy, p) <= r)
		mask |= 1 << faction_of(j);
	return mask;
}

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	int g = texel.x;
	int mask = int(texelFetch(intel_in, texel, 0).x + 0.5);
	if (g < GOALS && mask != EVERYONE) {
		vec2 p = goal_pos(g, bounds);
		float r = g < SPAWNERS ? DISCOVER_RADIUS : SCOUTED_RADIUS;
		if (use_grid) {
			ivec2 size = grid_size(bounds);
			ivec2 lo = cell_of(p - vec2(r), bounds);
			ivec2 hi = cell_of(p + vec2(r), bounds);
			for (int cy = lo.y; cy <= hi.y && mask != EVERYONE; cy++) {
				int first = table_entry(cy * size.x + lo.x, 0);
				int last = table_entry(cy * size.x + hi.x, 1);
				for (int i = first; i < last && mask != EVERYONE; i++)
					mask = sees(int(texelFetch(grid_in, slot_texel(i), 0).y), p, r, mask);
			}
		} else {
			ivec2 size = textureSize(motion_in, 0);
			int count = size.x * (size.y - 1);
			for (int j = 0; j < count && mask != EVERYONE; j++)
				mask = sees(j, p, r, mask);
		}
	}
	COLOR0 = vec4(float(mask), 0.0, 0.0, 0.0);
}
