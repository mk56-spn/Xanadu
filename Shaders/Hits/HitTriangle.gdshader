shader_type canvas_item;

#define OVERRIDE_DEFAULT_VERTEX


#include "res://Shaders/Base.gdshaderinc"
#include "res://Shaders/NoteBase.gdshaderinc"


void vertex(){
	SIZE
	VERTEX *= 4. * hitProgress();
}
void fragment() {

	COLOR = renderSDFWithOutline(sdCircle(PIXEL_UV, 50),TRANSPARENT, COLOR * 1. - hitProgress(),2.,4.);
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
