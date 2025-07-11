#include "res://Shaders/Base.gdshaderinc"
#include "res://Shaders/ActiveUtils/CanvasSizing.gdshaderinc"

uniform float outline : hint_range(0, 10, 1) = 3.;

#define CALCULATE_PARTIAL\
	SIZE \
	COLOR *=  getColour();

void vertex(){
	CALCULATE_PARTIAL
}
