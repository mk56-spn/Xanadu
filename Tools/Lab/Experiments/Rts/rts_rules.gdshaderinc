// The rules every RTS shader agrees on: the state layout, who is what, and the
// unit stats. Included by the step, the pathing passes and the drawing.
//
// State textures are WIDTH texels wide. Row 0 is the control row: texel f
// holds faction f's totals. Every row below it holds units, one per texel.
//
//   motion  unit: xy position, zw velocity
//           row 0: xy army centre, z units alive, w kills so far; texels
//           OBJECTIVE_TEXEL on: squad leaders (see leader_texel)
//   unit    unit: x hp, y cooldown, z target id (-1 none), w respawn wait (> 0 while dead)
//           row 0, texels OBJECTIVE_TEXEL on: every squad's objective, four to
//           a texel (see squad_objective_texel), as spawner + 1 (0 for none)
//   fx      unit: x flags (killer, died, buffed, retreating), y tracer time left, z heading
//           row 0, texels SPAWNER_TEXEL on: one per spawner, x owner + 1 (0 before
//           the first step), y faction capturing it (-1 none), z capture progress 0..1;
//           texels OBJECTIVE_TEXEL on: squad leaders, as in motion

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
const int HOWITZER = 10;  // huge range, crawls; shells whatever enemy mass its squad has found

const int TYPES = 11;
const float SPEED[TYPES] = float[](190.0, 100.0, 55.0, 80.0, 45.0, 110.0, 150.0, 60.0, 160.0, 85.0, 12.0);
const float HP[TYPES] = float[](50.0, 110.0, 420.0, 70.0, 120.0, 90.0, 160.0, 380.0, 70.0, 260.0, 320.0);
const float DAMAGE[TYPES] = float[](6.0, 12.0, 40.0, 70.0, 45.0, 18.0, 18.0, 10.0, 28.0, 16.0, 150.0);   // a medic's is healing; a howitzer's at the shell's centre
const float RANGE[TYPES] = float[](70.0, 130.0, 170.0, 600.0, 270.0, 80.0, 20.0, 60.0, 45.0, 150.0, 2600.0);
const float COOLDOWN[TYPES] = float[](0.35, 0.8, 2.0, 3.0, 3.5, 0.6, 0.5, 1.0, 0.9, 1.0, 10.0);
const float RESPAWN[TYPES] = float[](2.0, 3.0, 6.0, 4.0, 6.0, 3.0, 3.0, 5.0, 3.5, 8.0, 15.0);
const float SIZE[TYPES] = float[](5.0, 6.0, 10.0, 6.0, 9.0, 6.0, 7.0, 11.0, 6.0, 9.0, 14.0);
const float ARMOR[TYPES] = float[](0.0, 1.0, 6.0, 0.0, 1.0, 0.0, 2.0, 4.0, 0.0, 3.0, 3.0);   // off every hit
const float RETREAT_AT[TYPES] = float[](0.45, 0.3, 0.25, 0.4, 0.35, 0.35, 0.0, 0.2, 0.4, 0.4, 0.0);   // hp share that sends it back
const float HOLD_AT[TYPES] = float[](0.8, 0.85, 0.85, 0.9, 0.9, 0.7, 0.3, 0.6, 0.5, 0.85, 1.0);    // share of range it fights from
const float KITE[TYPES] = float[](0.0, 0.0, 0.0, 160.0, 150.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);      // enemies this close make it back off

// Howitzers: a few per faction, at fixed ranks so anyone can list them. One
// shell at a time; it flies SHELL_FLIGHT s and lands on its target's position,
// hurting every enemy of the gun within SHELL_SPLASH, most at the centre.
const int HOWITZERS = 16;            // per faction, given 1024 or more slots
const int HOWITZER_EVERY = 64;       // one rank in this many is a howitzer
const float HOWITZER_MIN = 400.0;    // px: too close to shell
const float SHELL_FLIGHT = 2.5;
const float SHELL_SPLASH = 220.0;
const float MASS_MIN = 12.0;        // units in a grid cell worth a shell

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

// Navigation: a coarse grid of NAV_CELL px. The nav texture holds one tile per
// goal (see goal_pos), NAV_COLUMNS to a row, each the whole grid: x distance to
// that goal in cells, y 1 where rock, z unused, w a mask of the neighbours a
// unit can step to (see open_to). Goals and rock never move, so the fields are
// relaxed at startup and then left alone; every squad, retreat and objective
// follows the field of the goal it is heading for. Half float, so NO_PATH
// stays under half's 65504.
const float NAV_CELL = 96.0;
const int NAV_COLUMNS = 8;
const float NO_PATH = 60000.0;

// fx.x flags.
const int NO_KILLER = 7;
const int DIED_BIT = 3;
const int BUFFED_BIT = 4;
const int RETREAT_BIT = 5;

// The bit for neighbour (dx, dy), each -1..1, in a nav texel's w mask.
int neighbour_bit(int dx, int dy) { return 1 << ((dy + 1) * 3 + dx + 1); }

// Whether the nav mask lets a unit step to neighbour (dx, dy): inside the
// field, not rock, and for a diagonal, not cutting a rock corner.
bool open_to(float mask, int dx, int dy) { return (int(mask) & neighbour_bit(dx, dy)) != 0; }

int unit_id(ivec2 texel) { return (texel.y - 1) * WIDTH + texel.x; }
ivec2 unit_texel(int id) { return ivec2(id % WIDTH, id / WIDTH + 1); }
int faction_of(int id) { return id % FACTIONS; }

// Howitzer k of faction f.
int howitzer_id(int f, int k) { return ((k + 1) * HOWITZER_EVERY - 1) * FACTIONS + f; }

// Every faction's mix, per 20: 6 rifles, 3 scouts, 2 heavies, 2 medics,
// 2 berserkers, one each of the rest; and its first HOWITZERS ranks of every
// HOWITZER_EVERY are howitzers instead.
// The type at place k (0-19) of the twenty-unit mix.
int mix_type(int k) {
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

int type_of(int id) {
	int rank = id / FACTIONS;
	if (rank % HOWITZER_EVERY == HOWITZER_EVERY - 1 && rank < HOWITZERS * HOWITZER_EVERY)
		return HOWITZER;
	return mix_type(rank % 20);
}

bool is_alive(vec4 unit_state) { return unit_state.w <= 0.0 && unit_state.x > 0.0; }

int killer_of(float flags) { return int(flags) & 7; }
bool has_flag(float flags, int bit) { return ((int(flags) >> bit) & 1) == 1; }

float pack_flags(int killer, bool died, bool buffed, bool retreat) {
	return float(killer | (died ? 1 << DIED_BIT : 0) | (buffed ? 1 << BUFFED_BIT : 0) | (retreat ? 1 << RETREAT_BIT : 0));
}

// Counters: what each type is especially good against.
float bonus(int shooter, int target) {
	if (shooter == SCOUT && (target == ARTILLERY || target == SNIPER || target == HOWITZER)) return 2.0;
	if (shooter == RIFLE && (target == SCOUT || target == ASSASSIN)) return 1.5;
	if (shooter == HEAVY && (target == RIFLE || target == BERSERKER)) return 1.5;
	if (shooter == SNIPER && (target == COMMANDER || target == MEDIC)) return 1.5;
	if (shooter == BERSERKER && target == HEAVY) return 1.5;
	if (shooter == ASSASSIN && (target == SNIPER || target == MEDIC || target == COMMANDER || target == HOWITZER)) return 1.5;
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
const int CAP_PER_SPAWNER = 1000;

// What a spawner brings units back as. A main base raises the line infantry;
// the neutral sites each raise their own specialists, so armies have to take
// ground to field them, and respawns are spread over many places.
const int SITE_BASE = 0;       // rifles, scouts, medics
const int SITE_FACTORY = 1;    // heavies, guardians
const int SITE_FIREBASE = 2;   // artillery, snipers, howitzers
const int SITE_BARRACKS = 3;   // berserkers, assassins
const int SITE_COMMAND = 4;    // commanders     // units a faction may field per spawner beyond its base

// How many of a faction's units may be up: half its slots while it holds just
// its base, CAP_PER_SPAWNER more or less for every spawner more or fewer. Units
// past the cap that are already up fight on; dead ones wait for room.
int unit_cap(int slots, int owned) {
	return clamp(slots / 2 + CAP_PER_SPAWNER * (owned - 1), 0, slots);
}

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

// A neutral spawner's site, varying across the grid's diagonals so like
// sites don't line up: of every seven, two factories, two firebases, two
// barracks and one command post.
int site_of(int s) {
	if (s < FACTIONS)
		return SITE_BASE;
	int i = s - FACTIONS;
	int k = (i % 7 + 2 * (i / 7)) % 7;
	if (k < 2) return SITE_FACTORY;
	if (k < 4) return SITE_FIREBASE;
	if (k < 6) return SITE_BARRACKS;
	return SITE_COMMAND;
}

bool site_raises(int site, int type) {
	if (site == SITE_BASE) return type == RIFLE || type == SCOUT || type == MEDIC;
	if (site == SITE_FACTORY) return type == HEAVY || type == GUARDIAN;
	if (site == SITE_FIREBASE) return type == ARTILLERY || type == SNIPER || type == HOWITZER;
	if (site == SITE_BARRACKS) return type == BERSERKER || type == ASSASSIN;
	return type == COMMANDER;
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

// Goals: what a squad can head for, and a nav tile each. 0-39 the spawners,
// then a waypoint at the middle of every sector of a SECTOR_COLUMNS x
// SECTOR_ROWS split of the field, for exploring. Armies start knowing only
// their own base: a spawner is known to a faction once one of its units has
// been within DISCOVER_RADIUS, and a sector scouted once within SCOUTED_RADIUS
// of its waypoint. The intel pass keeps that, one texel per goal.
const int SECTOR_COLUMNS = 6;
const int SECTOR_ROWS = 4;
const int SECTORS = SECTOR_COLUMNS * SECTOR_ROWS;
const int GOALS = SPAWNERS + SECTORS;
const float DISCOVER_RADIUS = 900.0;
const float SCOUTED_RADIUS = 1200.0;
const int SECTOR_SEED = 5;     // nav cells round a waypoint that seed its field, so one in rock still has a way in
const int RECON_SQUADS = 2;    // squads 0 and 1 explore first, while any sector is unscouted

vec2 sector_centre(int i, vec4 field) {
	vec2 cell = (field.zw - field.xy) / vec2(float(SECTOR_COLUMNS), float(SECTOR_ROWS));
	return field.xy + (vec2(float(i % SECTOR_COLUMNS), float(i / SECTOR_COLUMNS)) + 0.5) * cell;
}

vec2 goal_pos(int g, vec4 field) {
	return g < SPAWNERS ? spawner_pos(g, field) : sector_centre(g - SPAWNERS, field);
}

// Each army splits into SQUADS squads, each after its own spawner. A squad is
// whole runs of the twenty-unit type mix, so every squad gets every type.
const int SQUADS = 12;
const int OBJECTIVE_TEXEL = SPAWNER_TEXEL + SPAWNERS;   // 48: FACTIONS * SQUADS / 4 texels from here

int squad_of(int id) { return (id / FACTIONS / 20) % SQUADS; }

// The control-row texel holding squad (f, q)'s objective; the channel is q % 4.
int squad_objective_texel(int f, int q) { return OBJECTIVE_TEXEL + (f * SQUADS + q) / 4; }

// Formations. Each squad marches behind a leader: a point that walks its
// objective's flow field at FORMATION_SPEED. Its position is kept in the same
// texel as the objective, squads q % 4 = 0, 1 in motion (xy, zw) and 2, 3 in fx.
// A unit's slot comes from its rank, so a dead unit leaves a hole. Bands run
// front to back: heavies, guardians and berserkers; rifles and assassins;
// medics and commanders; snipers and artillery. Scouts ride the flanks and
// howitzers keep their own pace.
const float FORMATION_SPEED = 40.0;   // px/s: the leader's walk, just under artillery's
const float SLOT = 26.0;              // px between neighbouring slots
const int ROW_MAX = 24;               // slots across a row; a band takes more rows when it outgrows one
const float BAND_GAP = 18.0;          // px of extra room between bands
const float FORMATION_JOIN = 700.0;   // px: farther than this from its slot, a unit marches the flow field instead
const float ARRIVED = 120.0;          // px: a leader this close to its objective stops

const int BAND_FLANK = 4;
const int BAND_FREE = 5;
// Places in the twenty-unit mix per band 0-3.
const int BAND_RUN[4] = int[](5, 7, 3, 2);

int band_of(int type) {
	if (type == HEAVY || type == GUARDIAN || type == BERSERKER) return 0;
	if (type == RIFLE || type == ASSASSIN) return 1;
	if (type == MEDIC || type == COMMANDER) return 2;
	if (type == SNIPER || type == ARTILLERY) return 3;
	if (type == SCOUT) return BAND_FLANK;
	return BAND_FREE;
}

vec2 safe_normalize(vec2 v) {
	float l = length(v);
	return l > 1e-4 ? v / l : vec2(0.0);
}
