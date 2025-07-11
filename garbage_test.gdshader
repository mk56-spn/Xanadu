shader_type canvas_item;

varying vec2 uv;
varying  vec2 true_uv;

varying flat vec2 canvas_half_size;

void vertex() {
	
	COLOR.a = float (INSTANCE_ID % 2);
	vec2 f = VERTEX.xy / UV;
	uv.x = float( VERTEX_ID < 1  || VERTEX_ID> 2);
}
void fragment(){

}