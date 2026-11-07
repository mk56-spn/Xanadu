// A Frutiger Aero glass button on its own quad, padded so its shadow and halo
// fit. It draws over whatever is behind it: the glass refracts the screen, so
// it works on any backdrop. No textures, no fonts: the pill, gloss, glow,
// bubbles and the PLAY label are distance fields and gradients.
// The host only feeds the eased hover / press state and the last click.
shader_type canvas_item;

#include "res://Experiments/Aero/aero_common.gdshaderinc"

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
uniform vec2 half_size;            // pill half extents, px
uniform float hover = 0.0;         // 0..1, eased
uniform float press = 0.0;         // 0..1, eased
uniform vec2 ripple_at;            // last click, px from the centre
uniform float ripple_age = 10.0;   // seconds since that click

const float LENS = 0.55;           // how much the glass shrinks what is behind it
const vec3 GLASS_TOP = vec3(0.42, 0.90, 0.88);
const vec3 GLASS_MID = vec3(0.00, 0.47, 0.62);
const vec3 GLASS_LOW = vec3(0.16, 0.80, 0.78);
const vec3 GLOW = vec3(0.55, 1.00, 0.88);
const vec3 SHADOW = vec3(0.02, 0.18, 0.28);
const vec3 STROKE = vec3(0.00, 0.26, 0.38);
const vec3 INK_SHADOW = vec3(0.00, 0.22, 0.32);

varying vec2 pos;              // local px, 0 at the button centre
varying flat vec2 px_scale;    // screen px per local px, per axis

void vertex() {
	pos = VERTEX;
	mat4 to_px = CANVAS_MATRIX * MODEL_MATRIX;
	px_scale = vec2(length(to_px[0].xy), length(to_px[1].xy));
}

float sd_pill(vec2 p, vec2 h) {
	float r = min(h.x, h.y);
	vec2 q = abs(p) - h + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

float sd_seg(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a, ba = b - a;
	return length(pa - ba * clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0));
}

// The PLAY label: a play triangle and four stroke letters, in em units
// (y down, 0 at the cap line, 1 at the baseline). Returns distance in em.
float label(vec2 q) {
	vec2 t = q - vec2(0.0, 0.5);
	float d = max(-t.x, max(dot(t, vec2(0.53, 0.85)), dot(t, vec2(0.53, -0.85))) - 0.36) + 0.04;

	vec2 p = q - vec2(1.15, 0.0);   // P
	d = min(d, sd_seg(p, vec2(0.0, 0.0), vec2(0.0, 1.0)));
	d = min(d, sd_seg(p, vec2(0.0, 0.0), vec2(0.27, 0.0)));
	d = min(d, sd_seg(p, vec2(0.0, 0.52), vec2(0.27, 0.52)));
	if (p.x > 0.27) d = min(d, abs(length(p - vec2(0.27, 0.26)) - 0.26));

	p = q - vec2(1.95, 0.0);        // L
	d = min(d, sd_seg(p, vec2(0.0, 0.0), vec2(0.0, 1.0)));
	d = min(d, sd_seg(p, vec2(0.0, 1.0), vec2(0.46, 1.0)));

	p = q - vec2(2.62, 0.0);        // A
	d = min(d, sd_seg(p, vec2(0.0, 1.0), vec2(0.31, 0.0)));
	d = min(d, sd_seg(p, vec2(0.31, 0.0), vec2(0.62, 1.0)));
	d = min(d, sd_seg(p, vec2(0.12, 0.64), vec2(0.50, 0.64)));

	p = q - vec2(3.48, 0.0);        // Y
	d = min(d, sd_seg(p, vec2(0.0, 0.0), vec2(0.30, 0.52)));
	d = min(d, sd_seg(p, vec2(0.60, 0.0), vec2(0.30, 0.52)));
	d = min(d, sd_seg(p, vec2(0.30, 0.52), vec2(0.30, 1.0)));
	return d;
}

const float LABEL_EM = 4.08;   // label width in em

void fragment() {
	// Pressing sinks the button a little and shrinks it a touch.
	vec2 h = half_size * (1.0 - 0.015 * press);
	vec2 p = pos - vec2(0.0, press * 4.0);
	float sd = sd_pill(p, h);
	float aa = fwidth(sd);
	float inside = 1.0 - smoothstep(-aa, aa, sd);

	// Outside: a soft drop shadow that tightens as it is pressed, and a hover halo.
	float sds = sd_pill(pos - vec2(0.0, 16.0 - press * 10.0), h * vec2(0.97, 0.9));
	float shadow = 0.38 * exp(-max(sds, 0.0) / (20.0 - press * 8.0));
	float halo = 0.55 * hover * exp(-max(sd, 0.0) / 26.0);
	vec4 outside = vec4(mix(SHADOW, GLOW, halo / max(halo + shadow, 1e-4)), clamp(shadow + halo, 0.0, 1.0));

	vec3 body = vec3(0.0);
	if (sd < aa * 2.0) {
		float v = (p.y + h.y) / (2.0 * h.y);   // 0 top .. 1 bottom
		float depth = -sd;

		// Glass: what is behind, pulled in toward the centre like a lens, then tinted.
		vec2 bend = -p * (1.0 - LENS) * px_scale * SCREEN_PIXEL_SIZE;
		vec3 seen = textureLod(screen, SCREEN_UV + bend, 1.5).rgb;
		vec3 tint = mix(GLASS_TOP, GLASS_MID, smoothstep(0.0, 0.55, v));
		tint = mix(tint, GLASS_LOW, smoothstep(0.55, 1.0, v));
		body = mix(seen, tint, 0.78) * (1.0 - 0.18 * press);

		// Light pooling at the bottom, brighter on hover.
		vec2 g = (p - vec2(0.0, h.y * 0.78)) / vec2(h.x * 0.85, h.y * 0.62);
		body += GLOW * exp(-dot(g, g) * 1.6) * (0.55 + 0.35 * hover - 0.2 * press);

		// Bubbles rising inside the glass.
		for (int i = 0; i < 9; i++) {
			float s = float(i) + 40.0;
			float r = mix(5.0, 15.0, hash(s * 2.3));
			float x = (hash(s * 1.3) * 2.0 - 1.0) * (h.x - h.y * 0.6) + sin(TIME * 1.3 + s) * 6.0;
			float y = h.y + r - fract(hash(s * 4.1) + TIME * mix(0.12, 0.3, hash(s * 6.7)) * (1.0 + hover)) * (2.0 * h.y + 2.0 * r);
			vec4 b = bubble(p, vec2(x, y), r);
			body = mix(body, b.rgb, b.a * 0.8);
		}

		// Darker toward the edge, so it reads as a thick lens.
		body *= mix(0.72, 1.0, smoothstep(0.0, 22.0, depth));

		// Click ripple, spreading from the press point.
		float ring = exp(-abs(length(pos - ripple_at) - ripple_age * 700.0) / 10.0) * exp(-ripple_age * 2.5);
		body += vec3(0.8, 1.0, 1.0) * ring * 0.5;

		// The label, with a soft shadow below it.
		float em = h.y * 0.62;
		vec2 lq = (p - vec2(-LABEL_EM * em * 0.5, -em * 0.5 + 1.0)) / em;
		float ld = label(lq) * em;
		float sh = label(lq - vec2(0.0, 3.0 / em)) * em;
		float stroke = em * 0.085;
		body = mix(body, INK_SHADOW, 0.55 * (1.0 - smoothstep(stroke - 1.0, stroke + 5.0, sh)));
		vec3 ink = mix(vec3(1.0), vec3(0.80, 1.0, 0.98), clamp(lq.y, 0.0, 1.0));
		body = mix(body, ink, 1.0 - smoothstep(stroke - 0.8, stroke + 0.8, ld));

		// Gloss: the top half under a curved lower edge, inset from the rim.
		vec2 e = (p - vec2(0.0, -h.y * 1.55)) / vec2(h.x * 1.25, h.y * 2.05);
		float lens = 1.0 - smoothstep(0.98, 1.0, length(e));
		float inset = 1.0 - smoothstep(-7.0, -4.0, sd);
		float gloss = lens * inset * mix(0.85, 0.10, smoothstep(0.03, 0.5, v)) * (1.0 - 0.45 * press);
		body = mix(body, vec3(1.0), gloss);

		// A shine sweeping across now and then, every sweep on hover.
		float phase = fract(TIME * (0.22 + 0.25 * hover));
		float band = p.x + p.y * 0.7 - mix(-h.x * 1.5, h.x * 1.5, phase);
		body += vec3(1.0) * exp(-band * band / 900.0) * (0.12 + 0.28 * hover) * smoothstep(0.0, 6.0, depth);

		// Rim: a thin bright inner line over a dark outer stroke.
		body += vec3(0.9, 1.0, 1.0) * exp(-abs(sd + 2.5) / 1.1) * mix(0.75, 0.35, v);
		body = mix(body, STROKE, exp(-abs(sd + 0.3) / 0.9) * 0.85);
	}

	COLOR = mix(outside, vec4(body, 1.0), inside);
}
