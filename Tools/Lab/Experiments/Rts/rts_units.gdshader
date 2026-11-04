// Draws every unit from the state textures: one triangle array, twelve
// vertices per unit, built once. UV holds the unit's texel. VERTEX.x is a
// corner (0..5) and VERTEX.y the part: 0 the body, 1 the tracer of its latest
// shot or heal.
//
// The body is a small quad turned to the unit's heading; the fragment shader
// draws the unit in it as a signed distance field: anti-aliased silhouette
// per type, an outline in its squad's colour, bevel light, drop shadow, a detail mark, a health arc,
// a muzzle flash and the commander's buff glow. Only the quad's few pixels pay.
shader_type canvas_item;

uniform sampler2D motion : filter_nearest;
uniform sampler2D unit : filter_nearest;
uniform sampler2D fx : filter_nearest;
// The simulation steps at a fixed rate; drawing blends from the step before.
uniform sampler2D motion_prev : filter_nearest;
uniform sampler2D fx_prev : filter_nearest;
uniform float blend = 1.0;   // 0 the previous step, 1 the latest

#include "res://Experiments/Rts/rts_rules.gdshaderinc"

const vec2 QUAD[6] = vec2[](
	vec2(-1.0, -1.0), vec2(1.0, -1.0), vec2(1.0, 1.0),
	vec2(-1.0, -1.0), vec2(1.0, 1.0), vec2(-1.0, 1.0));
// A unit strip: x from 0 to 1, y from -1 to 1.
const vec2 STRIP[6] = vec2[](
	vec2(0.0, -1.0), vec2(1.0, -1.0), vec2(1.0, 1.0),
	vec2(0.0, -1.0), vec2(1.0, 1.0), vec2(0.0, 1.0));

// The quad reaches this far, in body radii: room for the shadow, arc and glow.
const float EXTENT = 1.75;
const vec2 SHADOW = vec2(0.28, 0.38);   // world-aligned, in body radii
const vec2 LIGHT_FROM = vec2(-0.6, -0.8);    // screen direction the light comes from
const float BEAM_WIDTH[TYPES] = float[](1.0, 1.5, 3.0, 1.0, 4.0, 2.0, 2.0, 2.0, 1.5, 2.0, 8.0);
const float BEAM_GLOW = 2.5;            // the strip is this much wider than the core

// Zoomed far out, units and tracers grow so they never drop below these, in screen px.
const float MIN_BODY_PX = 2.5;
const float MIN_BEAM_PX = 1.0;
// Details and the health arc only from this many screen px per body radius.
const float DETAIL_PX = 5.0;
// The squad outline from this many, a little before the details.
const float OUTLINE_PX = 3.0;

// Outline colours: squad q of every army wears SQUAD_COLORS[q], so squads read
// apart within an army. Fill is the faction; these sit away from the faction
// hues where they can, and differ in lightness where they can't.
const vec3 SQUAD_COLORS[SQUADS] = vec3[](
	vec3(1.00, 1.00, 1.00),   // white
	vec3(0.08, 0.08, 0.10),   // black
	vec3(0.20, 0.95, 0.95),   // cyan
	vec3(1.00, 0.30, 0.85),   // magenta
	vec3(1.00, 0.55, 0.10),   // orange
	vec3(0.60, 1.00, 0.20),   // lime
	vec3(1.00, 0.70, 0.75),   // pink
	vec3(0.05, 0.45, 0.45),   // teal
	vec3(0.50, 0.30, 0.12),   // brown
	vec3(0.15, 0.20, 0.55),   // navy
	vec3(0.55, 0.55, 0.15),   // olive
	vec3(0.75, 0.65, 1.00));  // lavender

varying vec2 local;                 // body: body radii, unit's frame. beam: strip coordinates
varying vec2 upright;               // body: the same offset, world-aligned
varying flat int v_part;
varying flat int v_type;
varying flat vec4 v_color;          // rgb tint, a overall alpha
varying flat vec4 v_info;           // x hp share, y muzzle flash, z 1 buffed, w screen px per body radius
varying flat vec2 v_shadow;         // shadow offset in the unit's frame
varying flat vec3 v_outline;        // the squad's colour, or a dark edge for a wreck

// Farther than this in one step is a respawn, not movement: don't blend it.
const float TELEPORT = 200.0;

vec2 blended_pos(ivec2 texel) {
	vec2 now = texelFetch(motion, texel, 0).xy;
	vec2 before = texelFetch(motion_prev, texel, 0).xy;
	return distance(now, before) > TELEPORT ? now : mix(before, now, blend);
}

void vertex() {
	ivec2 texel = ivec2(UV);
	int corner = int(VERTEX.x);
	int part = int(VERTEX.y);
	vec4 m = texelFetch(motion, texel, 0);
	vec4 u = texelFetch(unit, texel, 0);
	vec4 e = texelFetch(fx, texel, 0);
	m.xy = blended_pos(texel);
	// Heading turns the short way round.
	float before = texelFetch(fx_prev, texel, 0).z;
	e.z = before + (mod(e.z - before + 3.1415927, 6.2831853) - 3.1415927) * blend;
	int id = unit_id(texel);
	int type = type_of(id);
	vec3 tint = FACTION_COLORS[faction_of(id)];
	bool alive = is_alive(u);
	float zoom = length((CANVAS_MATRIX * MODEL_MATRIX)[0].xy);   // world px to screen px
	float size = max(SIZE[type], MIN_BODY_PX / zoom);
	v_part = part;
	v_type = type;
	VERTEX = m.xy;
	v_color = vec4(0.0);
	v_outline = alive ? SQUAD_COLORS[squad_of(id)] : vec3(0.1);

	if (part == 0) {
		float share = clamp(u.x / HP[type], 0.0, 1.0);
		float alpha = 1.0;
		// Retreating units wash out toward grey.
		if (has_flag(e.x, RETREAT_BIT))
			tint = mix(tint, vec3(dot(tint, vec3(0.33))), 0.6);
		float scale = size;
		if (!alive) {
			// A grey wreck that shrinks away.
			float fade = 1.0 - clamp((RESPAWN[type] - u.w) / WRECK, 0.0, 1.0);
			scale *= fade;
			tint = vec3(0.3);
			alpha = 0.8 * fade;
			share = 1.0;
		}
		float c = cos(e.z);
		float s = sin(e.z);
		mat2 turn = mat2(vec2(c, s), vec2(-s, c));
		local = QUAD[corner] * EXTENT;
		upright = turn * local;
		VERTEX = m.xy + upright * scale;
		v_shadow = transpose(turn) * SHADOW;
		float flash = alive ? clamp((e.y / TRACER - 0.5) * 2.0, 0.0, 1.0) : 0.0;
		v_color = vec4(tint, alpha);
		v_info = vec4(share, flash, has_flag(e.x, BUFFED_BIT) && alive ? 1.0 : 0.0, scale * zoom);
	} else {
		int target = int(u.z);
		float left = e.y / TRACER;
		if (alive && target >= 0 && left > 0.0) {
			vec2 to = blended_pos(unit_texel(target)) - m.xy;
			float width = max(BEAM_WIDTH[type], MIN_BEAM_PX / zoom) * 0.5 * BEAM_GLOW;
			vec2 side = vec2(-to.y, to.x) / max(length(to), 1e-3) * width;
			local = STRIP[corner];
			VERTEX = m.xy + to * local.x + side * local.y;
			vec3 color = type == MEDIC ? vec3(0.4, 1.0, 0.55)
				: type == ARTILLERY ? vec3(1.0, 0.6, 0.2)
				: type == HOWITZER ? vec3(1.0, 0.35, 0.15)
				: mix(tint, vec3(1.0), 0.5);
			v_color = vec4(color, left);
		}
	}
}

// Signed distance helpers (negative inside), after Inigo Quilez.
float sd_circle(vec2 p, float r) {
	return length(p) - r;
}

float sd_box(vec2 p, vec2 b, float r) {
	vec2 q = abs(p) - b + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

float sd_segment(vec2 p, vec2 a, vec2 b, float r) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	return length(pa - ba * clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0)) - r;
}

float sd_triangle(vec2 p, vec2 p0, vec2 p1, vec2 p2) {
	vec2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
	vec2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
	vec2 pq0 = v0 - e0 * clamp(dot(v0, e0) / dot(e0, e0), 0.0, 1.0);
	vec2 pq1 = v1 - e1 * clamp(dot(v1, e1) / dot(e1, e1), 0.0, 1.0);
	vec2 pq2 = v2 - e2 * clamp(dot(v2, e2) / dot(e2, e2), 0.0, 1.0);
	float s = sign(e0.x * e2.y - e0.y * e2.x);
	vec2 d = min(min(vec2(dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
					 vec2(dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
					 vec2(dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
	return -sqrt(d.x) * sign(d.y);
}

// An n-pointed star with a point along +x; m sets how deep the valleys are.
float sd_star(vec2 p, float r, int n, float m) {
	p = vec2(p.y, p.x);
	float an = 3.141593 / float(n);
	float en = 3.141593 / m;
	vec2 acs = vec2(cos(an), sin(an));
	vec2 ecs = vec2(cos(en), sin(en));
	float bn = mod(atan(p.x, p.y), 2.0 * an) - an;
	p = length(p) * vec2(cos(bn), abs(sin(bn)));
	p -= r * acs;
	p += ecs * clamp(-dot(p, ecs), 0.0, r * acs.y / ecs.y);
	return length(p) * sign(p.x);
}

// Each type's silhouette, nose along +x, about one radius across.
float silhouette(int type, vec2 p) {
	if (type == SCOUT)
		return max(sd_triangle(p, vec2(1.05, 0.0), vec2(-0.85, 0.72), vec2(-0.85, -0.72)), -sd_circle(p - vec2(-1.0, 0.0), 0.5)) - 0.04;
	if (type == RIFLE)
		return sd_triangle(p, vec2(0.95, 0.0), vec2(-0.6, 0.8), vec2(-0.6, -0.8)) - 0.08;
	if (type == HEAVY)
		return min(sd_box(p, vec2(0.8, 0.68), 0.18), sd_segment(p, vec2(0.0), vec2(1.3, 0.0), 0.11));
	if (type == SNIPER)
		return min(sd_segment(p, vec2(-0.8, 0.0), vec2(1.35, 0.0), 0.16), sd_circle(p - vec2(-0.35, 0.0), 0.38));
	if (type == ARTILLERY)
		return min(min(sd_box(p - vec2(-0.25, 0.0), vec2(0.6, 0.62), 0.22), sd_segment(p, vec2(0.0), vec2(1.45, 0.0), 0.15)),
				   min(sd_circle(p - vec2(-0.25, 0.78), 0.26), sd_circle(p - vec2(-0.25, -0.78), 0.26)));
	if (type == HOWITZER)   // long barrel over a wide carriage, trails behind
		return min(min(sd_box(p - vec2(-0.2, 0.0), vec2(0.5, 0.7), 0.15), sd_segment(p, vec2(-0.1, 0.0), vec2(1.6, 0.0), 0.17)),
				   min(sd_segment(p, vec2(-0.5, 0.35), vec2(-1.3, 0.75), 0.1), sd_segment(p, vec2(-0.5, -0.35), vec2(-1.3, -0.75), 0.1)));
	if (type == MEDIC)
		return sd_circle(p, 0.85);
	if (type == BERSERKER)
		return sd_star(p, 1.0, 4, 3.2) - 0.03;
	if (type == GUARDIAN)
		return sd_box(p, vec2(0.55, 0.98), 0.45);
	if (type == ASSASSIN)
		return max(sd_circle(p, 0.92), -sd_circle(p - vec2(-0.48, 0.0), 0.78)) - 0.02;
	return sd_star(p, 1.05, 5, 3.0) - 0.04;   // commander
}

// A mark inside the body: negative where it is.
float detail(int type, vec2 p) {
	if (type == HEAVY)
		return abs(sd_circle(p, 0.36)) - 0.07;                                  // turret ring
	if (type == MEDIC)
		return min(sd_box(p, vec2(0.52, 0.15), 0.03), sd_box(p, vec2(0.15, 0.52), 0.03));   // cross
	if (type == SNIPER)
		return sd_circle(p - vec2(-0.35, 0.0), 0.14);                          // scope
	if (type == GUARDIAN)
		return sd_box(p, vec2(0.1, 0.62), 0.08);                               // shield boss
	if (type == COMMANDER)
		return abs(sd_circle(p, 0.32)) - 0.06;                                 // insignia
	if (type == RIFLE)
		return sd_circle(p - vec2(0.05, 0.0), 0.14);
	return 1.0;
}

vec4 over(vec4 under, vec3 color, float alpha) {
	return vec4(mix(under.rgb, color, alpha), under.a + alpha * (1.0 - under.a));
}

void fragment() {
	if (v_part == 1) {
		// Tracer: a hot core with a soft glow either side, fading in from the muzzle.
		float across = abs(local.y);
		float core = exp(-across * across * 14.0);
		float glow = exp(-across * across * 3.0) * 0.45;
		vec3 color = mix(v_color.rgb, vec3(1.0), core * 0.6);
		COLOR = vec4(color, (core + glow) * v_color.a * smoothstep(0.0, 0.08, local.x));
	} else {
		vec2 p = local;
		float d = silhouette(v_type, p);
		float aa = max(fwidth(d), 1e-4);
		float px = v_info.w;
		bool detailed = px >= DETAIL_PX;
		vec4 color = vec4(0.0);

		// Drop shadow, soft, under everything.
		float shadow = 1.0 - smoothstep(-0.1, 0.35, silhouette(v_type, p - v_shadow));
		color = over(color, vec3(0.0), shadow * 0.35);

		// Commander's buff: a warm glow hugging the outside.
		if (v_info.z > 0.5)
			color = over(color, vec3(1.0, 0.82, 0.35), exp(-max(d, 0.0) * 6.0) * 0.45 * step(0.0, d));

		// Body: lit from the light side by the distance field's gradient, darker
		// with damage, with a dark outline.
		vec2 grad = vec2(dFdx(d), dFdy(d));
		float facing = dot(grad / max(length(grad), 1e-5), -LIGHT_FROM);
		float rim = smoothstep(-0.35, 0.0, d) * clamp(facing, 0.0, 1.0);
		float shade = 0.92 + 0.18 * (1.0 - smoothstep(-0.6, 0.0, d));            // brighter toward the middle
		vec3 body = v_color.rgb * mix(0.6, 1.0, v_info.x) * shade;
		body = mix(body, vec3(1.0), rim * 0.45);
		if (detailed) {
			float mark = detail(v_type, p);
			vec3 mark_color = v_type == MEDIC ? vec3(1.0) : mix(body, vec3(0.05), 0.55);
			body = mix(body, mark_color, 1.0 - smoothstep(-aa, aa, mark));
		}
		float outline_width = px >= OUTLINE_PX ? max(0.16, aa) : 0.0;
		body = mix(body, v_outline, smoothstep(-outline_width - aa, -outline_width + aa, d));
		color = over(color, body, 1.0 - smoothstep(-aa, aa, d));

		// Muzzle flash just past the nose.
		float flash = exp(-length(p - vec2(1.25, 0.0)) * 5.0) * v_info.y;
		color = over(color, vec3(1.0, 0.92, 0.6), clamp(flash, 0.0, 1.0));

		// Health arc, clockwise from the top, when hurt and big enough to read.
		if (detailed && v_info.x < 0.999) {
			float r = length(upright);
			float band = 1.0 - smoothstep(0.07 - aa, 0.07 + aa, abs(r - 1.45));
			float angle = fract(atan(upright.x, -upright.y) / 6.2831853 + 1.0);
			vec3 arc = angle < v_info.x ? mix(vec3(0.95, 0.25, 0.2), vec3(0.35, 0.95, 0.4), v_info.x) : vec3(0.12);
			color = over(color, arc, band * 0.9);
		}

		COLOR = vec4(color.rgb, color.a * v_color.a);
	}
}
