// The ground under the battle: open field, rock from the nav texture's
// obstacle channel (filtered, so the blocky cells read as soft outcrops), and
// every spawner: its capture ring in its owner's colour (grey if neutral), a
// core, and an arc in the colour of whoever is taking it. One rect over the field.
shader_type canvas_item;

uniform sampler2D nav_smooth : filter_linear;
uniform sampler2D control : filter_nearest;   // the fx state: row 0 holds the spawners
uniform vec4 bounds;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"

const vec3 GROUND = vec3(0.065, 0.085, 0.125);
const vec3 ROCK = vec3(0.20, 0.22, 0.27);
const vec3 RIM = vec3(0.34, 0.37, 0.44);
const vec3 NEUTRAL_COLOR = vec3(0.55, 0.58, 0.64);
const float CORE = 70.0;   // px
// Zoomed far out, spawners keep at least these sizes, in screen px.
const float MIN_CORE_PX = 6.0;
const float MIN_LINE_PX = 1.5;

varying vec2 world;

void vertex() {
	world = VERTEX;
}

void fragment() {
	// Tile 0's rock; the tiles are side by side and all carry the same rock.
	vec2 cells = (world - bounds.xy) / NAV_CELL;
	float rock = texture(nav_smooth, cells / vec2(textureSize(nav_smooth, 0))).y;
	float body = smoothstep(0.45, 0.55, rock);
	float rim = smoothstep(0.3, 0.45, rock) - body;
	vec3 color = mix(GROUND, ROCK, body) + RIM * rim * 0.6;

	// World px per screen px, so the spawners stay readable at any zoom.
	float px = length(fwidth(world)) * 0.7071;
	float core_size = max(CORE, MIN_CORE_PX * px);
	float line = max(14.0, MIN_LINE_PX * px);
	float arc_gap = max(34.0, (MIN_CORE_PX * 0.5 + 2.0) * px);
	for (int s = 0; s < SPAWNERS; s++) {
		vec2 offset = world - spawner_pos(s, bounds);
		float d = length(offset);
		if (d > max(CAPTURE_RADIUS, core_size) + arc_gap + line * 2.0)
			continue;
		vec4 state = texelFetch(control, ivec2(SPAWNER_TEXEL + s, 0), 0);
		int owner = spawner_owner(s, state);
		vec3 tint = owner < FACTIONS ? FACTION_COLORS[owner] : NEUTRAL_COLOR;
		float zone = 1.0 - smoothstep(CAPTURE_RADIUS - 20.0, CAPTURE_RADIUS, d);
		float ring = 1.0 - smoothstep(0.0, line, abs(d - CAPTURE_RADIUS));
		color = mix(color, tint, zone * 0.08 + ring * 0.45);

		// The core: a hexagon, bright for owned, hollow for neutral.
		vec2 q = abs(offset);
		float hex = max(q.x * 0.866 + q.y * 0.5, q.y) - core_size;
		float core = 1.0 - smoothstep(-px, 0.0, hex);
		float edge = 1.0 - smoothstep(0.0, max(6.0, px), abs(hex + max(6.0, px)));
		color = mix(color, tint, owner < FACTIONS ? core * 0.85 : edge * 0.8);

		// Capture progress, clockwise from the top, just outside the ring.
		if (state.z > 0.0 && state.y >= 0.0) {
			float angle = fract(atan(offset.x, -offset.y) / 6.2831853 + 1.0);
			float radius = max(CAPTURE_RADIUS, core_size) + arc_gap;
			float band = 1.0 - smoothstep(0.0, px, abs(d - radius) - line);
			color = mix(color, FACTION_COLORS[int(state.y)], band * step(angle, state.z));
		}
	}
	COLOR = vec4(color, 1.0);
}
