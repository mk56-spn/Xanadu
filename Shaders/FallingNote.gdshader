shader_type canvas_item;
#include "NoteBase.gdshaderinc"

const float speed = 500.;

void vertex(){
	SIZE
	VERTEX.y -= noteProgress()* speed;
	COLOR.a = 1. - noteProgress();
}

void fragment(){
	vec4 col = vec4(1.,1.,0.,1.);
	COLOR *= renderSDFWithOutline(sdBox(pixel_uv,vec2(20,10)),col,col-0.1,2.,0.5);
}
