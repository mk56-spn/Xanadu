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

uniform bool use_grid = true;
uniform int sorted_rows;   // rows of grid_in holding the sorted list; the table starts below

uniform vec4 bounds;       // field rect: xy min corner, zw max corner
uniform float dt;
uniform float time;

#include "res://Experiments/Rts/rts_rules.gdshaderinc"
#include "res://Experiments/Rts/rts_grid.gdshaderinc"

const float SPAWN_SPREAD = 45.0;
const float RESPONSE = 6.0;      // per second: how quickly velocity follows intent

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

bool rock_at(vec2 p) {
	return texelFetch(nav_field, nav_cell(p, bounds), 0).y > 0.5;
}

bool clear_line(vec2 a, vec2 b) {
	int steps = int(distance(a, b) / LOS_STEP);
	for (int i = 1; i < steps; i++) {
		if (rock_at(mix(a, b, float(i) / float(steps))))
			return false;
	}
	return true;
}

// In range, and either in line of sight or lobbed (artillery, past its minimum).
bool can_hit(int type, vec2 from, vec2 to) {
	float d = distance(from, to);
	if (d > RANGE[type])
		return false;
	if (type == ARTILLERY)
		return d >= ARTILLERY_MIN;
	return clear_line(from, to);
}

// Last frame's fire rule, used by the shooter and by its target alike.
bool fires_at(vec4 shooter_unit, int shooter, vec2 shooter_pos, int target, vec2 target_pos) {
	return int(shooter_unit.z) == target
		&& shooter_unit.y - dt <= 0.0
		&& can_hit(type_of(shooter), shooter_pos, target_pos);
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
	if (type == SNIPER && (other == MEDIC || other == ARTILLERY || other == COMMANDER || other == SNIPER))
		return dist * 0.3;
	if (type == ARTILLERY && (other == HEAVY || other == GUARDIAN || other == ARTILLERY))
		return dist * 0.5;
	if (type == SCOUT && (other == ARTILLERY || other == SNIPER || other == MEDIC))
		return dist * 0.5;
	return dist;
}

// Toward the neighbouring nav cell nearest this tile's goal; straight at the
// fallback when the field hasn't reached here or we're already at the goal.
vec2 downhill(int tile, vec2 pos, vec2 fallback) {
	ivec2 n = nav_size(bounds);
	ivec2 c = nav_cell(pos, bounds);
	ivec2 origin = tile_origin(tile, bounds);
	float here = texelFetch(nav_field, origin + c, 0).x;
	if (here >= NO_PATH * 0.5)
		return safe_normalize(fallback - pos);
	float best = here;
	vec2 toward = fallback;
	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			ivec2 q = c + ivec2(dx, dy);
			if ((dx == 0 && dy == 0) || q.x < 0 || q.y < 0 || q.x >= n.x || q.y >= n.y)
				continue;
			vec4 v = texelFetch(nav_field, origin + q, 0);
			if (v.y > 0.5)
				continue;
			if (dx != 0 && dy != 0
					&& (texelFetch(nav_field, origin + c + ivec2(dx, 0), 0).y > 0.5
					|| texelFetch(nav_field, origin + c + ivec2(0, dy), 0).y > 0.5))
				continue;
			if (v.x < best) {
				best = v.x;
				toward = nav_centre(q, bounds);
			}
		}
	}
	return safe_normalize(toward - pos);
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
	if (type != MEDIC && dist < SIGHT) {
		float score = target_score(type, other, dist, uj.x / HP[other]);
		if (score < s.best_score && (type == ARTILLERY ? dist >= ARTILLERY_MIN : clear_line(pos, pj))) {
			s.best_score = score;
			s.pick = j;
			s.pick_pos = pj;
		}
	}
	if (other == MEDIC)
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

// Where a respawning unit comes back: half the time the owned spawner nearest
// its squad's objective (reinforcing the front), otherwise any owned one. -1 if none.
int respawn_spawner(int f, vec2 objective, inout uint seed) {
	int owned = 0;
	for (int s = 0; s < SPAWNERS; s++)
		owned += owner_of(s) == f ? 1 : 0;
	if (owned == 0)
		return -1;
	if (rand(seed) < 0.5)
		return nearest_owned(f, objective);
	int pick = int(rand(seed) * float(owned)) % owned;
	for (int s = 0; s < SPAWNERS; s++) {
		if (owner_of(s) == f) {
			if (pick == 0)
				return s;
			pick--;
		}
	}
	return -1;
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

int table_entry(int cell, int part) {
	ivec2 t = ivec2(cell % WIDTH, sorted_rows + cell / WIDTH);
	vec4 entry = texelFetch(grid_in, t, 0);
	return int(part == 0 ? entry.x : entry.y);
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

	if (!is_alive(u)) {
		// Dead, or never spawned (a cleared texture is hp 0, wait 0).
		respawn -= dt;
		target = -1;
		tracer = 0.0;
		uint s = hash(uint(me)) ^ hash(floatBitsToUint(time));
		vec4 objectives = texelFetch(unit_in, ivec2(f, 0), 0);
		int spawner = respawn <= 0.0 ? respawn_spawner(f, squad_of(me) == 0 ? objectives.xy : objectives.zw, s) : -1;
		// No spawner left to come back at: wait, and try again in a second.
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
		if (target >= 0) {
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
			ivec2 lo = max(c - REACH, ivec2(0));
			ivec2 hi = min(c + REACH, g - 1);
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

			vec4 objectives = texelFetch(unit_in, ivec2(f, 0), 0);
			int squad = squad_of(me);
			vec2 objective = squad == 0 ? objectives.xy : objectives.zw;
			int field = f * 2 + squad;
			bool engaged = s.pick >= 0;
			vec2 dir = vec2(0.0);
			if (retreat) {
				// Back to a medic if one's near, else home along the home field.
				if (type != MEDIC && s.medic_dist < SIGHT)
					dir = s.medic_dist > 30.0 ? safe_normalize(s.medic_pos - pos) : vec2(0.0);
				else
					dir = downhill(HOME_TILE + f, pos, home >= 0 ? spawner_pos(home, bounds) : centre);
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
			} else if (type == MEDIC) {
				// Nobody to heal: keep up with the army.
				vec2 army = texelFetch(motion_in, ivec2(f, 0), 0).xy;
				dir = distance(army, pos) > 120.0 ? downhill(field, pos, objective) : vec2(0.0);
			} else {
				dir = downhill(field, pos, objective);
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
			if (engaged && length(aim) <= RANGE[type])
				heading = atan(aim.y, aim.x);
			else if (length(vel) > 5.0)
				heading = atan(vel.y, vel.x);
			target = s.pick;
		}
	}

	out_motion = vec4(pos, vel);
	out_unit = vec4(hp, cool, float(target), respawn);
	out_fx = vec4(pack_flags(killer, died, buffed, retreat), tracer, heading, 0.0);
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
		// Control row: faction totals and objectives, then spawners.
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

			// Squad A takes the nearest spawner we don't own; squad B the next
			// nearest, unless one of ours is being taken, which it goes to save.
			vec2 a = centre;
			vec2 b = centre;
			float first = 1e20;
			float second = 1e20;
			float threatened = 1e20;
			vec2 rescue = centre;
			for (int s = 0; s < SPAWNERS; s++) {
				vec4 state = spawner_state(s);
				vec2 p = spawner_pos(s, bounds);
				float d = distance(army, p);
				if (spawner_owner(s, state) != f) {
					if (d < first) {
						second = first;
						b = a;
						first = d;
						a = p;
					} else if (d < second) {
						second = d;
						b = p;
					}
				} else if (state.z > 0.0 && d < threatened) {
					threatened = d;
					rescue = p;
				}
			}
			if (second >= 1e20)
				b = a;
			if (threatened < 1e20)
				b = rescue;
			c1 = vec4(a, b);
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
