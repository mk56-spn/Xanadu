// The scoreboard and army markers, drawn straight from the control row: no
// readback, no CPU. Six vertices per part, five parts per faction; UV.x is the
// faction, VERTEX.x the corner, VERTEX.y the part:
//   0 track, 1 units alive, 2 kills (against the leader),
//   3 a diamond on the army's centre (in the world, so through the camera),
//   4 its share of the spawners.
shader_type canvas_item;

uniform sampler2D motion : filter_nearest;
uniform sampler2D control : filter_nearest;   // the fx state: row 0 holds the spawners
uniform vec2 origin;          // scoreboard top-left, canvas px
uniform float per_faction;    // units each faction fields
uniform vec3 view;            // the camera: xy world origin on screen, z zoom

#include "res://Experiments/Rts/rts_rules.gdshaderinc"

const float BAR = 240.0;
const float ROW = 32.0;
const float THICK = 11.0;
const float THIN = 4.0;
const float MARKER = 9.0;

const vec2 QUAD[6] = vec2[](
	vec2(0.0, 0.0), vec2(1.0, 0.0), vec2(1.0, 1.0),
	vec2(0.0, 0.0), vec2(1.0, 1.0), vec2(0.0, 1.0));
const vec2 DIAMOND[6] = vec2[](
	vec2(0.0, -1.0), vec2(1.0, 0.0), vec2(0.0, 1.0),
	vec2(0.0, -1.0), vec2(0.0, 1.0), vec2(-1.0, 0.0));

void vertex() {
	int f = int(UV.x);
	int corner = int(VERTEX.x);
	int part = int(VERTEX.y);
	vec4 army = texelFetch(motion, ivec2(f, 0), 0);
	vec3 tint = FACTION_COLORS[f];
	vec2 row = origin + vec2(0.0, float(f) * ROW);
	vec2 q = QUAD[corner];

	if (part == 0) {
		VERTEX = row + q * vec2(BAR, THICK);
		COLOR = vec4(tint * 0.18, 0.9);
	} else if (part == 1) {
		VERTEX = row + q * vec2(BAR * army.z / max(per_faction, 1.0), THICK);
		COLOR = vec4(tint, 1.0);
	} else if (part == 2) {
		float leader = 1.0;
		for (int g = 0; g < FACTIONS; g++)
			leader = max(leader, texelFetch(motion, ivec2(g, 0), 0).w);
		VERTEX = row + vec2(0.0, THICK + 2.0) + q * vec2(BAR * army.w / leader, THIN);
		COLOR = vec4(mix(tint, vec3(1.0), 0.5), 1.0);
	} else if (part == 4) {
		float owned = 0.0;
		for (int s = 0; s < SPAWNERS; s++)
			owned += spawner_owner(s, texelFetch(control, ivec2(SPAWNER_TEXEL + s, 0), 0)) == f ? 1.0 : 0.0;
		VERTEX = row + vec2(0.0, THICK + THIN + 4.0) + q * vec2(BAR * owned / float(SPAWNERS), THIN);
		COLOR = vec4(tint * 0.75, 1.0);
	} else {
		float shown = army.z > 0.0 ? 1.0 : 0.0;
		VERTEX = view.xy + army.xy * view.z + DIAMOND[corner] * MARKER * shown;
		COLOR = vec4(mix(tint, vec3(1.0), 0.35), 0.9);
	}
}
