shader_type canvas_item;

#define OVERRIDE_DEFAULT_VERTEX
#include "NoteBase.gdshaderinc"

const float speed = 500.;

void vertex(){
	SIZE
	VERTEX.y -= noteProgress()* speed;
	COLOR.a = 1. - noteProgress();
}

void fragment(){
	COLOR = renderSDF(sdBox(PIXEL_UV,vec2(20,10)),COLOR ,0.5);
}
