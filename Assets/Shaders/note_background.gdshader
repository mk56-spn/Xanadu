shader_type canvas_item;

// Rows of music notes drifting across a dark gradient, black unless tinted.
// The notes are font glyphs, rasterised into the `notes` atlas (one square
// cell per glyph).
// Each layer is a grid of square cells; a row of cells slides sideways at its
// own speed and each cell holds at most one note of its own size and tilt.

uniform sampler2D notes : filter_linear_mipmap, repeat_disable;
uniform int glyph_count = 2;
uniform float aspect = 1.7778;

// Divides the cell count: a small frame wants a few big notes, not many tiny ones.
uniform float zoom = 1.0;
uniform float speed = 1.0;

// How loud the music is for each layer, far to near: the far notes follow the
// bass and the near ones the treble. Zero when nothing is playing.
uniform vec4 energy = vec4(0.0);

// 1 while music drives the notes, 0 when nothing is playing. Driven notes all
// but vanish as their layer goes quiet; undriven ones keep their normal look.
uniform float reactive = 0.0;
uniform vec4 note_color : source_color = vec4(0.96, 0.77, 0.26, 1.0);
uniform vec4 top_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform vec4 bottom_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);

// A soft halo that follows each note's outline, in this colour; zero leaves
// the notes bare and skips the extra samples.
uniform vec4 glow_color : source_color = vec4(1.0, 0.72, 0.2, 1.0);
uniform float glow = 0.0;

// How far the halo spreads past the ink, as a share of the note's box. The
// atlas leaves a tenth of a cell clear on each side, so this stays inside it.
const float GLOW_REACH = 0.09;

// The blurrier mip the halo is read from, so few samples still look smooth.
const float GLOW_LOD = 3.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// The glyph's coverage at q in its box, read at `lod` (below zero picks it
// automatically); nothing outside the box, so neighbouring cells never bleed in.
float glyph_ink(vec2 q, float glyph, float lod) {
	if (q.x < 0.0 || q.x > 1.0 || q.y < 0.0 || q.y > 1.0) {
		return 0.0;
	}

	vec2 atlas_uv = vec2((q.x + glyph) / float(glyph_count), q.y);
	return lod < 0.0 ? texture(notes, atlas_uv).a : textureLod(notes, atlas_uv, lod).a;
}

// The glyph blurred outwards: a centre and two rings of samples from a blurry
// mip, so the halo hugs the note's outline rather than its box.
float glyph_halo(vec2 q, float glyph) {
	float sum = glyph_ink(q, glyph, GLOW_LOD) * 2.0;
	float weight = 2.0;
	for (int i = 0; i < 8; i++) {
		float a = float(i) * 0.785398;
		vec2 dir = vec2(cos(a), sin(a));
		sum += glyph_ink(q + dir * GLOW_REACH * 0.5, glyph, GLOW_LOD) * 1.5;
		sum += glyph_ink(q + dir * GLOW_REACH, glyph, GLOW_LOD);
		weight += 2.5;
	}

	return sum / weight;
}

// Coverage of the note, if any, at uv in one layer (x), and of its halo (y).
// `rows` cells span the screen's height, so a smaller number means bigger,
// nearer notes.
vec2 note_layer(vec2 uv, float rows, float drift, float seed, float pulse) {
	vec2 p = vec2(uv.x * aspect, uv.y) * (rows / zoom);
	float row = floor(p.y);

	// Every row of cells moves at its own speed, rightwards.
	float row_speed = drift * (0.6 + 0.8 * hash(vec2(row, seed)));
	p.x -= TIME * speed * row_speed;

	vec2 cell = floor(p);
	vec2 local = fract(p) - 0.5;

	float h1 = hash(cell + seed);
	float h2 = hash(cell + seed + 17.0);
	float h3 = hash(cell + seed + 41.0);
	if (h1 > 0.4) {
		return vec2(0.0);
	}

	// Loud passages swell the notes, up to the edge of their cell.
	float size = min(mix(0.35, 0.9, h2) * (1.0 + 0.4 * pulse), 0.98);
	float tilt = (h3 - 0.5) * 0.9;

	// Scatter the note inside its cell, then bob it gently.
	vec2 offset = (vec2(h2, h3) - 0.5) * (1.0 - size) * 0.6;
	offset.y += sin(TIME * speed * 0.8 + h1 * 40.0) * 0.04;
	vec2 d = local - offset;

	float c = cos(tilt);
	float s = sin(tilt);
	d = vec2(c * d.x + s * d.y, -s * d.x + c * d.y);

	vec2 q = d / size + 0.5;
	float margin = glow > 0.0 ? GLOW_REACH : 0.0;
	if (q.x < -margin || q.x > 1.0 + margin || q.y < -margin || q.y > 1.0 + margin) {
		return vec2(0.0);
	}

	float presence = mix(1.0, 0.03 + 0.97 * smoothstep(0.0, 0.5, pulse), reactive);
	float strength = presence * (1.0 + 1.0 * pulse);
	float glyph = floor(h1 * 100.0) - float(glyph_count) * floor(floor(h1 * 100.0) / float(glyph_count));
	float ink = glyph_ink(q, glyph, -1.0);
	float halo = glow > 0.0 ? glyph_halo(q, glyph) : 0.0;
	return vec2(ink * strength, halo * strength);
}

// Lays one layer over the colour: its halo first, added like light, then its notes.
vec3 layer(vec3 color, vec2 coverage, float weight) {
	color += glow_color.rgb * coverage.y * weight * glow;
	return mix(color, note_color.rgb, coverage.x * weight);
}

void fragment() {
	vec2 uv = UV;
	vec3 color = mix(top_color.rgb, bottom_color.rgb, uv.y);

	// Far layers first, so the near, large notes sit on top and glow brighter.
	color = layer(color, note_layer(uv, 14.0, 0.12, 3.0, energy.x), 0.12);
	color = layer(color, note_layer(uv, 9.0, 0.2, 11.0, energy.y), 0.18);
	color = layer(color, note_layer(uv, 5.5, 0.32, 23.0, energy.z), 0.28);
	color = layer(color, note_layer(uv, 3.0, 0.5, 37.0, energy.w), 0.4);

	COLOR = vec4(color, 1.0);
}
