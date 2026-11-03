// Marks the point the item turns around (its origin), even when that point is
// outside the shape: the vertex stage grows the quad until it covers the pivot,
// and every fragment in the grown area is paid for. No uniforms.
// Expects a rect quad (four corners with UV 0 or 1), like draw_rect.
shader_type canvas_item;

const float BORDER_PX = 3.0;
const float MARK_PX = 16.0;          // crosshair reach, in screen pixels
const bool SHOW_PAID = true;         // tint the grown area you are paying for
const vec4 FILL = vec4(0.10, 0.12, 0.18, 1.0);
const vec4 EDGE = vec4(0.58, 0.64, 0.72, 1.0);
const vec4 MARK = vec4(0.93, 0.29, 0.60, 1.0);
const vec4 PAID = vec4(0.93, 0.29, 0.60, 0.06);

varying flat vec2 pivot_px;   // origin in screen pixels
varying flat vec2 px_scale;   // screen pixels per local unit, per axis
varying vec2 local_pos;       // position in the grown quad, local units
varying vec2 t;               // original UV, stretched over the grown quad
varying vec2 left_term;       // original corner * (1 - t): recovers the left/top edge
varying vec2 right_term;      // original corner * t: recovers the right/bottom edge

void vertex() {
	mat4 to_px = CANVAS_MATRIX * MODEL_MATRIX;
	pivot_px = to_px[3].xy;
	px_scale = vec2(length(to_px[0].xy), length(to_px[1].xy));

	t = UV;
	left_term = VERTEX * (1.0 - UV);
	right_term = VERTEX * UV;

	// The pivot is local (0,0). Push each corner out until the quad covers it
	// plus the marker's reach; a corner already past it stays put.
	vec2 reach = (MARK_PX + 1.0) / px_scale;
	VERTEX = mix(min(VERTEX, -reach), max(VERTEX, reach), step(0.5, UV));
	local_pos = VERTEX;
}

void fragment() {
	// Interpolated a*(1-t) / (1-t) and b*t / t give back the fixed edges a, b.
	vec2 lo = left_term / (1.0 - t);
	vec2 hi = right_term / t;

	vec2 d_px = min(local_pos - lo, hi - local_pos) * px_scale;
	float edge_px = min(d_px.x, d_px.y);
	float inside = clamp(edge_px + 0.5, 0.0, 1.0);
	vec4 shape = mix(FILL, EDGE, clamp(BORDER_PX - edge_px + 0.5, 0.0, 1.0));
	vec4 color = mix(SHOW_PAID ? PAID : vec4(0.0), shape, inside);

	// Crosshair in screen space, so it stays upright while the square spins.
	vec2 to_pivot = abs(FRAGCOORD.xy - pivot_px);
	float ring = abs(length(to_pivot) - 10.0);
	float cross = min(to_pivot.x, to_pivot.y) < 1.0 && max(to_pivot.x, to_pivot.y) < MARK_PX ? 0.0 : 9.0;
	float mark = 1.0 - clamp(min(ring - 0.5, cross), 0.0, 1.0);
	COLOR = mix(color, MARK, mark);
}
