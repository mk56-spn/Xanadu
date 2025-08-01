shader_type canvas_item;

#include "uid://c126trlildmkv"

void fragment(){
	float dist = sdRoundedBox(PIXEL_UV,CANVAS_HALF_SIZE, vec4(5.));
	COLOR *= renderSDFWithOutline(dist +outline,vec4(1.), vec4(vec3(0.8),1.),outline,0.5);
}