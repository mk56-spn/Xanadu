#include "res://Shaders/Base.gdshaderinc"

uniform float outline : hint_range(0, 10, 1) = 2.;

#define CALCULATE_PARTIAL\
	SIZE \
	COLOR = getColour();

void vertex(){
	CALCULATE_PARTIAL
}

