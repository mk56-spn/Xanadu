shader_type canvas_item;

#include "uid://c126trlildmkv"

void fragment() {
	vec2 rot_uv = rotate(pixel_uv, PI * 0.25);

	float side = min(canvas_half_size.x, canvas_half_size.y);
	float dist = sdCross(rot_uv,vec2(side/1.,3.),0.);

	COLOR *= renderSDFWithOutline(dist +outline,vec4(1.), vec4(1.),outline,0.5);
}