// The rules every RTS shader agrees on: the state layout, who is what, and the
// unit stats. Included by the step, the pathing passes and the drawing.
//
// State textures are WIDTH texels wide. Row 0 is the control row: texel f
// holds faction f's totals. Every row below it holds units, one per texel.
//
//   motion  unit: xy position, zw velocity
//           row 0: xy army centre, z units alive, w kills so far
//   unit    unit: x hp, y cooldown, z target id (-1 none), w respawn wait (> 0 while dead)
//           row 0: xy squad A's objective, zw squad B's
//   fx      unit: x flags (killer, died, buffed, retreating), y tracer time left, z heading
//           row 0, texels SPAWNER_TEXEL on: one per spawner, x owner + 1 (0 before
//           the first step), y faction capturing it (-1 none), z capture progress 0..1

const int WIDTH = 64;
const int FACTIONS = 5;

const vec3 FACTION_COLORS[5] = vec3[](
	vec3(0.96, 0.32, 0.30),
	vec3(0.98, 0.78, 0.22),
	vec3(0.36, 0.86, 0.42),
	vec3(0.26, 0.68, 1.00),
	vec3(0.80, 0.46, 0.98));

const int SCOUT = 0;      // fast flanker, circles its target, hunts artillery and snipers
const int RIFLE = 1;      // line infantry
const int HEAVY = 2;      // armoured, crushes infantry
const int SNIPER = 3;     // long range, picks off medics, artillery and commanders, backs off
const int ARTILLERY = 4;  // lobs over walls, splash, can't fire up close
const int MEDIC = 5;      // heals the most wounded friend it can see
const int BERSERKER = 6;  // melee, hits harder the more hurt it is, never retreats
const int GUARDIAN = 7;   // friends near it take less damage
const int ASSASSIN = 8;   // hunts the weakest enemy, executes the wounded
const int COMMANDER = 9;  // friends near it hit harder and focus its target

const float SPEED[10] = float[](190.0, 100.0, 55.0, 80.0, 45.0, 110.0, 150.0, 60.0, 160.0, 85.0);
const float HP[10] = float[](50.0, 110.0, 420.0, 70.0, 120.0, 90.0, 160.0, 380.0, 70.0, 260.0);
const float DAMAGE[10] = float[](6.0, 12.0, 40.0, 70.0, 45.0, 18.0, 18.0, 10.0, 28.0, 16.0);   // a medic's is healing
const float RANGE[10] = float[](70.0, 130.0, 170.0, 300.0, 270.0, 80.0, 20.0, 60.0, 45.0, 150.0);
const float COOLDOWN[10] = float[](0.35, 0.8, 2.0, 3.0, 3.5, 0.6, 0.5, 1.0, 0.9, 1.0);
const float RESPAWN[10] = float[](2.0, 3.0, 6.0, 4.0, 6.0, 3.0, 3.0, 5.0, 3.5, 8.0);
const float SIZE[10] = float[](5.0, 6.0, 10.0, 6.0, 9.0, 6.0, 7.0, 11.0, 6.0, 9.0);
const float ARMOR[10] = float[](0.0, 1.0, 6.0, 0.0, 1.0, 0.0, 2.0, 4.0, 0.0, 3.0);   // off every hit
const float RETREAT_AT[10] = float[](0.45, 0.3, 0.25, 0.4, 0.35, 0.35, 0.0, 0.2, 0.4, 0.4);   // hp share that sends it back
const float HOLD_AT[10] = float[](0.8, 0.85, 0.85, 0.9, 0.9, 0.7, 0.3, 0.6, 0.5, 0.85);    // share of range it fights from
const float KITE[10] = float[](0.0, 0.0, 0.0, 160.0, 150.0, 0.0, 0.0, 0.0, 0.0, 0.0);      // enemies this close make it back off

const float SIGHT = 300.0;          // px a unit picks targets within
const float LOS_STEP = 24.0;        // px between line-of-sight samples
const float ARTILLERY_MIN = 90.0;
const float SPLASH = 50.0;
const float SPLASH_SHARE = 0.6;
const float AURA = 70.0;            // guardian
const float AURA_SHARE = 0.6;
const float BUFF = 140.0;           // commander
const float BUFF_SHARE = 1.35;
const float RAGE = 1.5;             // berserker: extra damage at zero hp
const float EXECUTE = 2.0;          // assassin, against targets under half hp
const float BASE_REGEN = 15.0;      // hp/s inside an owned spawner's ring
const float CLEAR_RADIUS = 650.0;   // no rocks this close to a spawner
const float TRACER = 0.12;          // s a shot stays drawn
const float WRECK = 1.0;            // s a wreck stays drawn

// Navigation: a coarse grid of NAV_CELL px. The nav texture holds NAV_TILES
// tiles, NAV_COLUMNS to a row, each the whole grid: x distance to that tile's
// goal in cells, y 1 where rock. Tiles 0-9 lead each army's two squads to
// their objectives (faction * 2 + squad); 10-14 home, to its owned spawners.
const float NAV_CELL = 96.0;
const int NAV_TILES = 15;
const int NAV_COLUMNS = 5;
const int HOME_TILE = 10;
const float NO_PATH = 1e6;

// fx.x flags.
const int NO_KILLER = 7;
const int DIED_BIT = 3;
const int BUFFED_BIT = 4;
const int RETREAT_BIT = 5;

int unit_id(ivec2 texel) { return (texel.y - 1) * WIDTH + texel.x; }
ivec2 unit_texel(int id) { return ivec2(id % WIDTH, id / WIDTH + 1); }
int faction_of(int id) { return id % FACTIONS; }

// Every faction's mix, per 20: 6 rifles, 3 scouts, 2 heavies, 2 medics,
// 2 berserkers, one each of the rest.
int type_of(int id) {
	int k = (id / FACTIONS) % 20;
	if (k < 6) return RIFLE;
	if (k < 9) return SCOUT;
	if (k < 11) return HEAVY;
	if (k < 13) return MEDIC;
	if (k < 15) return BERSERKER;
	if (k == 15) return SNIPER;
	if (k == 16) return ARTILLERY;
	if (k == 17) return GUARDIAN;
	if (k == 18) return ASSASSIN;
	return COMMANDER;
}

bool is_alive(vec4 unit_state) { return unit_state.w <= 0.0 && unit_state.x > 0.0; }

int killer_of(float flags) { return int(flags) & 7; }
bool has_flag(float flags, int bit) { return ((int(flags) >> bit) & 1) == 1; }

float pack_flags(int killer, bool died, bool buffed, bool retreat) {
	return float(killer | (died ? 1 << DIED_BIT : 0) | (buffed ? 1 << BUFFED_BIT : 0) | (retreat ? 1 << RETREAT_BIT : 0));
}

// Counters: what each type is especially good against.
float bonus(int shooter, int target) {
	if (shooter == SCOUT && (target == ARTILLERY || target == SNIPER)) return 2.0;
	if (shooter == RIFLE && (target == SCOUT || target == ASSASSIN)) return 1.5;
	if (shooter == HEAVY && (target == RIFLE || target == BERSERKER)) return 1.5;
	if (shooter == SNIPER && (target == COMMANDER || target == MEDIC)) return 1.5;
	if (shooter == BERSERKER && target == HEAVY) return 1.5;
	if (shooter == ASSASSIN && (target == SNIPER || target == MEDIC || target == COMMANDER)) return 1.5;
	return 1.0;
}

// Spawners: capture points units respawn at. Spawner f < FACTIONS is that
// faction's main base on the outer pentagon; the rest start neutral, on a
// jittered 7 x 5 grid over the field.
const int SPAWNERS = 40;
const int SPAWNER_TEXEL = 8;          // control-row texel of spawner 0
const int NEUTRAL = 5;                // owner of a spawner nobody holds
const float CAPTURE_RADIUS = 320.0;   // px: units inside count toward holding or taking it
const float CAPTURE_TIME = 8.0;       // s to take one with ten or more units
const float GRID_JITTER = 0.5;        // share of a grid cell a neutral spawner may wander

uint rules_hash(uint x) {
	x ^= x >> 16u; x *= 0x7feb352du;
	x ^= x >> 15u; x *= 0x846ca68bu;
	x ^= x >> 16u;
	return x;
}

vec2 spawner_pos(int s, vec4 field) {
	vec2 size = field.zw - field.xy;
	vec2 centre = (field.xy + field.zw) * 0.5;
	if (s < FACTIONS) {
		float a = -1.5707963 + float(s) * 1.2566371;
		return centre + vec2(cos(a), sin(a)) * size * 0.42 * vec2(1.0, 0.92);
	}
	int i = s - FACTIONS;
	vec2 cell = size / vec2(7.0, 5.0);
	vec2 jitter = vec2(float(rules_hash(uint(i) * 2u) >> 8u), float(rules_hash(uint(i) * 2u + 1u) >> 8u)) / 16777216.0 - 0.5;
	return field.xy + (vec2(float(i % 7), float(i / 7)) + 0.5 + jitter * GRID_JITTER) * cell;
}

// The owner from a spawner's control texel: before the first step, its starting owner.
int spawner_owner(int s, vec4 state) {
	if (state.x < 0.5)
		return s < FACTIONS ? s : NEUTRAL;
	return int(state.x + 0.5) - 1;
}

ivec2 nav_size(vec4 field) { return ivec2(ceil((field.zw - field.xy) / NAV_CELL)); }

ivec2 nav_cell(vec2 pos, vec4 field) {
	return clamp(ivec2(floor((pos - field.xy) / NAV_CELL)), ivec2(0), nav_size(field) - 1);
}

vec2 nav_centre(ivec2 cell, vec4 field) { return field.xy + (vec2(cell) + 0.5) * NAV_CELL; }

ivec2 tile_origin(int tile, vec4 field) {
	return ivec2(tile % NAV_COLUMNS, tile / NAV_COLUMNS) * nav_size(field);
}

// Each army splits in two squads with their own objectives.
int squad_of(int id) { return (id / FACTIONS) % 2; }

vec2 safe_normalize(vec2 v) {
	float l = length(v);
	return l > 1e-4 ? v / l : vec2(0.0);
}
