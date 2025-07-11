shader_type canvas_item;

#include "BlockBase.gdshaderinc"

void fragment(){
	float dist = sdBox(pixel_uv,canvas_half_size);
	COLOR *= renderSDFWithOutline(dist +outline,vec4(1.), vec4(vec3(0.8),1.),outline,0.5);
}