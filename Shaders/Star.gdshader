shader_type canvas_item;


#include "ActiveUtils/CanvasSizing.gdshaderinc"

uniform float grid_sizeV : hint_range(10., 500.0, 5.) = 100.0;

uniform vec2 grid_size = vec2(100.,100.);
uniform float column : hint_range(20., 50.0, 1.);

uniform sampler2D tex;

void vertex(){

	COLOR.a = INSTANCE_CUSTOM.r * 0.20;
	SIZE
	/*COLOR.r = (sin(float(INSTANCE_ID) + TIME + 1.));
	COLOR.b = (sin(float(INSTANCE_ID) + TIME + 3.));
	COLOR.g = (sin(float(INSTANCE_ID) + TIME + 5.));

	vec2 tex_point = vec2(float(INSTANCE_ID) * 0.01,1.);
	VERTEX.x += texture(tex, tex_point ).r *200.;

	VERTEX.x += float(INSTANCE_ID / int(column)) * grid_size.x + sin(TIME+ float(INSTANCE_ID )) * 30.;
	VERTEX.y += mod(float(INSTANCE_ID), column) * grid_size.y;*/

}

void fragment() {

}
