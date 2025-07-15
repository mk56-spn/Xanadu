shader_type canvas_item;
render_mode blend_add;

#include "ActiveUtils/Perspective.gdshaderinc"
#include "NoteBase.gdshaderinc"

void vertex() {
	SIZE
	PERSPECTIVE

}

void fragment() {
	PERSPECTIVE_FRAGMENT
	float dist = sdCircle(uv * canvas_half_size ,canvas_half_size.x / 2.);
	dist = abs(dist) - 30.;


	COLOR *= renderSDFWithOutline(dist,vec4(vec3(1.),1.),vec4(0.3),2,0.4);
	COLOR *= checkerboard_aa(uv,50.);
	COLOR.a *= 1. - hitProgress();

}