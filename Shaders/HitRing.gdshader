shader_type canvas_item;
render_mode blend_add;

#define OVERRIDE_DEFAULT_VERTEX

#include "ActiveUtils/Perspective.gdshaderinc"
#include "NoteBase.gdshaderinc"
#include "Decorative/Glows.gdshaderinc"

void vertex(){
	PERSPECTIVE
	SIZE
	VERTEX *= 1. -hitProgress();
}
void fragment() {
	PERSPECTIVE_FRAGMENT
	float dist = sdCircle(uv * CANVAS_HALF_SIZE, CANVAS_HALF_SIZE.x / 2. - 15.);
	dist = abs(dist) - 30.;

	COLOR *= renderSDF(dist, COLOR,1.);
	COLOR *= checkerboard(uv * CANVAS_HALF_SIZE , 10.);
	COLOR += flare(1.);
	COLOR.a *= 1. - hitProgress();
}