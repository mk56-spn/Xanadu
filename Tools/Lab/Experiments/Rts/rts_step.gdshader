// One frame of battle for every unit, plus the control row, in one blit.
//
// Each unit scans its neighbours (a gather: a texel can read anything but
// write only itself) and works out, from last frame's state alone:
//   targeting  the best enemy it can see, scored by its type's preferences;
//              a nearby commander's target overrides it
//   support    commanders (buff), guardians (aura) and medics near it
//   damage     every shot landing on it, and artillery splash near it;
//              shooter and victim use one fire rule, so they always agree
//   movement   retreat, kite, close in or follow its army's flow field
//              around the rocks, sliding along walls
// Row 0 gathers each faction's totals and picks its squads' objectives, and
// steps every spawner's capture from the units standing in its ring.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D motion_in : hint_blit_source0;
uniform sampler2D unit_in : hint_blit_source1;
uniform sampler2D fx_in : hint_blit_source2;
uniform sampler2D grid_in : hint_blit_source3;   // sorted list, then cell table
// Ordinary texture uniforms: a blit takes at most four sources.
uniform sampler2D nav_field : filter_nearest;
uniform sampler2D faction_tally : filter_nearest;   // per state column and faction: see rts_tally
uniform sampler2D fire_list : filter_nearest;       // snipers firing this step, sorted by victim: see rts_fire_keys
uniform int fire_slots;                             // entries in fire_list
uniform sampler2D intel : filter_nearest;           // what each faction has found, per goal: see rts_intel

uniform bool use_grid = true;
uniform int sorted_rows;   // rows of grid_in holding the sorted list; the table starts below

uniform vec4 bounds;       // field rect: xy min corner, zw max corner
uniform float dt;
uniform float time;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"
#include "res://Experiments/Rts/rts_grid.gdshaderinc"
#include "res://Experiments/Rts/rts_fire.gdshaderinc"

const float SPAWN_SPREAD = 45.0;
const float RESPONSE = 6.0;      // per second: how quickly velocity follows intent

// Crowding: a grid cell holding more than CROWD_CAPACITY units costs a
// marching unit extra, in nav cells of detour, CROWD_COST for every capacity
// over and at most CROWD_MAX, so packed cells are walked round, not into.
// Every faction counts. Soft, so a lone gap in a wall still gets used.
const float CROWD_CAPACITY = 60.0;
const float CROWD_COST = 2.0;
const float CROWD_MAX = 6.0;

// A squad doesn't pick a spawner its army is already taking with this many.
const float COVERED = 20.0;

uint hash(uint x) {
	x ^= x >> 16u; x *= 0x7feb352du;
	x ^= x >> 15u; x *= 0x846ca68bu;
	x ^= x >> 16u;
	return x;
}

float rand(inout uint s) {
	s = hash(s);
	return float(s >> 8u) * (1.0 / 16777216.0);
}

// One hit, before the victim's guardian aura.
float hit(int shooter_type, vec4 shooter_unit, float shooter_flags, int victim_type, float victim_share) {
	float dmg = DAMAGE[shooter_type] * bonus(shooter_type, victim_type);
	if (has_flag(shooter_flags, BUFFED_BIT))
		dmg *= BUFF_SHARE;
	if (shooter_type == BERSERKER)
		dmg *= 1.0 + (1.0 - clamp(shooter_unit.x / HP[BERSERKER], 0.0, 1.0)) * RAGE;
	if (shooter_type == ASSASSIN && victim_share < 0.5)
		dmg *= EXECUTE;
	return max(dmg - ARMOR[victim_type], dmg * 0.2);
}

// Lower is better.
float target_score(int type, int other, float dist, float other_share) {
	if (type == ASSASSIN)
		return other_share * 400.0 + dist * 0.4;
	if (type == SNIPER && (other == MEDIC || other == ARTILLERY || other == COMMANDER || other == SNIPER || other == HOWITZER))
		return dist * 0.3;
	if (type == ARTILLERY && (other == HEAVY || other == GUARDIAN || other == ARTILLERY))
		return dist * 0.5;
	if ((type == SCOUT || type == ASSASSIN) && other == HOWITZER)
		return dist * 0.3;
	if (type == SCOUT && (other == ARTILLERY || other == SNIPER || other == MEDIC))
		return dist * 0.5;
	return dist;
}

int table_entry(int cell, int part) {
	ivec2 t = ivec2(cell % WIDTH, sorted_rows + cell / WIDTH);
	vec4 entry = texelFetch(grid_in, t, 0);
	return int(part == 0 ? entry.x : entry.y);
}

// Units in the grid cell holding p; 0 when the grid isn't built (scan all).
float crowd_at(vec2 p) {
	if (!use_grid)
		return 0.0;
	ivec2 c = cell_of(p, bounds);
	int cell = c.y * grid_size(bounds).x + c.x;
	return float(table_entry(cell, 1) - table_entry(cell, 0));
}

// The detour, in nav cells, that walking into p's grid cell is worth.
float crowd_cost(vec2 p) {
	return min(max(crowd_at(p) - CROWD_CAPACITY, 0.0) / CROWD_CAPACITY * CROWD_COST, CROWD_MAX);
}

// Toward the neighbouring nav cell nearest this tile's goal; straight at the
// fallback when the field hasn't reached here or we're already at the goal.
// With avoid, each cell also costs its crowding, so a unit in a packed cell
// steps out of it and the stream bends round packed cells ahead.
vec2 downhill(int tile, vec2 pos, vec2 fallback, bool avoid) {
	ivec2 c = nav_cell(pos, bounds);
	ivec2 origin = tile_origin(tile, bounds);
	vec4 self = texelFetch(nav_field, origin + c, 0);
	float here = self.x;
	if (here >= NO_PATH * 0.5)
		return safe_normalize(fallback - pos);
	float best = here + (avoid ? crowd_cost(pos) : 0.0);
	vec2 toward = fallback;
	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			if (!open_to(self.w, dx, dy))
				continue;
			ivec2 q = c + ivec2(dx, dy);
			vec2 centre = nav_centre(q, bounds);
			float v = texelFetch(nav_field, origin + q, 0).x + (avoid ? crowd_cost(centre) : 0.0);
			if (v < best) {
				best = v;
				toward = centre;
			}
		}
	}
	return safe_normalize(toward - pos);
}

// Toward a squad's objective along its spawner's field; straight for the
// centre when the squad has nothing left to take.
vec2 march(int goal, vec2 pos, vec2 objective) {
	return goal >= 0 ? downhill(goal, pos, objective, true) : safe_normalize(objective - pos);
}

// Pushes away from rock cells close by, so units round corners instead of grinding.
vec2 rock_push(vec2 pos) {
	ivec2 n = nav_size(bounds);
	ivec2 c = nav_cell(pos, bounds);
	vec2 push = vec2(0.0);
	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			ivec2 q = c + ivec2(dx, dy);
			if (q.x < 0 || q.y < 0 || q.x >= n.x || q.y >= n.y || texelFetch(nav_field, q, 0).y < 0.5)
				continue;
			vec2 away = pos - nav_centre(q, bounds);
			float d = length(away);
			float reach = NAV_CELL * 1.1;
			if (d < reach)
				push += away / max(d, 1.0) * (1.0 - d / reach);
		}
	}
	return push;
}

// What one unit learns from its neighbours.
struct Scan {
	float best_score;
	int pick;
	vec2 pick_pos;
	float enemy_dist;
	vec2 enemy_pos;
	int focus;            // a nearby commander's target
	bool buffed;
	bool aura;
	float medic_dist;
	vec2 medic_pos;
	vec2 push;
	float damage;
	float heal;
	int hit_by;
};

void scan(int j, int me, int f, int type, vec2 pos, float share, inout Scan s) {
	if (j == me)
		return;
	ivec2 tj = unit_texel(j);
	vec4 uj = texelFetch(unit_in, tj, 0);
	if (!is_alive(uj))
		return;
	vec2 pj = texelFetch(motion_in, tj, 0).xy;
	vec2 away = pos - pj;
	float dist = length(away);
	int fj = faction_of(j);
	int other = type_of(j);

	float personal = (SIZE[type] + SIZE[other]) * 1.2;
	if (dist < personal && dist > 1e-3)
		s.push += away / dist * (1.0 - dist / personal);

	if (fj == f) {
		if (other == COMMANDER && dist < BUFF) {
			s.buffed = true;
			s.focus = int(uj.z);
		}
		if (other == GUARDIAN && dist < AURA)
			s.aura = true;
		if (other == MEDIC && dist < s.medic_dist) {
			s.medic_dist = dist;
			s.medic_pos = pj;
		}
		// A medic's target is the most wounded friend it can see.
		if (type == MEDIC && dist < SIGHT) {
			float wounded = uj.x / HP[other];
			float score = wounded * 400.0 + dist * 0.5;
			if (wounded < 0.95 && score < s.best_score && clear_line(pos, pj)) {
				s.best_score = score;
				s.pick = j;
				s.pick_pos = pj;
			}
		}
		if (other == MEDIC && fires_at(uj, j, pj, me, pos))
			s.heal += DAMAGE[MEDIC];
		return;
	}

	if (dist < s.enemy_dist) {
		s.enemy_dist = dist;
		s.enemy_pos = pj;
	}
	if (type != MEDIC && dist < (type == SNIPER ? RANGE[SNIPER] : SIGHT)) {
		float score = target_score(type, other, dist, uj.x / HP[other]);
		if (score < s.best_score && (type == ARTILLERY ? dist >= ARTILLERY_MIN : clear_line(pos, pj))) {
			s.best_score = score;
			s.pick = j;
			s.pick_pos = pj;
		}
	}
	// Medics heal; howitzers' shells and snipers' shots reach past the scan,
	// so shellfire and sniper_fire gather those instead.
	if (other == MEDIC || other == HOWITZER || other == SNIPER)
		return;
	if (fires_at(uj, j, pj, me, pos)) {
		s.damage += hit(other, uj, texelFetch(fx_in, tj, 0).x, type, share);
		s.hit_by = fj;
	} else if (other == ARTILLERY && int(uj.z) >= 0 && uj.y - dt <= 0.0) {
		// Splash: its shell lands on its own target, and I'm standing close to it.
		ivec2 tt = unit_texel(int(uj.z));
		vec2 tp = texelFetch(motion_in, tt, 0).xy;
		if (distance(tp, pos) < SPLASH && is_alive(texelFetch(unit_in, tt, 0)) && can_hit(ARTILLERY, pj, tp)) {
			s.damage += hit(other, uj, texelFetch(fx_in, tj, 0).x, type, share) * SPLASH_SHARE;
			s.hit_by = fj;
		}
	}
}

// Shells from enemy howitzers landing this step: one whose flight time (fx.w)
// runs out now lands on its gun's target. A gun is far beyond the scan, so
// every unit checks every enemy howitzer; there are only a few.
void shellfire(int f, int type, vec2 pos, int count, inout Scan s) {
	for (int g = 0; g < FACTIONS; g++) {
		if (g == f)
			continue;
		for (int k = 0; k < HOWITZERS; k++) {
			int h = howitzer_id(g, k);
			if (h >= count)
				break;
			ivec2 th = unit_texel(h);
			float flight = texelFetch(fx_in, th, 0).w;
			if (flight <= 0.0 || flight > dt)
				continue;
			int t = int(texelFetch(unit_in, th, 0).z);
			if (t < 0)
				continue;
			float d = distance(texelFetch(motion_in, unit_texel(t), 0).xy, pos);
			if (d >= SHELL_SPLASH)
				continue;
			float dmg = DAMAGE[HOWITZER] * (1.0 - 0.6 * d / SHELL_SPLASH);
			s.damage += max(dmg - ARMOR[type], dmg * 0.2);
			s.hit_by = g;
		}
	}
}

// Shots from snipers this step: the fire list holds (victim, sniper) for every
// sniper firing, sorted by victim, so mine are one run found by binary search.
void sniper_fire(int me, int type, float share, inout Scan s) {
	float key = float(me);
	int lo = 0;
	int hi = fire_slots;
	while (lo < hi) {
		int mid = (lo + hi) / 2;
		if (texelFetch(fire_list, slot_texel(mid), 0).x < key)
			lo = mid + 1;
		else
			hi = mid;
	}
	for (int i = lo; i < fire_slots; i++) {
		vec4 entry = texelFetch(fire_list, slot_texel(i), 0);
		if (entry.x != key)
			break;
		int sniper = int(entry.y);
		ivec2 ts = unit_texel(sniper);
		s.damage += hit(SNIPER, texelFetch(unit_in, ts, 0), texelFetch(fx_in, ts, 0).x, type, share);
		s.hit_by = faction_of(sniper);
	}
}

// What a howitzer shells: the enemy, among those its squad is fighting, standing
// in the most crowded grid cell, if that's crowded enough and in range. Samples
// a couple of dozen squadmates' targets a step; its last target stays in the
// running, so it settles on a mass instead of flicking between them.
int gun_target(int me, int f, vec2 pos, int last, int count, inout uint seed) {
	int per_faction = count / FACTIONS;
	int squad = squad_of(me);
	int runs = max(per_faction / 20 / SQUADS, 1);
	int best = -1;
	float best_mass = MASS_MIN - 1.0;
	for (int i = 0; i <= 24; i++) {
		int t = last;
		if (i > 0) {
			int rank = (squad + SQUADS * int(rand(seed) * float(runs))) * 20 + int(rand(seed) * 20.0);
			if (rank >= per_faction)
				continue;
			vec4 mate = texelFetch(unit_in, unit_texel(rank * FACTIONS + f), 0);
			if (!is_alive(mate))
				continue;
			t = int(mate.z);
		}
		if (t < 0 || faction_of(t) == f || !is_alive(texelFetch(unit_in, unit_texel(t), 0)))
			continue;
		vec2 tp = texelFetch(motion_in, unit_texel(t), 0).xy;
		float d = distance(pos, tp);
		if (d > RANGE[HOWITZER] || d < HOWITZER_MIN)
			continue;
		float mass = crowd_at(tp);
		if (mass > best_mass) {
			best_mass = mass;
			best = t;
		}
	}
	return best;
}

vec4 spawner_state(int s) {
	return texelFetch(fx_in, ivec2(SPAWNER_TEXEL + s, 0), 0);
}

int owner_of(int s) {
	return spawner_owner(s, spawner_state(s));
}

// The faction's owned spawner nearest pos; -1 if it owns none.
int nearest_owned(int f, vec2 pos) {
	int best = -1;
	float best_dist = 1e20;
	for (int s = 0; s < SPAWNERS; s++) {
		float d = distance(pos, spawner_pos(s, bounds));
		if (owner_of(s) == f && d < best_dist) {
			best_dist = d;
			best = s;
		}
	}
	return best;
}

int owned_count(int f) {
	int owned = 0;
	for (int s = 0; s < SPAWNERS; s++)
		owned += owner_of(s) == f ? 1 : 0;
	return owned;
}

// The goal squad (f, q) is heading for (a spawner or a sector waypoint, see
// goal_pos), or -1 when it has none.
int squad_objective(int f, int q) {
	vec4 v = texelFetch(unit_in, ivec2(squad_objective_texel(f, q), 0), 0);
	return int(v[q % 4] + 0.5) - 1;
}

// Squad (f, q)'s leader: where its formation is laid out from.
vec2 squad_leader(int f, int q) {
	ivec2 t = ivec2(squad_objective_texel(f, q), 0);
	int k = q % 4;
	vec4 v = k < 2 ? texelFetch(motion_in, t, 0) : texelFetch(fx_in, t, 0);
	return k % 2 == 0 ? v.xy : v.zw;
}

// Which way a leader at l faces: along its field, or straight at the objective
// when the field says nothing (arrived, or no field yet). Never zero.
vec2 leader_facing(int goal, vec2 l, vec2 objective) {
	vec2 face = goal >= 0 ? downhill(goal, l, objective, false) : safe_normalize(objective - l);
	if (length(face) < 0.5)
		face = safe_normalize(objective - l);
	return length(face) < 0.5 ? vec2(0.0, -1.0) : face;
}

// Rows a band fills in a squad of `runs` twenty-unit runs.
int band_rows(int band, int runs) {
	return (BAND_RUN[band] * runs + ROW_MAX - 1) / ROW_MAX;
}

// How far back its band starts from the leader, in px.
float band_depth(int band, int runs) {
	float depth = 0.0;
	for (int b = 0; b < band; b++)
		depth += float(band_rows(b, runs)) * SLOT + BAND_GAP;
	return depth;
}

// A unit's place in its formation: x px behind the leader, y px to its right.
vec2 slot_offset(int id, int type, int per_faction) {
	int rank = id / FACTIONS;
	int run = rank / 20 / SQUADS;
	int place = rank % 20;
	int runs = max((per_faction / 20 + SQUADS - 1) / SQUADS, 1);
	int band = band_of(type);
	// This unit's index among its band's places: whole runs before it, then
	// the places of its run before it.
	int before = 0;
	for (int k = 0; k < place; k++) {
		int b = band_of(mix_type(k));
		before += (b == band) ? 1 : 0;
	}
	if (band == BAND_FLANK) {
		int i = run * 3 + before;
		float side = i % 2 == 0 ? -1.0 : 1.0;
		int j = i / 2;
		return vec2(band_depth(1, runs) + float(j / 3) * SLOT,
				side * (float(ROW_MAX) * 0.5 + 1.0 + float(j % 3)) * SLOT);
	}
	int i = run * BAND_RUN[band] + before;
	int width = min(BAND_RUN[band] * runs, ROW_MAX);
	return vec2(band_depth(band, runs) + float(i / ROW_MAX) * SLOT,
			(float(i % ROW_MAX) - float(width - 1) * 0.5) * SLOT);
}

// The velocity that keeps a marching unit in its formation: the leader's walk
// plus a pull onto its slot. False when it shouldn't be in formation: a free
// type, a leader not placed yet, too far off to join, or a slot in rock.
bool formation_velocity(int me, int f, int type, vec2 pos, int goal, vec2 objective, int count,
		out vec2 want_velocity) {
	want_velocity = vec2(0.0);
	if (band_of(type) == BAND_FREE)
		return false;
	int q = squad_of(me);
	vec2 l = squad_leader(f, q);
	if (length(l) < 1.0)
		return false;
	vec2 face = leader_facing(goal, l, objective);
	vec2 off = slot_offset(me, type, count / FACTIONS);
	vec2 slot = l - face * off.x + vec2(-face.y, face.x) * off.y;
	vec2 to = slot - pos;
	if (length(to) > FORMATION_JOIN || rock_at(slot))
		return false;
	bool walking = distance(l, objective) >= ARRIVED;
	want_velocity = (walking ? face * FORMATION_SPEED : vec2(0.0)) + to * 1.5;
	return true;
}

// The leaders held in control texel x: each walks its squad's flow field
// toward this step's objective, and stops on arriving. A new leader starts
// at its faction's base. Squads 0-1 go out in a, 2-3 in b.
void step_leaders(int x, vec4 picks, vec4 a_in, vec4 b_in, vec2 centre, out vec4 a, out vec4 b) {
	int first = (x - OBJECTIVE_TEXEL) * 4;
	int f = first / SQUADS;
	vec2 l[4] = vec2[](a_in.xy, a_in.zw, b_in.xy, b_in.zw);
	for (int k = 0; k < 4; k++) {
		if (length(l[k]) < 1.0)
			l[k] = spawner_pos(f, bounds);
		int goal = int(picks[k] + 0.5) - 1;
		vec2 objective = goal >= 0 ? goal_pos(goal, bounds) : centre;
		if (distance(l[k], objective) >= ARRIVED)
			l[k] += leader_facing(goal, l[k], objective) * FORMATION_SPEED * dt;
	}
	a = vec4(l[0], l[1]);
	b = vec4(l[2], l[3]);
}

// Where a respawning unit comes back: only at an owned spawner whose site
// raises its type. Half the time the one nearest its squad's objective
// (reinforcing the front), otherwise any of them. -1 if there is none.
int respawn_spawner(int f, int type, vec2 objective, inout uint seed) {
	int able = 0;
	int nearest = -1;
	float nearest_dist = 1e20;
	for (int s = 0; s < SPAWNERS; s++) {
		if (owner_of(s) != f || !site_raises(site_of(s), type))
			continue;
		able++;
		float d = distance(objective, spawner_pos(s, bounds));
		if (d < nearest_dist) {
			nearest_dist = d;
			nearest = s;
		}
	}
	if (able == 0 || rand(seed) < 0.5)
		return nearest;
	int pick = int(rand(seed) * float(able)) % able;
	for (int s = 0; s < SPAWNERS; s++) {
		if (owner_of(s) == f && site_raises(site_of(s), type)) {
			if (pick == 0)
				return s;
			pick--;
		}
	}
	return nearest;
}

// Whether faction f's intel has goal g: found it, or scouted its sector.
bool in_intel(int f, int g) {
	return ((int(texelFetch(intel, ivec2(g, 0), 0).x + 0.5) >> f) & 1) == 1;
}

// Picks the four objectives held in control texel x: squads q0..q0+3 of one
// faction. An army only goes for what it knows of: its own spawners, ones it
// has found, and sectors it hasn't scouted. A squad keeps its goal until the
// army owns that spawner or has scouted that sector. The last two squads go
// instead to save the nearest of its own spawners being taken. Squads needing
// a goal pick in squad order, each the nearest to the army that no other
// squad has: the first RECON_SQUADS want an unscouted sector, the rest a
// known spawner the army doesn't own and isn't already taking with COVERED or
// more; each falls back to the other kind, then to doubling up on a known
// spawner, then on an unscouted sector. Every texel replays the whole army's
// picks so they agree. Stored as goal + 1, 0 when there is nothing to do.
vec4 pick_objectives(int x) {
	int first = (x - OBJECTIVE_TEXEL) * 4;
	int f = first / SQUADS;
	int q0 = first % SQUADS;
	vec4 army = texelFetch(motion_in, ivec2(f, 0), 0);
	vec2 from = army.z > 0.0 ? army.xy : spawner_pos(f, bounds);

	float dist[GOALS];
	bool ours[GOALS];
	bool known[GOALS];    // a spawner we know of, or a sector we've scouted
	bool open[GOALS];     // worth picking: a known spawner to take, or a sector to scout
	int rescue = -1;
	float rescue_dist = 1e20;
	for (int g = 0; g < GOALS; g++) {
		dist[g] = distance(from, goal_pos(g, bounds));
		if (g < SPAWNERS) {
			vec4 state = spawner_state(g);
			ours[g] = spawner_owner(g, state) == f;
			known[g] = ours[g] || g == f || in_intel(f, g);
			bool covered = int(state.y) == f && state.w >= COVERED;
			open[g] = known[g] && !ours[g] && !covered;
			if (ours[g] && state.z > 0.0 && dist[g] < rescue_dist) {
				rescue_dist = dist[g];
				rescue = g;
			}
		} else {
			ours[g] = false;
			known[g] = in_intel(f, g);
			open[g] = !known[g];
		}
	}

	// Kept goals are taken; the rest of the squads pick, in order.
	bool taken[GOALS];
	for (int g = 0; g < GOALS; g++)
		taken[g] = false;
	int goal_of[SQUADS];
	bool picking[SQUADS];
	for (int q = 0; q < SQUADS; q++) {
		int kept = squad_objective(f, q);
		bool rescuing = rescue >= 0 && q >= SQUADS - 2;
		bool valid = kept >= 0 && (kept < SPAWNERS ? !ours[kept] : !known[kept]);
		picking[q] = !rescuing && !valid;
		goal_of[q] = rescuing ? rescue : (valid ? kept : -1);
		if (!rescuing && valid)
			taken[kept] = true;
	}
	for (int q = 0; q < SQUADS; q++) {
		if (!picking[q])
			continue;
		bool recon = q < RECON_SQUADS;
		int pick = -1;
		for (int pass = 0; pass < 4 && pick < 0; pass++) {
			float best = 1e20;
			for (int g = 0; g < GOALS; g++) {
				bool sector = g >= SPAWNERS;
				bool ok;
				if (pass == 0)
					ok = open[g] && !taken[g] && sector == recon;
				else if (pass == 1)
					ok = open[g] && !taken[g];
				else if (pass == 2)
					ok = !sector && known[g] && !ours[g];
				else
					ok = sector && !known[g];
				if (ok && dist[g] < best) {
					best = dist[g];
					pick = g;
				}
			}
		}
		goal_of[q] = pick;
		if (pick >= 0)
			taken[pick] = true;
	}

	vec4 picks = vec4(0.0);
	for (int k = 0; k < 4; k++)
		picks[k] = float(goal_of[q0 + k] + 1);
	return picks;
}

// A unit's faction if it's alive within the capture ring around p; -1 otherwise.
int capturer_at(int j, vec2 p) {
	ivec2 t = unit_texel(j);
	if (!is_alive(texelFetch(unit_in, t, 0)) || distance(texelFetch(motion_in, t, 0).xy, p) > CAPTURE_RADIUS)
		return -1;
	return faction_of(j);
}

void tally(int f, inout vec4 first, inout float fifth) {
	if (f == 0) first.x += 1.0;
	else if (f == 1) first.y += 1.0;
	else if (f == 2) first.z += 1.0;
	else if (f == 3) first.w += 1.0;
	else if (f == 4) fifth += 1.0;
}

// Everything a unit texel does, writing its three outputs.
void step_unit(ivec2 texel, int count, vec4 m, vec4 u, vec4 fx, vec2 centre,
		out vec4 out_motion, out vec4 out_unit, out vec4 out_fx) {
	int me = unit_id(texel);
	int f = faction_of(me);
	int type = type_of(me);
	vec2 pos = m.xy;
	vec2 vel = m.zw;
	float hp = u.x;
	float cool = u.y - dt;
	int target = int(u.z);
	float respawn = u.w;
	int killer = killer_of(fx.x);
	bool retreat = has_flag(fx.x, RETREAT_BIT);
	bool buffed = false;
	bool died = false;
	float tracer = max(fx.y - dt, 0.0);
	float heading = fx.z;
	float shell = max(fx.w - dt, 0.0);   // a howitzer's shell still in the air

	if (!is_alive(u)) {
		// Dead, or never spawned (a cleared texture is hp 0, wait 0).
		respawn -= dt;
		target = -1;
		tracer = 0.0;
		uint s = hash(uint(me)) ^ hash(floatBitsToUint(time));
		int goal = squad_objective(f, squad_of(me));
		vec2 objective = goal >= 0 ? goal_pos(goal, bounds) : centre;
		// Over the faction's cap: stay down until it holds more spawners.
		bool capped = me / FACTIONS >= unit_cap(count / FACTIONS, owned_count(f));
		int spawner = respawn <= 0.0 && !capped ? respawn_spawner(f, type, objective, s) : -1;
		// No site of mine raises my type, or no room: wait, and try again in a second.
		if (respawn <= 0.0 && spawner < 0)
			respawn = 1.0;
		if (respawn <= 0.0) {
			float a = rand(s) * 6.2831853;
			float spread = SPAWN_SPREAD * sqrt(max(float(count) / float(FACTIONS) / 256.0, 1.0));
			pos = spawner_pos(spawner, bounds) + vec2(cos(a), sin(a)) * sqrt(rand(s)) * spread;
			vel = vec2(0.0);
			hp = HP[type];
			cool = rand(s) * COOLDOWN[type];
			respawn = 0.0;
			retreat = false;
			killer = NO_KILLER;
			vec2 inward = centre - pos;
			heading = atan(inward.y, inward.x);
		}
	} else {
		// My shot this frame, at last frame's target.
		if (target >= 0 && type != HOWITZER) {
			ivec2 tt = unit_texel(target);
			vec2 tp = texelFetch(motion_in, tt, 0).xy;
			if (is_alive(texelFetch(unit_in, tt, 0)) && fires_at(u, me, pos, target, tp)) {
				cool = COOLDOWN[type];
				tracer = TRACER;
			}
		}
		cool = max(cool, 0.0);
		float share = hp / HP[type];

		Scan s = Scan(1e9, -1, pos, 1e9, pos, -1, false, false, 1e9, pos, vec2(0.0), 0.0, 0.0, NO_KILLER);
		if (use_grid) {
			// A row of cells is a run of consecutive keys, so each row of the
			// neighbourhood is one stretch of the sorted list.
			ivec2 g = grid_size(bounds);
			ivec2 c = cell_of(pos, bounds);
			// Snipers look further, to pick targets at their longer range.
			int reach = type == SNIPER ? SNIPER_REACH : REACH;
			ivec2 lo = max(c - reach, ivec2(0));
			ivec2 hi = min(c + reach, g - 1);
			for (int cy = lo.y; cy <= hi.y; cy++) {
				int first = table_entry(cy * g.x + lo.x, 0);
				int last = table_entry(cy * g.x + hi.x, 1);
				for (int i = first; i < last; i++)
					scan(int(texelFetch(grid_in, slot_texel(i), 0).y), me, f, type, pos, share, s);
			}
		} else {
			for (int j = 0; j < count; j++)
				scan(j, me, f, type, pos, share, s);
		}
		buffed = s.buffed;

		// A nearby commander's target comes first, if I can hit it.
		if (s.focus >= 0 && type != MEDIC && type != COMMANDER && faction_of(s.focus) != f) {
			ivec2 tt = unit_texel(s.focus);
			vec2 tp = texelFetch(motion_in, tt, 0).xy;
			if (is_alive(texelFetch(unit_in, tt, 0)) && can_hit(type, pos, tp)) {
				s.pick = s.focus;
				s.pick_pos = tp;
			}
		}

		shellfire(f, type, pos, count, s);
		sniper_fire(me, type, share, s);

		// A howitzer ignores the scan's pick: it holds its target while a shell
		// is up, then fires at the biggest mass its squad has found.
		int gun = -1;
		if (type == HOWITZER) {
			s.pick = -1;
			if (shell > 0.0) {
				gun = target;
			} else {
				uint seed = hash(uint(me) * 3u) ^ hash(floatBitsToUint(time));
				gun = gun_target(me, f, pos, target, count, seed);
				if (gun >= 0 && cool <= 0.0) {
					cool = COOLDOWN[HOWITZER];
					shell = SHELL_FLIGHT;
					tracer = TRACER * 3.0;
				}
			}
		}

		hp -= s.damage * (s.aura ? AURA_SHARE : 1.0);
		hp += s.heal;
		int home = nearest_owned(f, pos);
		if (home >= 0 && distance(pos, spawner_pos(home, bounds)) < CAPTURE_RADIUS)
			hp += BASE_REGEN * dt;
		hp = min(hp, HP[type]);

		if (hp <= 0.0) {
			died = true;
			killer = s.hit_by;
			respawn = RESPAWN[type];
			vel = vec2(0.0);
			target = -1;
		} else {
			share = hp / HP[type];
			if (share < RETREAT_AT[type])
				retreat = true;
			else if (share > 0.9)
				retreat = false;

			int goal = squad_objective(f, squad_of(me));
			vec2 objective = goal >= 0 ? goal_pos(goal, bounds) : centre;
			bool engaged = s.pick >= 0;
			vec2 dir = vec2(0.0);
			if (type == HOWITZER && gun >= 0) {
				// Dug in while there's a mass to shell.
			} else if (retreat) {
				// Back to a medic if one's near, else home along the home field.
				if (type != MEDIC && s.medic_dist < SIGHT)
					dir = s.medic_dist > 30.0 ? safe_normalize(s.medic_pos - pos) : vec2(0.0);
				else
					dir = home >= 0 ? downhill(home, pos, spawner_pos(home, bounds), false) : safe_normalize(centre - pos);
			} else if (engaged) {
				vec2 to = s.pick_pos - pos;
				float d = length(to);
				to = safe_normalize(to);
				if (KITE[type] > 0.0 && s.enemy_dist < KITE[type])
					dir = safe_normalize(pos - s.enemy_pos);
				else if (d > RANGE[type] * HOLD_AT[type])
					dir = to;
				else if (type == SCOUT)
					dir = vec2(-to.y, to.x) * (me % 2 == 0 ? 1.0 : -1.0);   // circle the target
			} else {
				// In formation when near enough its slot; otherwise along the field.
				vec2 slot_velocity;
				if (formation_velocity(me, f, type, pos, goal, objective, count, slot_velocity)) {
					dir = slot_velocity / SPEED[type];
					if (length(dir) > 1.0)
						dir = normalize(dir);
				} else {
					dir = march(goal, pos, objective);
				}
			}

			vec2 want = (dir + s.push * 1.5 + rock_push(pos) * 2.0) * SPEED[type];
			vel = mix(vel, want, 1.0 - exp(-RESPONSE * dt));
			vec2 next = clamp(pos + vel * dt, bounds.xy, bounds.zw);
			// Slide along rock rather than into it.
			if (rock_at(next)) {
				if (!rock_at(vec2(next.x, pos.y))) {
					next = vec2(next.x, pos.y);
					vel.y = 0.0;
				} else if (!rock_at(vec2(pos.x, next.y))) {
					next = vec2(pos.x, next.y);
					vel.x = 0.0;
				} else {
					next = pos;
					vel = vec2(0.0);
				}
			}
			pos = next;

			vec2 aim = s.pick_pos - pos;
			if (type == HOWITZER && gun >= 0)
				aim = texelFetch(motion_in, unit_texel(gun), 0).xy - pos;
			if ((engaged || gun >= 0) && length(aim) <= RANGE[type])
				heading = atan(aim.y, aim.x);
			else if (length(vel) > 5.0)
				heading = atan(vel.y, vel.x);
			target = type == HOWITZER ? gun : s.pick;
		}
	}

	out_motion = vec4(pos, vel);
	out_unit = vec4(hp, cool, float(target), respawn);
	out_fx = vec4(pack_flags(killer, died, buffed, retreat), tracer, heading, shell);
}

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	ivec2 size = textureSize(motion_in, 0);
	int count = size.x * (size.y - 1);
	vec4 m = texelFetch(motion_in, texel, 0);
	vec4 u = texelFetch(unit_in, texel, 0);
	vec4 fx = texelFetch(fx_in, texel, 0);
	vec2 centre = (bounds.xy + bounds.zw) * 0.5;

	if (texel.y == 0) {
		// Control row: faction totals, then spawners, then squad objectives.
		vec4 c0 = vec4(0.0);
		vec4 c1 = vec4(0.0);
		vec4 c2 = vec4(0.0);
		if (texel.x < FACTIONS) {
			int f = texel.x;
			vec4 total = vec4(0.0);
			for (int x = 0; x < WIDTH; x++)
				total += texelFetch(faction_tally, ivec2(x, f), 0);
			vec2 sum = total.xy;
			float alive = total.z;
			float kills = total.w;
			vec2 army = alive > 0.0 ? sum / alive : spawner_pos(f, bounds);
			c0 = vec4(army, alive, m.w + kills);
		} else if (texel.x >= OBJECTIVE_TEXEL && texel.x < OBJECTIVE_TEXEL + FACTIONS * SQUADS / 4) {
			c1 = pick_objectives(texel.x);
			step_leaders(texel.x, c1, m, fx, centre, c0, c2);
		} else if (texel.x >= SPAWNER_TEXEL && texel.x < SPAWNER_TEXEL + SPAWNERS) {
			// A spawner: count who stands in its ring. If the owner has nobody
			// there, the strongest faction present takes it over CAPTURE_TIME;
			// otherwise any capture drains away.
			int s = texel.x - SPAWNER_TEXEL;
			vec2 p = spawner_pos(s, bounds);
			int owner = spawner_owner(s, fx);
			int capturer = int(fx.y);
			float progress = fx.z;
			vec4 near = vec4(0.0);
			float near_fifth = 0.0;
			if (use_grid) {
				ivec2 g = grid_size(bounds);
				int reach = int(ceil(CAPTURE_RADIUS / CELL));
				ivec2 c = cell_of(p, bounds);
				ivec2 lo = max(c - reach, ivec2(0));
				ivec2 hi = min(c + reach, g - 1);
				for (int cy = lo.y; cy <= hi.y; cy++) {
					int first = table_entry(cy * g.x + lo.x, 0);
					int last = table_entry(cy * g.x + hi.x, 1);
					for (int i = first; i < last; i++)
						tally(capturer_at(int(texelFetch(grid_in, slot_texel(i), 0).y), p), near, near_fifth);
				}
			} else {
				for (int j = 0; j < count; j++)
					tally(capturer_at(j, p), near, near_fifth);
			}
			float counts[5] = float[](near.x, near.y, near.z, near.w, near_fifth);
			int strongest = -1;
			float most = 0.0;
			for (int g = 0; g < FACTIONS; g++) {
				if (counts[g] > most) {
					most = counts[g];
					strongest = g;
				}
			}
			bool defended = owner < FACTIONS && counts[owner] > 0.0;
			if (!defended && strongest >= 0 && strongest != owner) {
				if (capturer != strongest) {
					capturer = strongest;
					progress = 0.0;
				}
				progress += dt * clamp(most / 10.0, 0.2, 1.0) / CAPTURE_TIME;
				if (progress >= 1.0) {
					owner = capturer;
					capturer = -1;
					progress = 0.0;
				}
			} else {
				progress = max(progress - dt / CAPTURE_TIME, 0.0);
				if (progress <= 0.0)
					capturer = -1;
			}
			c2 = vec4(float(owner + 1), float(capturer), progress, most);
		}
		COLOR0 = c0;
		COLOR1 = c1;
		COLOR2 = c2;
	} else {
		COLOR0 = vec4(0.0);
		COLOR1 = vec4(0.0);
		COLOR2 = vec4(0.0);
		step_unit(texel, count, m, u, fx, centre, COLOR0, COLOR1, COLOR2);
	}
}
