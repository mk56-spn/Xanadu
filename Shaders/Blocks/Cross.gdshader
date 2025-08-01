shader_type canvas_item;

#include "uid://c126trlildmkv"

void fragment() {
	vec2 rot_uv = rotate(PIXEL_UV, PI * 0.25);

	float side = min(CANVAS_HALF_SIZE.x, CANVAS_HALF_SIZE.y);
	float dist = sdCross(rot_uv,vec2(side/1.,3.),0.);

	COLOR *= renderSDFWithOutline(dist +outline,vec4(1.), vec4(1.),outline,0.5);
}