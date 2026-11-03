// Constant screen-pixel border without any resolution or scale uniform.
// The local -> viewport-pixel scale is read from the matrix built-ins once per
// vertex; the fragment only multiplies. Expects the quad's origin at local (0,0).
shader_type canvas_item;

const float BORDER_PX = 5.0;
const vec4 FILL = vec4(0.10, 0.12, 0.18, 1.0);
const vec4 BORDER = vec4(0.98, 0.80, 0.08, 1.0);

varying vec2 local_pos;
varying flat vec2 px_scale;

void vertex() {
	mat4 to_px = CANVAS_MATRIX * MODEL_MATRIX;
	px_scale = vec2(length(to_px[0].xy), length(to_px[1].xy));
	local_pos = VERTEX;
}

void fragment() {
	// local_pos and UV are both linear over the quad: their ratio is its size.
	vec2 size = local_pos / UV;
	vec2 d_px = min(local_pos, size - local_pos) * px_scale;
	float edge_px = min(d_px.x, d_px.y);
	COLOR = mix(FILL, BORDER, clamp(BORDER_PX - edge_px + 0.5, 0.0, 1.0));
}
