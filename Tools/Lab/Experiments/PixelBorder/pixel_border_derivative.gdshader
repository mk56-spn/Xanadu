// Constant screen-pixel border without any resolution or scale uniform.
// Screen-space derivatives give UV change per pixel, per fragment.
shader_type canvas_item;

const float BORDER_PX = 5.0;
const vec4 FILL = vec4(0.10, 0.12, 0.18, 1.0);
const vec4 BORDER = vec4(0.22, 0.74, 0.97, 1.0);

void fragment() {
	vec2 d_uv = min(UV, 1.0 - UV);
	// Gradient length, not fwidth, so rotation does not thin the border.
	vec2 uv_per_px = vec2(
		length(vec2(dFdx(UV.x), dFdy(UV.x))),
		length(vec2(dFdx(UV.y), dFdy(UV.y))));
	vec2 d_px = d_uv / uv_per_px;
	float edge_px = min(d_px.x, d_px.y);
	COLOR = mix(FILL, BORDER, clamp(BORDER_PX - edge_px + 0.5, 0.0, 1.0));
}
