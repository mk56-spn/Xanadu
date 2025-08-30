shader_type canvas_item;

varying float vertical ;
void vertex() {
	if (VERTEX_ID == 1 ||VERTEX_ID == 2 ){
		vertical = 0.;
	}
	else{
		vertical = 1.;
	}
}

void fragment() {
	COLOR.rgb *=1. - 0.5 * abs(distance(vertical,0.5));
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
